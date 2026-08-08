using System.Text.Json;
using NetworkDiagnostics.Desktop.Monitoring;

namespace NetworkDiagnostics.Desktop;

public enum AppearancePreference
{
    System,
    Light,
    Dark
}

public sealed record PhotinoAppSettings(
    AppearancePreference Appearance,
    bool MonitoringEnabled,
    string MonitoringWindow,
    int MonitoringIntervalSeconds,
    int MonitoringAlertScoreThreshold,
    double ExpectedDownloadMbps,
    double ExpectedUploadMbps,
    IReadOnlyList<string> TestOrigins,
    string? InterfaceId,
    bool IncludeLocalIdentifiers,
    string? LanTarget,
    int LanPort,
    int LanDurationSeconds,
    int LanConnections)
{
    public static PhotinoAppSettings Default { get; } = new(
        AppearancePreference.System,
        true,
        MonitorWindow.FiveMinutes.ContractId(),
        5,
        70,
        100,
        20,
        [],
        null,
        false,
        null,
        8765,
        8,
        4);

    public MonitorWindow SelectedMonitoringWindow => MonitorWindowExtensions.Parse(MonitoringWindow);

    public MonitorOptions ToMonitorOptions() => new(
        MonitoringEnabled,
        new Uri("https://network.johnnyli.dev/"),
        TimeSpan.FromSeconds(Math.Clamp(MonitoringIntervalSeconds, 2, 60)),
        Math.Clamp(MonitoringAlertScoreThreshold, 1, 100),
        Math.Max(1, ExpectedDownloadMbps),
        Math.Max(1, ExpectedUploadMbps),
        0);
}

public sealed class PhotinoSettingsStore
{
    private const int MaximumEndpointCandidates = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object gate = new();
    private readonly string settingsPath;

    public PhotinoSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = Path.GetFullPath(settingsPath ?? GetDefaultSettingsPath());
        RootDirectory = Path.GetDirectoryName(this.settingsPath)
            ?? throw new InvalidOperationException("The desktop settings path does not have a parent directory.");
    }

    public string RootDirectory { get; }

    public PhotinoAppSettings Load()
    {
        lock (gate)
        {
            return LoadCore();
        }
    }

    public PhotinoAppSettings SaveAppearance(AppearancePreference appearance) =>
        Update(settings => settings with { Appearance = appearance });

    public PhotinoAppSettings SaveMonitoringEnabled(bool enabled) =>
        Update(settings => settings with { MonitoringEnabled = enabled });

    public PhotinoAppSettings SaveMonitoringWindow(MonitorWindow window) =>
        Update(settings => settings with { MonitoringWindow = window.ContractId() });

    public PhotinoAppSettings SaveExpectedCapacity(double downloadMbps, double uploadMbps)
    {
        if (!double.IsFinite(downloadMbps) || downloadMbps is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(downloadMbps), "Expected download must be between 1 and 100,000 Mbps.");
        }
        if (!double.IsFinite(uploadMbps) || uploadMbps is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(uploadMbps), "Expected upload must be between 1 and 100,000 Mbps.");
        }
        return Update(settings => settings with
        {
            ExpectedDownloadMbps = downloadMbps,
            ExpectedUploadMbps = uploadMbps
        });
    }

    public PhotinoAppSettings SaveAdvanced(
        IEnumerable<string> endpointCandidates,
        string? interfaceId,
        bool includeLocalIdentifiers,
        string? lanTarget,
        int lanPort,
        int lanDurationSeconds,
        int lanConnections)
    {
        ArgumentNullException.ThrowIfNull(endpointCandidates);
        var origins = NormalizeOrigins(endpointCandidates);
        if (lanPort is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(lanPort));
        if (lanDurationSeconds is < 3 or > 30) throw new ArgumentOutOfRangeException(nameof(lanDurationSeconds));
        if (lanConnections is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(lanConnections));

        return Update(settings => settings with
        {
            TestOrigins = origins,
            InterfaceId = NormalizeOptional(interfaceId),
            IncludeLocalIdentifiers = includeLocalIdentifiers,
            LanTarget = NormalizeOptional(lanTarget),
            LanPort = lanPort,
            LanDurationSeconds = lanDurationSeconds,
            LanConnections = lanConnections
        });
    }

    private PhotinoAppSettings Update(Func<PhotinoAppSettings, PhotinoAppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (gate)
        {
            var settings = update(LoadCore());
            SaveCore(settings);
            return settings;
        }
    }

    private void SaveCore(PhotinoAppSettings settings)
    {
        Directory.CreateDirectory(RootDirectory);
        var document = new SettingsDocument(
            BridgeProtocol.AppearanceId(settings.Appearance),
            settings.MonitoringEnabled,
            settings.SelectedMonitoringWindow.ContractId(),
            Math.Clamp(settings.MonitoringIntervalSeconds, 2, 60),
            Math.Clamp(settings.MonitoringAlertScoreThreshold, 1, 100),
            Math.Max(1, settings.ExpectedDownloadMbps),
            Math.Max(1, settings.ExpectedUploadMbps),
            settings.TestOrigins.ToArray(),
            settings.InterfaceId,
            settings.IncludeLocalIdentifiers,
            settings.LanTarget,
            settings.LanPort,
            settings.LanDurationSeconds,
            settings.LanConnections);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var temporaryPath = settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, settingsPath, overwrite: true);
    }

    private PhotinoAppSettings LoadCore()
    {
        if (!File.Exists(settingsPath)) return PhotinoAppSettings.Default;

        try
        {
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(settingsPath), JsonOptions);
            if (document is null) return PhotinoAppSettings.Default;
            return new PhotinoAppSettings(
                BridgeProtocol.ParseAppearance(document.Appearance),
                document.MonitoringEnabled,
                MonitorWindowExtensions.Parse(document.MonitoringWindow).ContractId(),
                Math.Clamp(document.MonitoringIntervalSeconds, 2, 60),
                Math.Clamp(document.MonitoringAlertScoreThreshold, 1, 100),
                Math.Max(1, document.ExpectedDownloadMbps),
                Math.Max(1, document.ExpectedUploadMbps),
                NormalizeOrigins(document.TestOrigins ?? []),
                NormalizeOptional(document.InterfaceId),
                document.IncludeLocalIdentifiers,
                NormalizeOptional(document.LanTarget),
                document.LanPort is >= 1024 and <= 65535 ? document.LanPort : 8765,
                document.LanDurationSeconds is >= 3 and <= 30 ? document.LanDurationSeconds : 8,
                document.LanConnections is >= 1 and <= 16 ? document.LanConnections : 4);
        }
        catch (JsonException)
        {
            return PhotinoAppSettings.Default;
        }
        catch (IOException)
        {
            return PhotinoAppSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return PhotinoAppSettings.Default;
        }
        catch (ArgumentException)
        {
            return PhotinoAppSettings.Default;
        }
    }

    private static IReadOnlyList<string> NormalizeOrigins(IEnumerable<string> values)
    {
        var origins = values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (origins.Length > MaximumEndpointCandidates)
        {
            throw new ArgumentException($"Configure no more than {MaximumEndpointCandidates} measurement endpoint candidates.");
        }
        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException($"Endpoint '{origin}' must be an absolute HTTP or HTTPS URL.");
            }
        }
        return origins;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetDefaultSettingsPath()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "NetworkDiagnostics", "desktop-settings.json");
    }

    private sealed record SettingsDocument(
        string Appearance = "system",
        bool MonitoringEnabled = true,
        string MonitoringWindow = "5m",
        int MonitoringIntervalSeconds = 5,
        int MonitoringAlertScoreThreshold = 70,
        double ExpectedDownloadMbps = 100,
        double ExpectedUploadMbps = 20,
        string[]? TestOrigins = null,
        string? InterfaceId = null,
        bool IncludeLocalIdentifiers = false,
        string? LanTarget = null,
        int LanPort = 8765,
        int LanDurationSeconds = 8,
        int LanConnections = 4);
}

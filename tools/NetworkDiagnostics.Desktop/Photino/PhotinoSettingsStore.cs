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
    double ExpectedUploadMbps)
{
    public static PhotinoAppSettings Default { get; } = new(
        AppearancePreference.System,
        true,
        MonitorWindow.FiveMinutes.ContractId(),
        5,
        70,
        100,
        20);

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
            Math.Max(1, settings.ExpectedUploadMbps));
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
                Math.Max(1, document.ExpectedUploadMbps));
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
    }

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
        double ExpectedUploadMbps = 20);
}

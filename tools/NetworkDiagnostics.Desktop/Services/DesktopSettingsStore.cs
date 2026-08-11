using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Services;

public sealed record DesktopSettings(
    bool IncludeLocalIdentifiers = false,
    string Appearance = "system",
    string DefaultProfile = "connection-check",
    string DefaultTransferMethod = "compare",
    string? ReportDirectory = null,
    string? TestOrigin = null,
    IReadOnlyList<string>? TestOrigins = null,
    string? InterfaceId = null,
    string? LanTarget = null,
    int LanPort = 8765,
    int LanDurationSeconds = 8,
    int LanConnections = 4,
    long FullApprovedCapBytes = 0,
    long StressApprovedCapBytes = 0,
    DesktopWorkbenchState? Workbench = null)
{
    public TestProfileId SelectedProfile => DefaultProfile switch
    {
        "quick" => TestProfileId.Quick,
        "standard" => TestProfileId.Standard,
        "extended" => TestProfileId.Extended,
        _ => TestProfileId.ConnectionCheck
    };

    public TransferMethod SelectedTransferMethod => DefaultTransferMethod switch
    {
        "single" => TransferMethod.Single,
        "aggregate" => TransferMethod.Aggregate,
        _ => TransferMethod.Compare
    };

    public IReadOnlyList<Uri> ParsedTestOrigins
    {
        get
        {
            var values = (TestOrigins ?? [])
                .Concat(string.IsNullOrWhiteSpace(TestOrigin) ? [] : [TestOrigin])
                .Select(ParseOrigin)
                .Where(uri => uri is not null)
                .Cast<Uri>()
                .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();
            return values.Length == 0 ? [new Uri("https://network.johnnyli.dev/")] : values;
        }
    }

    public bool HasDataApproval(TestProfileId profile, long currentCapBytes) => profile switch
    {
        TestProfileId.Standard => FullApprovedCapBytes >= currentCapBytes,
        TestProfileId.Extended => StressApprovedCapBytes >= currentCapBytes,
        _ => true
    };

    public DesktopSettings WithDataApproval(TestProfileId profile, long capBytes) => profile switch
    {
        TestProfileId.Standard => this with { FullApprovedCapBytes = capBytes },
        TestProfileId.Extended => this with { StressApprovedCapBytes = capBytes },
        _ => this
    };

    public DesktopSettings ResetDataApprovals() => this with
    {
        FullApprovedCapBytes = 0,
        StressApprovedCapBytes = 0
    };

    public static string ContractId(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "connection-check",
        TestProfileId.Quick => "quick",
        TestProfileId.Standard => "standard",
        TestProfileId.Extended => "extended",
        _ => "connection-check"
    };

    public static string ContractId(TransferMethod method) => method switch
    {
        TransferMethod.Single => "single",
        TransferMethod.Aggregate => "aggregate",
        _ => "compare"
    };

    public static IReadOnlyList<string> ParseOriginLines(string? value)
    {
        return (value ?? string.Empty)
            .Replace(";", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static Uri? ParseOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")) return null;
        var builder = new UriBuilder(uri);
        if (!builder.Path.EndsWith("/", StringComparison.Ordinal)) builder.Path += "/";
        return builder.Uri;
    }
}

public sealed class DesktopSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DesktopSettingsStore(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JohnnyLi",
            "NetworkDiagnostics");
        SettingsPath = Path.Combine(RootDirectory, "settings.json");
    }

    public string RootDirectory { get; }

    public string SettingsPath { get; }

    public async Task<DesktopSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath)) return new DesktopSettings();
        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
            return JsonSerializer.Deserialize<DesktopSettings>(json, JsonOptions) ?? new DesktopSettings();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new DesktopSettings();
        }
    }

    public async Task SaveAsync(DesktopSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(RootDirectory);
        var temporary = $"{SettingsPath}.tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
        File.Move(temporary, SettingsPath, true);
    }
}

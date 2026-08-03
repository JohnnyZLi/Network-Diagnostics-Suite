using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Services;

public sealed record DesktopSettings(
    bool IncludeLocalIdentifiers = false,
    string DefaultProfile = "connection-check",
    string? ReportDirectory = null,
    string? TestOrigin = null,
    long FullApprovedCapBytes = 0,
    long StressApprovedCapBytes = 0)
{
    public TestProfileId SelectedProfile => DefaultProfile switch
    {
        "quick" => TestProfileId.Quick,
        "standard" => TestProfileId.Standard,
        "extended" => TestProfileId.Extended,
        _ => TestProfileId.ConnectionCheck
    };

    public Uri? ParsedTestOrigin => Uri.TryCreate(TestOrigin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
            ? EnsureTrailingSlash(uri)
            : null;

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

    private static Uri EnsureTrailingSlash(Uri uri)
    {
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

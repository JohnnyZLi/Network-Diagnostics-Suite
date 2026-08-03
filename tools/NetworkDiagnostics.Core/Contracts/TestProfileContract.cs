using System.Reflection;
using System.Text.Json;

namespace NetworkDeepProbe.Contracts;

public static class TestProfileContract
{
    private const string ResourceName = "NetworkDiagnostics.Contracts.desktop-test-profiles.v1.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string ReadJson()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded profile contract {ResourceName} was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static TestProfileContractDocument Load()
    {
        return JsonSerializer.Deserialize<TestProfileContractDocument>(ReadJson(), JsonOptions)
            ?? throw new InvalidOperationException("The embedded profile contract could not be parsed.");
    }
}

public sealed record TestProfileContractDocument(
    string SchemaVersion,
    IReadOnlyList<string> TransferMethods,
    IReadOnlyList<TestProfileDefinition> Profiles);

public sealed record TestProfileDefinition(
    string Id,
    string Name,
    int EstimatedSeconds,
    int IdlePingCount,
    int PingIntervalMs,
    int DownloadDurationMs,
    long DownloadCapBytes,
    int DownloadSamples,
    int UploadDurationMs,
    long UploadCapBytes,
    int AggregateDownloadConnections,
    int AggregateUploadConnections,
    bool IncludeServices,
    TestProfileComparison Comparison,
    IReadOnlyList<ConnectionScalingStage> DownloadScaling);

public sealed record TestProfileComparison(
    int SingleDownloadDurationMs,
    long SingleDownloadCapBytes,
    int SingleUploadDurationMs,
    long SingleUploadCapBytes);

public sealed record ConnectionScalingStage(
    int Connections,
    int DurationMs,
    long CapBytes);

using System.Reflection;
using System.Text.Json;

namespace NetworkDeepProbe.Contracts;

public static class DiagnosticRulesContract
{
    private const string ResourceName = "NetworkDiagnostics.Contracts.diagnostic-rules.v1.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string ReadJson()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded diagnostic rules contract {ResourceName} was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static DiagnosticRulesDocument Load()
    {
        return JsonSerializer.Deserialize<DiagnosticRulesDocument>(ReadJson(), JsonOptions)
            ?? throw new InvalidOperationException("The embedded diagnostic rules contract could not be parsed.");
    }
}

public sealed record DiagnosticRulesDocument(
    string SchemaVersion,
    ApplicationLatencyRules ApplicationLatency,
    LoadedLatencyRules LoadedLatency,
    ThroughputRules Throughput,
    LocalDiagnosticRules LocalDiagnostics);

public sealed record ApplicationLatencyRules(
    double RequestLossWarningPercent,
    double RequestLossCriticalPercent,
    int MinimumSamplesForCriticalLoss,
    double IdleMedianWarningMs,
    double IdleJitterWarningMs);

public sealed record LoadedLatencyRules(
    double WarningIncreaseMs,
    double CriticalIncreaseMs);

public sealed record ThroughputRules(
    double SingleFlowShareWarningPercent,
    double MinimumAggregateMbpsForFlowComparison,
    double StabilityWarningPercent);

public sealed record LocalDiagnosticRules(
    double HealthyGatewayMedianMs,
    double HighInternetMedianMs,
    double SlowDnsMedianMs,
    int WeakWifiSignalPercent,
    double LocalLinkShareWarningPercent);

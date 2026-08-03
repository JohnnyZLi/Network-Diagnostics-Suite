namespace NetworkDeepProbe.Models;

public sealed record ReportProducer(
    string Application,
    string? Version,
    string? Engine);

public sealed record MeasurementEndpoint(
    string Id,
    string Name,
    string Provider,
    Uri Origin,
    bool IndependentProvider = false);

public sealed record EndpointProbeReport(
    string Id,
    string Name,
    string Provider,
    string Origin,
    bool Available,
    double? MedianLatencyMs,
    string? Error);

public sealed record MeasurementEndpointReport(
    string Id,
    string Name,
    string Provider,
    string Origin,
    string SelectionReason,
    double? PreflightLatencyMs);

public sealed record MeasurementContextReport(
    string ContractVersion,
    string Engine,
    string EngineVersion,
    IReadOnlyList<string> Capabilities,
    MeasurementEndpointReport SelectedEndpoint,
    IReadOnlyList<EndpointProbeReport> EndpointCandidates);

public sealed record DiagnosticEvidence(
    string Metric,
    string Label,
    string Value,
    string? Detail = null);

public sealed record DiagnosticFinding(
    string Id,
    string Category,
    string Severity,
    string Confidence,
    string Title,
    string Summary,
    IReadOnlyList<DiagnosticEvidence> Evidence,
    IReadOnlyList<string> Recommendations,
    string? NextTest = null);

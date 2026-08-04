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

public sealed record SelectedInterfaceReport(
    string Id,
    string Name,
    string Description,
    string Type,
    long? LinkSpeedMbps,
    string BindingScope,
    string? SourceAddress);

public sealed record NetworkMetadataReport(
    string? Edge,
    string? Network,
    int? Asn,
    string? Protocol,
    string? TlsVersion,
    string? IpVersion);

public sealed record Http3ProbeReport(
    bool Attempted,
    bool Supported,
    string? NegotiatedProtocol,
    double? DurationMs,
    string? Error);

public sealed record MeasurementContextReport(
    string ContractVersion,
    string Engine,
    string EngineVersion,
    IReadOnlyList<string> Capabilities,
    MeasurementEndpointReport SelectedEndpoint,
    IReadOnlyList<EndpointProbeReport> EndpointCandidates,
    SelectedInterfaceReport? SelectedInterface = null,
    NetworkMetadataReport? Network = null,
    Http3ProbeReport? Http3 = null);

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

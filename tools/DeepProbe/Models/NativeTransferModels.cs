using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Models;

public sealed record NativeTransferProgress(
    string Phase,
    string Stage,
    double Fraction,
    double? LiveMbps,
    double? LiveLatencyMs,
    long BytesTransferred);

public sealed record NativeTimelinePoint(
    double ElapsedMs,
    double Mbps);

public sealed record NativeThroughputSampleSummary(
    int Sample,
    double Mbps,
    double SteadyMbps,
    long Bytes,
    double DurationMs,
    double PeakMbps,
    double StabilityPercent,
    double? RampRatio,
    bool CapReached,
    string Qualification);

public sealed record NativeThroughputSummary(
    double Mbps,
    double SteadyMbps,
    long Bytes,
    double DurationMs,
    double PeakMbps,
    double StabilityPercent,
    double? RampRatio,
    bool CapReached,
    string Qualification,
    IReadOnlyList<NativeTimelinePoint> Timeline,
    string Aggregation,
    IReadOnlyList<NativeThroughputSampleSummary> Samples);

public sealed record NativeLoadedLatencyReport(
    LatencyStatistics Statistics,
    double? IncreaseMs,
    string Grade);

public sealed record NativeFlowMeasurement(
    TransferStrategy Strategy,
    int Connections,
    NativeThroughputSummary? Download,
    NativeThroughputSummary? Upload,
    NativeLoadedLatencyReport? DownloadLatency,
    NativeLoadedLatencyReport? UploadLatency);

public sealed record NativeFlowScalingPoint(
    int Connections,
    NativeThroughputSummary Download,
    NativeLoadedLatencyReport DownloadLatency);

public sealed record NativeDownloadPathStatus(
    string RequestedPath,
    string SelectedPath,
    string R2ProbeStatus,
    string? FallbackReason,
    string R2Origin);

public sealed record NativeDownloadDeliveryReport(
    string RequestedPath,
    string SelectedPath,
    string R2ProbeStatus,
    string? FallbackReason,
    string R2Origin,
    long Bytes,
    int RequestsStarted,
    int RequestsCompleted,
    int R2Requests,
    int WorkerRequests);

public sealed record NativeInternetTransferReport(
    string Origin,
    LatencyStatistics IdleLatency,
    NativeThroughputSummary Download,
    NativeThroughputSummary Upload,
    NativeLoadedLatencyReport DownloadLatency,
    NativeLoadedLatencyReport UploadLatency,
    IReadOnlyList<NativeFlowMeasurement> FlowMeasurements,
    IReadOnlyList<NativeFlowScalingPoint> DownloadScaling,
    long DataUsedBytes,
    NativeDownloadDeliveryReport? DownloadDelivery = null);

public sealed record NativeTransferStageReport(
    string Id,
    TransferDirection Direction,
    TransferStrategy Strategy,
    int Connections,
    int DurationMs,
    long CapBytes,
    int Samples);

public sealed record NativeTransferPlanReport(
    TestProfileId Profile,
    TransferMethod Method,
    string ProfileName,
    int EstimatedSeconds,
    long TransferCapBytes,
    bool IncludeServices,
    IReadOnlyList<NativeTransferStageReport> DownloadStages,
    IReadOnlyList<NativeTransferStageReport> UploadStages)
{
    public static NativeTransferPlanReport FromPlan(NativeTransferPlan plan)
    {
        static NativeTransferStageReport Map(TransferStagePlan stage) => new(
            stage.Id,
            stage.Direction,
            stage.Strategy,
            stage.Connections,
            stage.DurationMs,
            stage.CapBytes,
            stage.Samples);

        return new NativeTransferPlanReport(
            plan.Profile,
            plan.Method,
            plan.ProfileName,
            plan.EstimatedSeconds,
            plan.TransferCapBytes,
            plan.IncludeServices,
            plan.DownloadStages.Select(Map).ToArray(),
            plan.UploadStages.Select(Map).ToArray());
    }
}

public sealed record DiagnosticRunMetadata(
    Guid Id,
    string Platform,
    string? Architecture,
    TestProfileId Profile,
    TransferMethod TransferMethod,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool IncludesLocalAddresses);

public sealed record BrowserEdgeEvidence(
    string? Edge,
    string? Network,
    int? Asn,
    string? Protocol,
    string? TlsVersion,
    string? IpVersion);

public sealed record BrowserServiceCheck(
    string Id,
    string Name,
    bool Reachable,
    double? DurationMs,
    string? Note);

public sealed record BrowserReportEvidence(
    BrowserEdgeEvidence? Edge,
    IReadOnlyList<BrowserServiceCheck> ServiceChecks);

public sealed record NetworkDiagnosticsReportV2(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    DiagnosticRunMetadata Run,
    NativeTransferPlanReport TransferPlan,
    NativeInternetTransferReport? InternetTransfer,
    DeepProbeReport? DeepDiagnostics,
    LanThroughputReport? LocalLink,
    ReportProducer? Producer = null,
    MeasurementContextReport? Measurement = null,
    IReadOnlyList<DiagnosticFinding>? Findings = null,
    BrowserReportEvidence? BrowserEvidence = null,
    LoadedPathLocalizationReport? LoadLocalization = null,
    DualStackReport? DualStack = null,
    NetworkChangeReport? NetworkChange = null,
    HostResourceReport? HostResources = null,
    ReportAnnotations? Annotations = null);

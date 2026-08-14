using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

public sealed record PresentedTransferStage(
    string Id,
    string Direction,
    string Strategy,
    int Connections,
    int DurationMs,
    long CapBytes,
    int Samples);

public sealed record PresentedDiagnosticPlan(
    string Profile,
    string ProfileName,
    string Method,
    string DownloadPath,
    int EstimatedSeconds,
    int InternetEstimatedSeconds,
    long InternetTransferCapBytes,
    bool IncludeServices,
    int ServiceCheckCount,
    bool DeepDiagnostics,
    string DiagnosticDepth,
    int IdlePingCount,
    int PingIntervalMs,
    IReadOnlyList<PresentedTransferStage> DownloadStages,
    IReadOnlyList<PresentedTransferStage> UploadStages,
    int DownloadRuns,
    int MaxDownloadConnections,
    int MaxUploadConnections,
    int TotalTransferStages,
    bool LanEnabled,
    string? LanTarget,
    int? LanPort,
    int? LanDurationSeconds,
    int? LanConnections,
    int LanEstimatedSeconds)
{
    public long TransferCapBytes => InternetTransferCapBytes;
}

public static class DiagnosticRunPlanPresenter
{
    public static PresentedDiagnosticPlan Build(
        PhotinoAppSettings settings,
        TestProfileId profile,
        TransferMethod method,
        DownloadPathPreference downloadPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var plan = NetworkDeepProbe.Diagnostics.NetworkDiagnosticsRunner.DescribePlan(profile, method);
        var deepDiagnostics = profile is TestProfileId.Standard or TestProfileId.Extended;
        var lanEnabled = deepDiagnostics && !string.IsNullOrWhiteSpace(settings.LanTarget);
        var lanEstimatedSeconds = lanEnabled ? settings.LanDurationSeconds * 2 + 2 : 0;

        static PresentedTransferStage Stage(TransferStagePlan stage) => new(
            stage.Id,
            stage.Direction.ToString().ToLowerInvariant(),
            stage.Strategy.ToString().ToLowerInvariant(),
            stage.Connections,
            stage.DurationMs,
            stage.CapBytes,
            stage.Samples);

        return new PresentedDiagnosticPlan(
            BridgeProtocol.ProfileId(profile),
            plan.ProfileName,
            BridgeProtocol.MethodId(method),
            BridgeProtocol.DownloadPathId(downloadPath),
            plan.EstimatedSeconds + lanEstimatedSeconds,
            plan.EstimatedSeconds,
            plan.TransferCapBytes,
            plan.IncludeServices,
            plan.IncludeServices ? 6 : 0,
            deepDiagnostics,
            deepDiagnostics ? (lanEnabled ? "System, path, services, and LAN" : "System, path, and services") : "Core native set",
            plan.IdlePingCount,
            plan.PingIntervalMs,
            plan.DownloadStages.Select(Stage).ToArray(),
            plan.UploadStages.Select(Stage).ToArray(),
            plan.DownloadStages.Sum(stage => Math.Max(1, stage.Samples)),
            plan.DownloadStages.Max(stage => stage.Connections),
            plan.UploadStages.Max(stage => stage.Connections),
            plan.DownloadStages.Count + plan.UploadStages.Count,
            lanEnabled,
            lanEnabled ? settings.LanTarget : null,
            lanEnabled ? settings.LanPort : null,
            lanEnabled ? settings.LanDurationSeconds : null,
            lanEnabled ? settings.LanConnections : null,
            lanEstimatedSeconds);
    }
}

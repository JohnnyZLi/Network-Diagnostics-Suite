using System.Diagnostics;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

public sealed record PresentedRunProgress(
    string Phase,
    string Stage,
    string StageLabel,
    string Message,
    double StageFraction,
    double OverallFraction,
    int StageIndex,
    int TotalStages,
    double ElapsedSeconds,
    double? EstimatedSecondsRemaining,
    double? LiveMbps,
    double? LiveLatencyMs,
    long StageBytesTransferred,
    long TotalBytesTransferred);

public sealed class NativeRunProgressProjector
{
    private readonly object gate = new();
    private readonly IReadOnlyList<ProgressStage> stages;
    private readonly Dictionary<string, long> stageBytes = new(StringComparer.Ordinal);
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly double estimatedSeconds;
    private double highestOverall;
    private string? lastDeepMessage;
    private int deepMessages;
    private int currentStageIndex;
    private double currentStageFraction;
    private string currentMessage = "Preparing native measurements";
    private double? currentLiveMbps;
    private double? currentLiveLatencyMs;

    public NativeRunProgressProjector(
        NativeTransferPlan plan,
        bool includeDeepDiagnostics,
        bool includeLanMeasurement,
        double estimatedSeconds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        this.estimatedSeconds = Math.Max(1, estimatedSeconds);
        var configuredStages = new List<ProgressStage>
        {
            new("preflight", "endpoint", "Measurement path", 2),
            new("idle", "baseline", "Baseline latency", Math.Max(1, plan.IdlePingCount * plan.PingIntervalMs / 1000d))
        };
        configuredStages.AddRange(plan.DownloadStages.Select(item => new ProgressStage(
            "download", item.Id, StageLabel(item), Math.Max(1, item.DurationMs / 1000d))));
        configuredStages.AddRange(plan.UploadStages.Select(item => new ProgressStage(
            "upload", item.Id, StageLabel(item), Math.Max(1, item.DurationMs / 1000d))));
        if (includeDeepDiagnostics)
        {
            var deepWeight = plan.IncludeServices ? 6d : 4d;
            if (includeLanMeasurement) deepWeight += 2d;
            configuredStages.Add(new ProgressStage("diagnostics", "deep", "System and path diagnostics", deepWeight));
        }
        configuredStages.Add(new ProgressStage("finalize", "report", "Findings and report", 1));
        stages = configuredStages;
    }

    public PresentedRunProgress Project(NativeRunProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        lock (gate)
        {
            var phase = progress.Phase;
            var stage = progress.Stage;
            var fraction = Math.Clamp(progress.Fraction, 0, 1);
            var message = progress.Message;
            var genericMessageIgnored = false;

            if (phase == "diagnostics")
            {
                if (progress.Message.StartsWith("Selecting ", StringComparison.OrdinalIgnoreCase))
                {
                    phase = "preflight";
                    stage = "endpoint";
                    fraction = 0.5;
                }
                else if (progress.Message.StartsWith("Finalizing ", StringComparison.OrdinalIgnoreCase))
                {
                    phase = "finalize";
                    stage = "report";
                    fraction = 0.55;
                }
                else
                {
                    var deepStageIndex = FindStageIndex("diagnostics", "deep");
                    var lastTransferIndex = LastTransferStageIndex();
                    if (deepStageIndex >= 0
                        && currentStageIndex >= lastTransferIndex
                        && currentStageFraction >= 0.99)
                    {
                        stage = "deep";
                        if (!string.Equals(lastDeepMessage, progress.Message, StringComparison.Ordinal))
                        {
                            lastDeepMessage = progress.Message;
                            deepMessages++;
                        }
                        fraction = Math.Min(0.92, 0.08 + deepMessages * 0.09);
                    }
                    else
                    {
                        // FullDiagnosticRunner also publishes human-readable messages on a
                        // generic diagnostics channel while structured transfer progress is
                        // active. Those callbacks can arrive out of order, so they must not
                        // advance or relabel the native stage shown by the desktop.
                        genericMessageIgnored = true;
                    }
                }
            }
            else if (phase == "complete")
            {
                var finalTransfer = stages.LastOrDefault(item => item.Phase is "download" or "upload");
                if (finalTransfer is not null)
                {
                    phase = finalTransfer.Phase;
                    stage = finalTransfer.Id;
                    fraction = 1;
                }
            }

            var index = genericMessageIgnored ? currentStageIndex : FindStageIndex(phase, stage);
            if (index < 0)
            {
                index = currentStageIndex;
                genericMessageIgnored = true;
            }

            if (genericMessageIgnored || index < currentStageIndex)
            {
                index = currentStageIndex;
                fraction = currentStageFraction;
                message = currentMessage;
                phase = stages[index].Phase;
                stage = stages[index].Id;
            }
            else
            {
                if (index == currentStageIndex)
                {
                    fraction = Math.Max(currentStageFraction, fraction);
                }

                currentStageIndex = index;
                currentStageFraction = fraction;
                currentMessage = message;
                currentLiveMbps = progress.LiveMbps ?? currentLiveMbps;
                currentLiveLatencyMs = progress.LiveLatencyMs ?? currentLiveLatencyMs;
            }

            var selected = stages[index];
            var totalWeight = stages.Sum(item => item.Weight);
            var completedWeight = stages.Take(index).Sum(item => item.Weight);
            var overall = (completedWeight + selected.Weight * fraction) / totalWeight;
            highestOverall = Math.Max(highestOverall, Math.Clamp(overall, 0, 0.995));

            if (progress.BytesTransferred > 0 && phase is "download" or "upload")
            {
                var key = $"{phase}:{stage}";
                stageBytes[key] = Math.Max(stageBytes.GetValueOrDefault(key), progress.BytesTransferred);
            }

            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var remaining = EstimateRemaining(elapsed, highestOverall);
            return new PresentedRunProgress(
                phase,
                stage,
                selected.Label,
                message,
                fraction,
                highestOverall,
                index + 1,
                stages.Count,
                elapsed,
                remaining,
                currentLiveMbps,
                currentLiveLatencyMs,
                progress.BytesTransferred,
                stageBytes.Values.Sum());
        }
    }

    public PresentedRunProgress Complete(string message = "Diagnostic complete")
    {
        lock (gate)
        {
            highestOverall = 1;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            return new PresentedRunProgress(
                "complete",
                "complete",
                "Complete",
                message,
                1,
                1,
                stages.Count,
                stages.Count,
                elapsed,
                0,
                null,
                null,
                0,
                stageBytes.Values.Sum());
        }
    }

    private int FindStageIndex(string phase, string stage)
    {
        var index = stages.ToList().FindIndex(item =>
            string.Equals(item.Phase, phase, StringComparison.Ordinal)
            && string.Equals(item.Id, stage, StringComparison.Ordinal));
        if (index >= 0) return index;
        if (phase == "diagnostics")
        {
            index = stages.ToList().FindIndex(item => item.Phase == "diagnostics");
            if (index >= 0) return index;
        }
        return -1;
    }

    private int LastTransferStageIndex()
    {
        for (var index = stages.Count - 1; index >= 0; index--)
        {
            if (stages[index].Phase is "download" or "upload") return index;
        }
        return 0;
    }

    private double? EstimateRemaining(double elapsed, double overall)
    {
        if (overall < 0.03) return estimatedSeconds;
        var observedRemaining = elapsed / overall - elapsed;
        var plannedRemaining = estimatedSeconds * (1 - overall);
        if (!double.IsFinite(observedRemaining) || observedRemaining < 0)
        {
            observedRemaining = 0;
        }
        return Math.Min(Math.Max(plannedRemaining, observedRemaining), Math.Max(estimatedSeconds * 2, 5));
    }

    private static string StageLabel(TransferStagePlan stage)
    {
        var strategy = stage.Strategy == TransferStrategy.Single ? "Single flow" : $"{stage.Connections} connections";
        var direction = stage.Direction == TransferDirection.Download ? "download" : "upload";
        return $"{strategy} {direction}";
    }

    private sealed record ProgressStage(string Phase, string Id, string Label, double Weight);
}

using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Services;

public enum ActiveRunStatus
{
    Idle,
    Preparing,
    Running,
    Cancelling,
    Completed,
    Cancelled,
    Failed
}

public sealed record ActiveRunEvent(
    DateTimeOffset Timestamp,
    string Phase,
    string Detail,
    double Progress,
    double? LiveMbps = null,
    double? LiveLatencyMs = null,
    long BytesTransferred = 0);

public sealed record ActiveRunSnapshot(
    Guid RunId,
    ActiveRunStatus Status,
    TestProfileId Profile,
    TransferMethod Method,
    DateTimeOffset? StartedAt,
    string Phase,
    string Detail,
    double Progress,
    double? LiveMbps = null,
    double? LiveLatencyMs = null,
    long BytesTransferred = 0,
    Guid? ReportId = null,
    string? ErrorMessage = null,
    NetworkDiagnosticsReportV2? Report = null)
{
    public bool IsActive => Status is ActiveRunStatus.Preparing
        or ActiveRunStatus.Running
        or ActiveRunStatus.Cancelling;

    public bool HasTerminalResult => Status is ActiveRunStatus.Completed
        or ActiveRunStatus.Cancelled
        or ActiveRunStatus.Failed;

    public static ActiveRunSnapshot Empty { get; } = new(
        Guid.Empty,
        ActiveRunStatus.Idle,
        TestProfileId.ConnectionCheck,
        TransferMethod.Compare,
        null,
        "Idle",
        "No diagnostic is running.",
        0);
}

public sealed class ActiveRunSession : IDisposable
{
    private const int MaximumEvents = 60;
    private readonly List<ActiveRunEvent> events = [];
    private CancellationTokenSource? cancellation;

    public event EventHandler? Changed;

    public ActiveRunSnapshot Snapshot { get; private set; } = ActiveRunSnapshot.Empty;

    public IReadOnlyList<ActiveRunEvent> Events => events;

    public CancellationToken CancellationToken => cancellation?.Token ?? CancellationToken.None;

    internal CancellationTokenSource? CancellationSource => cancellation;

    public Guid Start(TestProfileId profile, TransferMethod method)
    {
        if (Snapshot.IsActive)
        {
            throw new InvalidOperationException("A diagnostic is already running.");
        }

        DisposeCancellation();
        cancellation = new CancellationTokenSource();
        events.Clear();
        var runId = Guid.NewGuid();
        Snapshot = new ActiveRunSnapshot(
            runId,
            ActiveRunStatus.Preparing,
            profile,
            method,
            DateTimeOffset.UtcNow,
            "Preparing",
            "Preparing the diagnostic…",
            0);
        AppendEvent(Snapshot.Phase, Snapshot.Detail, Snapshot.Progress);
        RaiseChanged();
        return runId;
    }

    public void UpdateProgress(
        string phase,
        string detail,
        double progress,
        double? liveMbps = null,
        double? liveLatencyMs = null,
        long bytesTransferred = 0)
    {
        if (!Snapshot.IsActive || Snapshot.Status == ActiveRunStatus.Cancelling)
        {
            return;
        }

        var nextPhase = string.IsNullOrWhiteSpace(phase) ? Snapshot.Phase : phase.Trim();
        var nextDetail = string.IsNullOrWhiteSpace(detail) ? Snapshot.Detail : detail.Trim();
        var nextProgress = Math.Clamp(Math.Max(Snapshot.Progress, progress), 0, 100);
        var shouldAppend = events.Count == 0
            || !string.Equals(events[^1].Phase, nextPhase, StringComparison.Ordinal)
            || !string.Equals(events[^1].Detail, nextDetail, StringComparison.Ordinal)
            || nextProgress - events[^1].Progress >= 5;

        Snapshot = Snapshot with
        {
            Status = ActiveRunStatus.Running,
            Phase = nextPhase,
            Detail = nextDetail,
            Progress = nextProgress,
            LiveMbps = liveMbps,
            LiveLatencyMs = liveLatencyMs,
            BytesTransferred = Math.Max(Snapshot.BytesTransferred, bytesTransferred)
        };
        if (shouldAppend)
        {
            AppendEvent(nextPhase, nextDetail, nextProgress, liveMbps, liveLatencyMs, Snapshot.BytesTransferred);
        }
        RaiseChanged();
    }

    public bool RequestCancel()
    {
        if (!Snapshot.IsActive || Snapshot.Status == ActiveRunStatus.Cancelling)
        {
            return false;
        }

        Snapshot = Snapshot with
        {
            Status = ActiveRunStatus.Cancelling,
            Phase = "Cancelling",
            Detail = "Stopping after the current operation…"
        };
        AppendEvent(Snapshot.Phase, Snapshot.Detail, Snapshot.Progress, Snapshot.LiveMbps, Snapshot.LiveLatencyMs, Snapshot.BytesTransferred);
        cancellation?.Cancel();
        RaiseChanged();
        return true;
    }

    public void Complete(NetworkDiagnosticsReportV2 report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Snapshot = Snapshot with
        {
            Status = ActiveRunStatus.Completed,
            Phase = "Complete",
            Detail = "Diagnostic completed and saved.",
            Progress = 100,
            ReportId = report.Run.Id,
            Report = report,
            ErrorMessage = null
        };
        AppendEvent(Snapshot.Phase, Snapshot.Detail, Snapshot.Progress, Snapshot.LiveMbps, Snapshot.LiveLatencyMs, Snapshot.BytesTransferred);
        DisposeCancellation();
        RaiseChanged();
    }

    public void MarkCancelled()
    {
        Snapshot = Snapshot with
        {
            Status = ActiveRunStatus.Cancelled,
            Phase = "Cancelled",
            Detail = "The diagnostic was cancelled.",
            ErrorMessage = null
        };
        AppendEvent(Snapshot.Phase, Snapshot.Detail, Snapshot.Progress, Snapshot.LiveMbps, Snapshot.LiveLatencyMs, Snapshot.BytesTransferred);
        DisposeCancellation();
        RaiseChanged();
    }

    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Snapshot = Snapshot with
        {
            Status = ActiveRunStatus.Failed,
            Phase = "Failed",
            Detail = "The diagnostic did not complete.",
            ErrorMessage = error.Message
        };
        AppendEvent(Snapshot.Phase, error.Message, Snapshot.Progress, Snapshot.LiveMbps, Snapshot.LiveLatencyMs, Snapshot.BytesTransferred);
        DisposeCancellation();
        RaiseChanged();
    }

    public void Reset()
    {
        if (Snapshot.IsActive)
        {
            throw new InvalidOperationException("An active diagnostic must be cancelled before resetting the session.");
        }

        DisposeCancellation();
        events.Clear();
        Snapshot = ActiveRunSnapshot.Empty;
        RaiseChanged();
    }

    public void Dispose()
    {
        cancellation?.Cancel();
        DisposeCancellation();
    }

    private void AppendEvent(
        string phase,
        string detail,
        double progress,
        double? liveMbps = null,
        double? liveLatencyMs = null,
        long bytesTransferred = 0)
    {
        events.Add(new ActiveRunEvent(
            DateTimeOffset.UtcNow,
            phase,
            detail,
            progress,
            liveMbps,
            liveLatencyMs,
            bytesTransferred));
        if (events.Count > MaximumEvents)
        {
            events.RemoveRange(0, events.Count - MaximumEvents);
        }
    }

    private void DisposeCancellation()
    {
        cancellation?.Dispose();
        cancellation = null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

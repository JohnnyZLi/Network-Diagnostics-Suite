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
    private CancellationTokenSource? cancellation;

    public event EventHandler? Changed;

    public ActiveRunSnapshot Snapshot { get; private set; } = ActiveRunSnapshot.Empty;

    public CancellationToken CancellationToken => cancellation?.Token ?? CancellationToken.None;

    public Guid Start(TestProfileId profile, TransferMethod method)
    {
        if (Snapshot.IsActive)
        {
            throw new InvalidOperationException("A diagnostic is already running.");
        }

        DisposeCancellation();
        cancellation = new CancellationTokenSource();
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

        Snapshot = Snapshot with
        {
            Status = ActiveRunStatus.Running,
            Phase = string.IsNullOrWhiteSpace(phase) ? Snapshot.Phase : phase.Trim(),
            Detail = string.IsNullOrWhiteSpace(detail) ? Snapshot.Detail : detail.Trim(),
            Progress = Math.Clamp(Math.Max(Snapshot.Progress, progress), 0, 100),
            LiveMbps = liveMbps,
            LiveLatencyMs = liveLatencyMs,
            BytesTransferred = Math.Max(Snapshot.BytesTransferred, bytesTransferred)
        };
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
        Snapshot = ActiveRunSnapshot.Empty;
        RaiseChanged();
    }

    public void Dispose()
    {
        cancellation?.Cancel();
        DisposeCancellation();
    }

    private void DisposeCancellation()
    {
        cancellation?.Dispose();
        cancellation = null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

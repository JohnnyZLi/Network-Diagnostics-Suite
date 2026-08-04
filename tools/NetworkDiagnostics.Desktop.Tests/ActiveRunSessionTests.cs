using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Services;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class ActiveRunSessionTests
{
    [Fact]
    public void StartCreatesCancellableApplicationLevelRun()
    {
        using var session = new ActiveRunSession();

        var runId = session.Start(TestProfileId.Standard, TransferMethod.Compare);

        Assert.NotEqual(Guid.Empty, runId);
        Assert.Equal(runId, session.Snapshot.RunId);
        Assert.Equal(ActiveRunStatus.Preparing, session.Snapshot.Status);
        Assert.Equal(TestProfileId.Standard, session.Snapshot.Profile);
        Assert.Equal(TransferMethod.Compare, session.Snapshot.Method);
        Assert.True(session.Snapshot.IsActive);
        Assert.True(session.CancellationToken.CanBeCanceled);
    }

    [Fact]
    public void ProgressIsMonotonicAndRetainsLiveEvidence()
    {
        using var session = new ActiveRunSession();
        session.Start(TestProfileId.Quick, TransferMethod.Aggregate);

        session.UpdateProgress("download", "81 Mbps", 42, 81, 24, 8_000_000);
        session.UpdateProgress("download", "79 Mbps", 39, 79, 26, 7_000_000);

        Assert.Equal(ActiveRunStatus.Running, session.Snapshot.Status);
        Assert.Equal(42, session.Snapshot.Progress);
        Assert.Equal(79, session.Snapshot.LiveMbps);
        Assert.Equal(26, session.Snapshot.LiveLatencyMs);
        Assert.Equal(8_000_000, session.Snapshot.BytesTransferred);
    }

    [Fact]
    public void CancelBelongsToSessionAndSignalsItsToken()
    {
        using var session = new ActiveRunSession();
        session.Start(TestProfileId.Extended, TransferMethod.Single);
        var token = session.CancellationToken;

        var requested = session.RequestCancel();

        Assert.True(requested);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(ActiveRunStatus.Cancelling, session.Snapshot.Status);
        Assert.True(session.Snapshot.IsActive);
    }

    [Fact]
    public void ConcurrentRunIsRejected()
    {
        using var session = new ActiveRunSession();
        session.Start(TestProfileId.ConnectionCheck, TransferMethod.Compare);

        Assert.Throws<InvalidOperationException>(() =>
            session.Start(TestProfileId.Quick, TransferMethod.Single));
    }

    [Fact]
    public void CancelledSessionCanBeResetAndStartedAgain()
    {
        using var session = new ActiveRunSession();
        session.Start(TestProfileId.ConnectionCheck, TransferMethod.Compare);
        session.MarkCancelled();

        Assert.Equal(ActiveRunStatus.Cancelled, session.Snapshot.Status);
        Assert.False(session.Snapshot.IsActive);

        session.Reset();
        var secondRun = session.Start(TestProfileId.Quick, TransferMethod.Aggregate);

        Assert.NotEqual(Guid.Empty, secondRun);
        Assert.Equal(ActiveRunStatus.Preparing, session.Snapshot.Status);
    }
}

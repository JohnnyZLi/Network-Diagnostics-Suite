using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class DesktopRuntimeContractTests
{
    [Fact]
    public void PresentedPlanIncludesLanOnlyForDeepProfiles()
    {
        var settings = PhotinoAppSettings.Default with
        {
            LanTarget = "192.168.1.20",
            LanDurationSeconds = 8,
            LanConnections = 4
        };

        var quick = DiagnosticRunPlanPresenter.Build(
            settings,
            TestProfileId.Quick,
            TransferMethod.Compare,
            DownloadPathPreference.Automatic);
        var full = DiagnosticRunPlanPresenter.Build(
            settings,
            TestProfileId.Standard,
            TransferMethod.Compare,
            DownloadPathPreference.DirectR2);

        Assert.False(quick.LanEnabled);
        Assert.Null(quick.LanTarget);
        Assert.Equal(quick.InternetEstimatedSeconds, quick.EstimatedSeconds);
        Assert.True(full.LanEnabled);
        Assert.Equal("192.168.1.20", full.LanTarget);
        Assert.Equal(18, full.LanEstimatedSeconds);
        Assert.Equal(full.InternetEstimatedSeconds + 18, full.EstimatedSeconds);
        Assert.Equal("direct-r2", full.DownloadPath);
        Assert.Equal(6, full.ServiceCheckCount);
    }

    [Fact]
    public void ProgressProjectionIsStructuredMonotonicAndCountsStageBytes()
    {
        var plan = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.ConnectionCheck, TransferMethod.Single);
        var projector = new NativeRunProgressProjector(plan, false, false, plan.EstimatedSeconds);

        var preflight = projector.Project(new NativeRunProgress(
            "diagnostics", "diagnostics", "Selecting the measurement endpoint and reading network metadata", 0, null, null, 0));
        var idle = projector.Project(new NativeRunProgress("idle", "baseline", "Baseline", 0.5, null, 18, 0));
        var download = projector.Project(new NativeRunProgress(
            "download", plan.DownloadStages[0].Id, "Download", 0.5, 120, 32, 5_000_000));
        var upload = projector.Project(new NativeRunProgress(
            "upload", plan.UploadStages[0].Id, "Upload", 0.25, 28, 41, 2_000_000));
        var complete = projector.Complete();

        Assert.Equal("preflight", preflight.Phase);
        Assert.Equal("Measurement path", preflight.StageLabel);
        Assert.True(idle.OverallFraction > preflight.OverallFraction);
        Assert.True(download.OverallFraction > idle.OverallFraction);
        Assert.True(upload.OverallFraction > download.OverallFraction);
        Assert.Equal(7_000_000, upload.TotalBytesTransferred);
        Assert.Equal(1, complete.OverallFraction);
        Assert.Equal(complete.TotalStages, complete.StageIndex);
    }

    [Fact]
    public void GenericDiagnosticMessagesCannotAdvanceOrRegressStructuredProgress()
    {
        var plan = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.ConnectionCheck, TransferMethod.Compare);
        var projector = new NativeRunProgressProjector(plan, false, false, plan.EstimatedSeconds);

        var preflight = projector.Project(new NativeRunProgress(
            "diagnostics", "diagnostics", "Selecting the measurement endpoint and reading network metadata", 0, null, null, 0));
        var genericBaseline = projector.Project(new NativeRunProgress(
            "diagnostics", "diagnostics", "Measuring first-party HTTP latency", 0, null, null, 0));
        var baseline = projector.Project(new NativeRunProgress(
            "idle", "baseline", "Measuring first-party HTTP latency", 0.25, null, 24, 0));
        var delayedGeneric = projector.Project(new NativeRunProgress(
            "diagnostics", "diagnostics", "Measuring first-party HTTP latency", 0, null, null, 0));

        Assert.Equal("preflight", genericBaseline.Phase);
        Assert.Equal(preflight.StageIndex, genericBaseline.StageIndex);
        Assert.Equal("idle", baseline.Phase);
        Assert.Equal("Baseline latency", baseline.StageLabel);
        Assert.InRange(baseline.OverallFraction, 0.1, 0.4);
        Assert.Equal(baseline.StageIndex, delayedGeneric.StageIndex);
        Assert.Equal(baseline.OverallFraction, delayedGeneric.OverallFraction);
        Assert.True(baseline.EstimatedSecondsRemaining > 5);
    }

    [Fact]
    public void SingleInstanceLockIsReleasedOnDispose()
    {
        var directory = Path.Combine(Path.GetTempPath(), "network-diagnostics-instance-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var first = DesktopSingleInstance.TryAcquire(directory);
            Assert.NotNull(first);
            using var second = DesktopSingleInstance.TryAcquire(directory);
            Assert.Null(second);
            first.Dispose();
            using var third = DesktopSingleInstance.TryAcquire(directory);
            Assert.NotNull(third);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopLifetimeClosesOnlyOnce()
    {
        var closeCalls = 0;
        using var lifetime = new DesktopLifetime(() => Interlocked.Increment(ref closeCalls));
        lifetime.RequestShutdown();
        lifetime.RequestShutdown();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(lifetime.ShutdownRequested);
        Assert.Equal(1, Volatile.Read(ref closeCalls));
    }
}

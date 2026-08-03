using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Tests;

public sealed class NativeTransferPlanTests
{
    [Fact]
    public void ConnectionCheckUsesTheLightweightCap()
    {
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.ConnectionCheck, TransferMethod.Compare);

        Assert.Equal(28_000_000, plan.TransferCapBytes);
        Assert.Equal(15, plan.EstimatedSeconds);
        Assert.Equal([1, 2], plan.DownloadStages.Select(stage => stage.Connections));
        Assert.Equal([2], plan.UploadStages.Select(stage => stage.Connections));
        Assert.Equal(1, plan.DownloadStages[1].Samples);
    }

    [Fact]
    public void QuickComparePreservesTheOriginalBrowserPlan()
    {
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.Quick, TransferMethod.Compare);

        Assert.Equal(728_000_000, plan.TransferCapBytes);
        Assert.Equal(25, plan.EstimatedSeconds);
        Assert.Equal([1, 6], plan.DownloadStages.Select(stage => stage.Connections));
        Assert.Equal([6], plan.UploadStages.Select(stage => stage.Connections));
        Assert.Equal(3, plan.DownloadStages[1].Samples);
    }

    [Fact]
    public void FullCompareSeparatesBothDirections()
    {
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.Standard, TransferMethod.Compare);

        Assert.Equal(1_156_000_000, plan.TransferCapBytes);
        Assert.Equal(50, plan.EstimatedSeconds);
        Assert.Equal([1, 8], plan.DownloadStages.Select(stage => stage.Connections));
        Assert.Equal([1, 8], plan.UploadStages.Select(stage => stage.Connections));
    }

    [Fact]
    public void StressCompareUsesTheApprovedScalingSequence()
    {
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.Extended, TransferMethod.Compare);

        Assert.Equal(3_512_000_000, plan.TransferCapBytes);
        Assert.Equal(65, plan.EstimatedSeconds);
        Assert.Equal([1, 2, 4, 8, 10], plan.DownloadStages.Select(stage => stage.Connections));
        Assert.Equal([1, 8], plan.UploadStages.Select(stage => stage.Connections));
    }

    [Theory]
    [InlineData(TestProfileId.ConnectionCheck)]
    [InlineData(TestProfileId.Quick)]
    [InlineData(TestProfileId.Standard)]
    [InlineData(TestProfileId.Extended)]
    public void MethodOverridesPreserveTheProfileCap(TestProfileId profile)
    {
        var compare = NativeTransferPlanBuilder.Build(profile, TransferMethod.Compare);
        var single = NativeTransferPlanBuilder.Build(profile, TransferMethod.Single);
        var aggregate = NativeTransferPlanBuilder.Build(profile, TransferMethod.Aggregate);

        Assert.Equal(compare.TransferCapBytes, single.TransferCapBytes);
        Assert.Equal(compare.TransferCapBytes, aggregate.TransferCapBytes);
        Assert.All(single.DownloadStages.Concat(single.UploadStages), stage => Assert.Equal(1, stage.Connections));
    }
}

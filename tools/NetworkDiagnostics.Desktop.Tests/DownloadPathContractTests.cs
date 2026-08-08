using System.Text.Json;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class DownloadPathContractTests
{
    [Theory]
    [InlineData("automatic", DownloadPathPreference.Automatic, "automatic")]
    [InlineData("direct-r2", DownloadPathPreference.DirectR2, "direct-r2")]
    [InlineData("r2-direct", DownloadPathPreference.DirectR2, "direct-r2")]
    [InlineData("worker", DownloadPathPreference.Worker, "worker")]
    [InlineData("worker-stream", DownloadPathPreference.Worker, "worker")]
    public void BridgeDownloadPathContractMapsUiIds(
        string contractId,
        DownloadPathPreference expected,
        string normalizedId)
    {
        using var document = JsonDocument.Parse($$"""{"downloadPath":"{{contractId}}"}""");
        var path = BridgeProtocol.ParseDownloadPath(document.RootElement);
        Assert.Equal(expected, path);
        Assert.Equal(normalizedId, BridgeProtocol.DownloadPathId(path));
    }

    [Fact]
    public void MissingDownloadPathDefaultsToAutomatic()
    {
        using var document = JsonDocument.Parse("{}");
        Assert.Equal(DownloadPathPreference.Automatic, BridgeProtocol.ParseDownloadPath(document.RootElement));
        Assert.Equal(DownloadPathPreference.Automatic, new NativeDiagnosticRunOptions().DownloadPath);
    }

    [Theory]
    [InlineData("auto", DownloadPathPreference.Automatic)]
    [InlineData("r2", DownloadPathPreference.DirectR2)]
    [InlineData("worker", DownloadPathPreference.Worker)]
    public void NativePlanParserAcceptsDownloadPathAliases(string value, DownloadPathPreference expected)
    {
        Assert.Equal(expected, NativeTransferPlanBuilder.ParseDownloadPath(value));
    }

    [Fact]
    public void RichPlanExposesActualStageConnectionsSamplesAndLimits()
    {
        var quick = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.Quick, TransferMethod.Compare);
        Assert.Equal(728_000_000, quick.TransferCapBytes);
        Assert.Equal([1, 6], quick.DownloadStages.Select(stage => stage.Connections));
        Assert.Equal([6], quick.UploadStages.Select(stage => stage.Connections));
        Assert.Equal(4, quick.DownloadStages.Sum(stage => Math.Max(1, stage.Samples)));
        Assert.False(quick.IncludeServices);

        var full = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.Standard, TransferMethod.Compare);
        Assert.True(full.IncludeServices);
        Assert.Contains(full.DownloadStages, stage => stage.Connections == 8);
        Assert.Contains(full.UploadStages, stage => stage.Connections == 8);

        var stress = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.Extended, TransferMethod.Compare);
        Assert.Equal([1, 2, 4, 8, 10], stress.DownloadStages.Select(stage => stage.Connections));
        Assert.True(stress.TransferCapBytes > 3_000_000_000);
    }
}

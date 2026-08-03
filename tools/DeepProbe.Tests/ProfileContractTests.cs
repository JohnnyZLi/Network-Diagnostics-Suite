using NetworkDeepProbe.Contracts;
using Xunit;

namespace NetworkDeepProbe.Tests;

public sealed class ProfileContractTests
{
    [Fact]
    public void EmbeddedContractLoadsTheApprovedDesktopProfiles()
    {
        var contract = TestProfileContract.Load();

        Assert.Equal("1.1", contract.SchemaVersion);
        Assert.Equal(["compare", "single", "aggregate"], contract.TransferMethods);
        Assert.Equal(
            ["Connection Check", "Quick", "Full", "Stress"],
            contract.Profiles.Select(profile => profile.Name));

        var connectionCheck = Assert.Single(contract.Profiles, profile => profile.Id == "connection-check");
        Assert.Equal(15, connectionCheck.EstimatedSeconds);
        Assert.Equal(28_000_000, connectionCheck.DownloadCapBytes + connectionCheck.UploadCapBytes);
        Assert.Equal(2, connectionCheck.AggregateDownloadConnections);

        var quick = Assert.Single(contract.Profiles, profile => profile.Id == "quick");
        Assert.Equal(728_000_000, quick.DownloadCapBytes + quick.UploadCapBytes);
        Assert.Equal(6, quick.AggregateDownloadConnections);

        var stress = Assert.Single(contract.Profiles, profile => profile.Id == "extended");
        Assert.Equal(3_512_000_000, stress.DownloadCapBytes + stress.UploadCapBytes);
        Assert.Equal([1, 2, 4, 8, 10], stress.DownloadScaling.Select(stage => stage.Connections));
        Assert.Equal(8, stress.AggregateUploadConnections);
    }
}

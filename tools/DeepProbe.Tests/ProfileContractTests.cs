using NetworkDeepProbe.Contracts;
using Xunit;

namespace NetworkDeepProbe.Tests;

public sealed class ProfileContractTests
{
    [Fact]
    public void EmbeddedContractLoadsTheApprovedProfiles()
    {
        var contract = TestProfileContract.Load();

        Assert.Equal("1.1", contract.SchemaVersion);
        Assert.Equal(["compare", "single", "aggregate"], contract.TransferMethods);
        Assert.Equal(["Connection Check", "Full", "Stress"], contract.Profiles.Select(profile => profile.Name));

        var connection = Assert.Single(contract.Profiles, profile => profile.Id == "quick");
        Assert.Equal(15, connection.EstimatedSeconds);
        Assert.Equal(28_000_000, connection.DownloadCapBytes + connection.UploadCapBytes);
        Assert.Equal(2, connection.AggregateDownloadConnections);

        var stress = Assert.Single(contract.Profiles, profile => profile.Id == "extended");
        Assert.Equal(3_512_000_000, stress.DownloadCapBytes + stress.UploadCapBytes);
        Assert.Equal([1, 2, 4, 8, 10], stress.DownloadScaling.Select(stage => stage.Connections));
        Assert.Equal(8, stress.AggregateUploadConnections);
    }
}

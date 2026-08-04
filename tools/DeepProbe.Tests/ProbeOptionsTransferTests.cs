using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Tests;

public sealed class ProbeOptionsTransferTests
{
    [Fact]
    public void InternetTransferIsOptInForCliCompatibility()
    {
        var options = ProbeOptions.Parse([]);

        Assert.False(options.IncludeInternetTransfer);
        Assert.Equal(TestProfileId.ConnectionCheck, options.Profile);
        Assert.Equal(TransferMethod.Compare, options.TransferMethod);
        Assert.Equal(InternetTransferProbe.DefaultOrigin, options.TestOrigin);
    }

    [Theory]
    [InlineData("connection-check", TestProfileId.ConnectionCheck)]
    [InlineData("quick", TestProfileId.Quick)]
    [InlineData("full", TestProfileId.Standard)]
    [InlineData("stress", TestProfileId.Extended)]
    public void ParsesAllNativeProfiles(string value, TestProfileId expected)
    {
        var options = ProbeOptions.Parse(["--internet-transfer", "--profile", value]);

        Assert.True(options.IncludeInternetTransfer);
        Assert.Equal(expected, options.Profile);
    }

    [Fact]
    public void ParsesProfileMethodAndOrigin()
    {
        var options = ProbeOptions.Parse([
            "--internet-transfer",
            "--profile", "stress",
            "--transfer-method", "aggregate",
            "--test-origin", "http://127.0.0.1:8787"
        ]);

        Assert.True(options.IncludeInternetTransfer);
        Assert.Equal(TestProfileId.Extended, options.Profile);
        Assert.Equal(TransferMethod.Aggregate, options.TransferMethod);
        Assert.Equal(new Uri("http://127.0.0.1:8787/"), options.TestOrigin);
    }

    [Fact]
    public void ParsesRepeatableOriginsAndInterfaceSelection()
    {
        var options = ProbeOptions.Parse([
            "--internet-transfer",
            "--test-origin", "https://first.example",
            "--test-origin", "https://second.example/path",
            "--interface", "en0"
        ]);

        Assert.Equal("en0", options.InterfaceId);
        Assert.Equal(2, options.CandidateOrigins.Count);
        Assert.Equal(new Uri("https://first.example/"), options.CandidateOrigins[0]);
        Assert.Equal(new Uri("https://second.example/path/"), options.CandidateOrigins[1]);
    }

    [Fact]
    public void RejectsMoreThanEightEndpointCandidates()
    {
        var arguments = Enumerable.Range(1, 9)
            .SelectMany(index => new[] { "--test-origin", $"https://endpoint-{index}.example" })
            .ToArray();

        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(arguments));
    }

    [Fact]
    public void RejectsServerAndInternetRunnerCombination()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["--lan-server", "--internet-transfer"]));
    }
}

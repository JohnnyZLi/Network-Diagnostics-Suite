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
    public void RejectsServerAndInternetRunnerCombination()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["--lan-server", "--internet-transfer"]));
    }
}

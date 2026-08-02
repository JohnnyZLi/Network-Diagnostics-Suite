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
        Assert.Equal(TestProfileId.Quick, options.Profile);
        Assert.Equal(TransferMethod.Compare, options.TransferMethod);
        Assert.Equal(InternetTransferProbe.DefaultOrigin, options.TestOrigin);
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

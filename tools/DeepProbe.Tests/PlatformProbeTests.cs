using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class PlatformProbeTests
{
    [Fact]
    public void InterfaceCollectionUsesPrivacyPreservingDefaultsOnTheCurrentPlatform()
    {
        var result = InterfaceProbe.Collect(includeAddresses: false);

        Assert.All(result.Reports, report =>
        {
            Assert.Null(report.UnicastAddresses);
            Assert.Null(report.Gateways);
            Assert.Null(report.DnsServers);
        });
    }
}

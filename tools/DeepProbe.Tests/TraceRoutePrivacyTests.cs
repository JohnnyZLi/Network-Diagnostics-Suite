using System.Net;
using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class TraceRoutePrivacyTests
{
    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("172.16.10.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.2.4")]
    [InlineData("127.0.0.1")]
    [InlineData("fe80::1")]
    [InlineData("fd12:3456::1")]
    public void PrivateAndLocalAddressesAreDetected(string value)
    {
        Assert.True(TraceRouteProbe.IsLocalOrPrivate(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddressesAreNotRedacted(string value)
    {
        Assert.False(TraceRouteProbe.IsLocalOrPrivate(IPAddress.Parse(value)));
    }
}

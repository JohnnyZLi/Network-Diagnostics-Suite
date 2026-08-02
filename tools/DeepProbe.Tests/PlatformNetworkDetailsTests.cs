using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class PlatformNetworkDetailsTests
{
    [Fact]
    public void ParsesWindowsWifiWithoutLeakingSsidByDefault()
    {
        const string output = """
            Name                   : Wi-Fi
            State                  : connected
            SSID                   : Lab Network
            Radio type             : 802.11ax
            Authentication         : WPA2-Personal
            Channel                : 149
            Receive rate (Mbps)    : 1201
            Transmit rate (Mbps)   : 960
            Signal                 : 82%
            """;

        var hidden = PlatformNetworkDetailsProbe.ParseWindowsWifi(output, false);
        var included = PlatformNetworkDetailsProbe.ParseWindowsWifi(output, true);

        Assert.Equal("available", hidden.Status);
        Assert.Null(hidden.Ssid);
        Assert.Equal("Lab Network", included.Ssid);
        Assert.Equal(82, hidden.SignalPercent);
        Assert.Equal(149, hidden.Channel);
        Assert.Equal("5 GHz", hidden.Band);
        Assert.Equal(1201, hidden.ReceiveRateMbps);
    }

    [Fact]
    public void ParsesLinuxWifiLink()
    {
        const string output = """
            Connected to 00:11:22:33:44:55 (on wlan0)
                SSID: Example
                freq: 6115
                signal: -48 dBm
                rx bitrate: 2401.9 MBit/s
                tx bitrate: 1921.5 MBit/s
            """;

        var report = PlatformNetworkDetailsProbe.ParseLinuxWifi(output, "wlan0", false);

        Assert.Equal("available", report.Status);
        Assert.Null(report.Ssid);
        Assert.Equal(-48, report.RssiDbm);
        Assert.Equal(104, report.SignalPercent);
        Assert.Equal("6 GHz", report.Band);
        Assert.Equal(2402, report.ReceiveRateMbps);
    }

    [Fact]
    public void ParsesLinuxDefaultRouteAndRedactsGateway()
    {
        const string output = """
            default via 192.168.1.1 dev wlan0 proto dhcp metric 600
            192.168.1.0/24 dev wlan0 proto kernel scope link src 192.168.1.20 metric 600
            """;

        var hidden = PlatformNetworkDetailsProbe.ParseLinuxRoutes(output, false);
        var included = PlatformNetworkDetailsProbe.ParseLinuxRoutes(output, true);

        var defaultHidden = Assert.Single(hidden.Entries, entry => entry.IsDefault);
        var defaultIncluded = Assert.Single(included.Entries, entry => entry.IsDefault);
        Assert.Null(defaultHidden.Gateway);
        Assert.Equal("192.168.1.1", defaultIncluded.Gateway);
        Assert.Equal("wlan0", defaultHidden.InterfaceName);
        Assert.Equal(600, defaultHidden.Metric);
    }

    [Fact]
    public void ParsesWindowsRouteTable()
    {
        const string output = """
            IPv4 Route Table
            Active Routes:
            Network Destination        Netmask          Gateway       Interface  Metric
                      0.0.0.0          0.0.0.0      10.0.0.1       10.0.0.50     25
                     10.0.0.0    255.255.255.0         On-link       10.0.0.50    281
            """;

        var report = PlatformNetworkDetailsProbe.ParseWindowsRoutes(output, true);

        Assert.Equal(2, report.Entries.Count);
        Assert.True(report.Entries[0].IsDefault);
        Assert.Equal("0.0.0.0/0", report.Entries[0].Destination);
        Assert.Equal("10.0.0.1", report.Entries[0].Gateway);
    }
}

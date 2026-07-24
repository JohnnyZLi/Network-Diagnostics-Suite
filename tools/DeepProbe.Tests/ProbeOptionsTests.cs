using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class ProbeOptionsTests
{
    [Fact]
    public void ParseUsesPrivacyPreservingDefaults()
    {
        var options = ProbeOptions.Parse([], new DateTimeOffset(2026, 7, 19, 10, 11, 12, TimeSpan.Zero));

        Assert.Equal("1.1.1.1", options.Target);
        Assert.Equal(20, options.PingCount);
        Assert.False(options.IncludeAddresses);
        Assert.Null(options.LanTarget);
        Assert.False(options.LanServer);
        Assert.Equal(8765, options.LanPort);
        Assert.Equal(8, options.LanDurationSeconds);
        Assert.Equal(4, options.LanConcurrency);
        Assert.Equal("network-report-20260719-101112.json", options.OutputPath);
    }

    [Fact]
    public void ParseAcceptsExplicitDiagnosticControls()
    {
        var options = ProbeOptions.Parse([
            "--target", "example.com",
            "--output", "report.json",
            "--pings", "40",
            "--max-hops", "48",
            "--include-addresses",
            "--lan-target", "192.168.1.10",
            "--lan-port", "9876",
            "--lan-duration", "12",
            "--lan-streams", "6"
        ]);

        Assert.Equal("example.com", options.Target);
        Assert.Equal("report.json", options.OutputPath);
        Assert.Equal(40, options.PingCount);
        Assert.Equal(48, options.MaximumHops);
        Assert.True(options.IncludeAddresses);
        Assert.Equal("192.168.1.10", options.LanTarget);
        Assert.Equal(9876, options.LanPort);
        Assert.Equal(12, options.LanDurationSeconds);
        Assert.Equal(6, options.LanConcurrency);
    }


    [Fact]
    public void ParseAcceptsLanServerMode()
    {
        var options = ProbeOptions.Parse(["--lan-server", "--lan-port", "9000"]);

        Assert.True(options.LanServer);
        Assert.Equal(9000, options.LanPort);
    }

    [Fact]
    public void ParseRejectsLanServerAndClientTogether()
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse(["--lan-server", "--lan-target", "192.168.1.10"]));
    }

    [Theory]
    [InlineData("--pings", "4")]
    [InlineData("--pings", "101")]
    [InlineData("--max-hops", "2")]
    [InlineData("--lan-port", "80")]
    [InlineData("--lan-duration", "31")]
    [InlineData("--lan-streams", "0")]
    public void ParseRejectsUnsafeBounds(string option, string value)
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse([option, value]));
    }
}

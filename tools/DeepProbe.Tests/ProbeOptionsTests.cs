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
            "--include-addresses"
        ]);

        Assert.Equal("example.com", options.Target);
        Assert.Equal("report.json", options.OutputPath);
        Assert.Equal(40, options.PingCount);
        Assert.Equal(48, options.MaximumHops);
        Assert.True(options.IncludeAddresses);
    }

    [Theory]
    [InlineData("--pings", "4")]
    [InlineData("--pings", "101")]
    [InlineData("--max-hops", "2")]
    public void ParseRejectsUnsafeBounds(string option, string value)
    {
        Assert.Throws<ArgumentException>(() => ProbeOptions.Parse([option, value]));
    }
}

using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Tests;

public sealed class AdvancedEvidenceTests
{
    [Fact]
    public void AdvancedEvidenceRoundTripsAsOptionalSchemaTwoFields()
    {
        var now = new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero);
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.Standard, TransferMethod.Compare);
        var emptyStatistics = EmptyStatistics();
        var report = new NetworkDiagnosticsReportV2(
            "2.0",
            now,
            new DiagnosticRunMetadata(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "test",
                "x64",
                TestProfileId.Standard,
                TransferMethod.Compare,
                now,
                now,
                false),
            NativeTransferPlanReport.FromPlan(plan),
            null,
            null,
            null,
            Producer: new ReportProducer("desktop", "1.0.0", "network-diagnostics-native"),
            LoadLocalization: new LoadedPathLocalizationReport(
                "measured",
                [new LoadedPathTargetReport("gateway", "Default gateway", null, emptyStatistics, emptyStatistics, emptyStatistics)],
                "local-network",
                "Latency rose at the gateway."),
            DualStack: new DualStackReport(
                new AddressFamilyProbeReport(
                    "IPv4", true, "203.0.113.10", true, 12, true, 8, null,
                    true, 10, "Tls13", "http/1.1", true, 18, 204),
                new AddressFamilyProbeReport(
                    "IPv6", true, "2001:db8::10", true, 14, true, 9, null,
                    true, 11, "Tls13", "http/1.1", true, 20, 204),
                "IPv4",
                false,
                "measured",
                4,
                1,
                1,
                "IPv4",
                1),
            NetworkChange: new NetworkChangeReport(
                new NetworkStateSnapshot("if0", "Interface 1", null, ["IPv4", "IPv6"], null, []),
                new NetworkStateSnapshot("if0", "Interface 1", null, ["IPv4", "IPv6"], null, []),
                false,
                [],
                false,
                new NetworkMetadataReport("LAX", "ExampleNet", 64500, "h2", "TLSv1.3", "IPv4"),
                new NetworkMetadataReport("LAX", "ExampleNet", 64500, "h2", "TLSv1.3", "IPv4"),
                false),
            HostResources: new HostResourceReport(
                12.5, 100_000_000, 10_000_000, 12_000_000, [], false,
                1_000, 5, 0.5, 4_000_000_000, 8_000_000_000),
            Annotations: new ReportAnnotations("Before router restart", ["Wi-Fi", "VPN off"]));

        var parsed = NetworkDiagnosticsJson.Deserialize(NetworkDiagnosticsJson.Serialize(report));

        Assert.Equal("local-network", parsed.LoadLocalization?.LikelyBoundary);
        Assert.True(parsed.DualStack?.Ipv4.HttpReachable);
        Assert.False(parsed.NetworkChange?.Changed);
        Assert.Equal("ExampleNet", parsed.NetworkChange?.PublicNetworkBefore?.Network);
        Assert.Equal(0.5, parsed.HostResources?.TcpRetransmissionPercent);
        Assert.Equal("Before router restart", parsed.Annotations?.Label);
        Assert.Equal(["Wi-Fi", "VPN off"], parsed.Annotations?.Tags);
    }

    [Fact]
    public void NetworkStateComparisonReportsMeaningfulChanges()
    {
        var before = new CapturedNetworkState(
            new NetworkStateSnapshot("wifi", "Wi-Fi", null, ["IPv4", "IPv6"], null, []),
            "192.168.1.1",
            "wifi|192.168.1.1|IPv4,IPv6||wifi");
        var after = new CapturedNetworkState(
            new NetworkStateSnapshot("ethernet", "Ethernet", null, ["IPv4"], "http://proxy.example:8080", ["Tunnel interface 1"]),
            "192.168.2.1",
            "ethernet|192.168.2.1|IPv4|http://proxy.example:8080|ethernet");
        var publicBefore = new NetworkMetadataReport("LAX", "ExampleNet", 64500, "h2", "TLSv1.3", "IPv4");
        var publicAfter = new NetworkMetadataReport("SJC", "OtherNet", 64501, "h2", "TLSv1.3", "IPv6");

        var result = NetworkStateProbe.Compare(before, after, true, publicBefore, publicAfter);

        Assert.True(result.Changed);
        Assert.True(result.CaptivePortalSuspected);
        Assert.True(result.PublicNetworkChanged);
        Assert.Contains(result.Changes, item => item.Contains("interface", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("gateway", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("address families", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("proxy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("public network", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("edge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadedPathLocalizationIdentifiesTheFirstAffectedBoundary()
    {
        var gateway = new LoadedPathTargetReport(
            "gateway", "Default gateway", null,
            StatisticsAt(5), StatisticsAt(35), StatisticsAt(30));
        var publicHop = new LoadedPathTargetReport(
            "first-public-hop", "First responsive public hop", "203.0.113.1",
            StatisticsAt(12), StatisticsAt(45), StatisticsAt(40));
        var endpoint = new LoadedPathTargetReport(
            "endpoint", "Measurement endpoint", "203.0.113.10",
            StatisticsAt(15), StatisticsAt(55), StatisticsAt(50));

        var result = LoadedPathLatencyCollector.Interpret([gateway, publicHop, endpoint]);

        Assert.Equal("local-network", result.Boundary);
        Assert.Contains("default gateway", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HostResourceEvidenceUsesRedactedInterfaceLabelsByDefault()
    {
        await using var monitor = HostResourceMonitor.Start(includeLocalIdentifiers: false);
        await Task.Delay(20);

        var report = await monitor.StopAsync(CancellationToken.None);

        Assert.InRange(report.ProcessCpuPercent, 0, 100);
        Assert.True(report.PeakWorkingSetBytes >= 0);
        Assert.True(report.TcpSegmentsSent >= 0);
        Assert.All(report.Interfaces, item =>
        {
            Assert.StartsWith("interface-", item.InterfaceId);
            Assert.StartsWith("Interface ", item.Name);
        });
    }

    private static LatencyStatistics EmptyStatistics() =>
        new(0, 0, 0, 0, null, null, null, null, null, null, []);

    private static LatencyStatistics StatisticsAt(double median) =>
        new(3, 3, 0, 0, median, median, median, median, median, 0, [median, median, median]);
}

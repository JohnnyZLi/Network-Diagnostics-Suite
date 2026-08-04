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
        var emptyStatistics = new LatencyStatistics(0, 0, 0, 0, null, null, null, null, null, null, []);
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
                new AddressFamilyProbeReport("IPv4", true, "203.0.113.10", true, 12, true, 8, null),
                new AddressFamilyProbeReport("IPv6", true, "2001:db8::10", true, 14, true, 9, null),
                "IPv4",
                false,
                "measured"),
            NetworkChange: new NetworkChangeReport(
                new NetworkStateSnapshot("if0", "Interface 1", null, ["IPv4", "IPv6"], null, []),
                new NetworkStateSnapshot("if0", "Interface 1", null, ["IPv4", "IPv6"], null, []),
                false,
                [],
                false),
            HostResources: new HostResourceReport(12.5, 100_000_000, 10_000_000, 12_000_000, [], false));

        var parsed = NetworkDiagnosticsJson.Deserialize(NetworkDiagnosticsJson.Serialize(report));

        Assert.Equal("local-network", parsed.LoadLocalization?.LikelyBoundary);
        Assert.True(parsed.DualStack?.Ipv4.TcpReachable);
        Assert.False(parsed.NetworkChange?.Changed);
        Assert.Equal(12.5, parsed.HostResources?.ProcessCpuPercent);
    }

    [Fact]
    public void NetworkStateComparisonReportsMeaningfulChanges()
    {
        var before = new CapturedNetworkState(
            new NetworkStateSnapshot("wifi", "Wi-Fi", null, ["IPv4", "IPv6"], null, []),
            "192.168.1.1",
            "before");
        var after = new CapturedNetworkState(
            new NetworkStateSnapshot("ethernet", "Ethernet", null, ["IPv4"], "http://proxy.example:8080", ["Tunnel interface 1"]),
            "192.168.2.1",
            "after");

        var result = NetworkStateProbe.Compare(before, after, captivePortal: true);

        Assert.True(result.Changed);
        Assert.True(result.CaptivePortalSuspected);
        Assert.Contains(result.Changes, item => item.Contains("interface", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("gateway", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("address families", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, item => item.Contains("proxy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HostResourceEvidenceUsesRedactedInterfaceLabelsByDefault()
    {
        await using var monitor = HostResourceMonitor.Start(includeLocalIdentifiers: false);
        await Task.Delay(20);

        var report = await monitor.StopAsync(CancellationToken.None);

        Assert.InRange(report.ProcessCpuPercent, 0, 100);
        Assert.True(report.PeakWorkingSetBytes >= 0);
        Assert.All(report.Interfaces, item =>
        {
            Assert.StartsWith("interface-", item.InterfaceId, StringComparison.Ordinal);
            Assert.StartsWith("Interface ", item.Name, StringComparison.Ordinal);
        });
    }
}

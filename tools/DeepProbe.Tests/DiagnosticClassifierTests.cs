using NetworkDeepProbe.Contracts;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Tests;

public sealed class DiagnosticClassifierTests
{
    [Fact]
    public void SharedRulesContractLoadsExpectedVersion()
    {
        var rules = DiagnosticRulesContract.Load();

        Assert.Equal("1.0", rules.SchemaVersion);
        Assert.Equal(100, rules.LoadedLatency.CriticalIncreaseMs);
        Assert.Equal(70, rules.Throughput.SingleFlowShareWarningPercent);
    }

    [Fact]
    public void SevereLoadedLatencyIncludesDirectionAndEvidence()
    {
        var report = CreateReport(downloadIncreaseMs: 55, uploadIncreaseMs: 135);

        var finding = Assert.Single(
            DiagnosticClassifier.Classify(report),
            item => item.Id == "loaded-latency");

        Assert.Equal("critical", finding.Severity);
        Assert.Contains("upload", finding.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(finding.Evidence, item => item.Metric == "internetTransfer.uploadLatency.increaseMs");
    }

    [Fact]
    public void ConstrainedSingleFlowIsReportedWithoutAssigningAnUnprovenCause()
    {
        var report = CreateReport(singleMbps: 24, aggregateMbps: 120);

        var finding = Assert.Single(
            DiagnosticClassifier.Classify(report),
            item => item.Id == "single-flow-limited");

        Assert.Equal("warning", finding.Severity);
        Assert.Contains(finding.Evidence, item => item.Metric.EndsWith("singleSharePercent", StringComparison.Ordinal) && item.Value == "20.0%");
        Assert.DoesNotContain("ISP is", finding.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WeakWifiSignalIsSupportedByOperatingSystemEvidence()
    {
        var report = CreateReport(wifiSignalPercent: 35);

        var finding = Assert.Single(
            DiagnosticClassifier.Classify(report),
            item => item.Id == "weak-wifi-signal");

        Assert.Equal("high", finding.Confidence);
        Assert.Contains(finding.Evidence, item => item.Metric == "deepDiagnostics.wifi.signalPercent" && item.Value == "35.0%");
    }

    [Fact]
    public void HealthyFixtureProducesOnlyTransparentInformation()
    {
        var findings = DiagnosticClassifier.Classify(CreateReport());

        Assert.Equal("no-obvious-instability", findings[0].Id);
        Assert.All(findings, item => Assert.Equal("info", item.Severity));
    }

    private static NetworkDiagnosticsReportV2 CreateReport(
        double downloadIncreaseMs = 8,
        double uploadIncreaseMs = 10,
        double singleMbps = 90,
        double aggregateMbps = 100,
        int? wifiSignalPercent = null)
    {
        var now = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.Standard, TransferMethod.Compare);
        var single = Throughput(singleMbps);
        var aggregate = Throughput(aggregateMbps);
        var upload = Throughput(40);
        var idle = Latency(20, 2);
        var internet = new NativeInternetTransferReport(
            "https://network.johnnyli.dev/",
            idle,
            aggregate,
            upload,
            new NativeLoadedLatencyReport(Latency(20 + downloadIncreaseMs, 2), downloadIncreaseMs, "A"),
            new NativeLoadedLatencyReport(Latency(20 + uploadIncreaseMs, 2), uploadIncreaseMs, "A"),
            [
                new NativeFlowMeasurement(TransferStrategy.Single, 1, single, null, null, null),
                new NativeFlowMeasurement(TransferStrategy.Aggregate, 8, aggregate, upload, null, null)
            ],
            [],
            50_000_000);

        DeepProbeReport? deep = wifiSignalPercent is null ? null : new DeepProbeReport(
            "1.2",
            now,
            "1.1.1.1",
            "Test OS",
            "Arm64",
            false,
            [],
            new PingTargetReport("Gateway", null, Latency(3, 1)),
            new PingTargetReport("Internet", null, Latency(20, 2)),
            new TraceRouteReport("1.1.1.1", null, 12, true, []),
            [],
            new PathMtuReport("1.1.1.1", 1472, 1500, "confirmed"),
            [],
            null,
            new WifiDetailsReport("available", "en0", null, wifiSignalPercent, -70, 36, "5 GHz", "802.11ax", 800, 700, "WPA3", null),
            new RoutingDetailsReport("available", [], null));

        return new NetworkDiagnosticsReportV2(
            "2.0",
            now,
            new DiagnosticRunMetadata(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Test OS",
                "Arm64",
                TestProfileId.Standard,
                TransferMethod.Compare,
                now,
                now,
                false),
            NativeTransferPlanReport.FromPlan(plan),
            internet,
            deep,
            null);
    }

    private static LatencyStatistics Latency(double medianMs, double jitterMs) => new(
        20,
        20,
        0,
        0,
        medianMs - 2,
        medianMs + 3,
        medianMs,
        medianMs,
        medianMs + 2,
        jitterMs,
        Enumerable.Repeat<double?>(medianMs, 20).ToArray());

    private static NativeThroughputSummary Throughput(double steadyMbps) => new(
        steadyMbps,
        steadyMbps,
        12_000_000,
        4_000,
        steadyMbps * 1.05,
        92,
        1,
        false,
        "qualified",
        [],
        "single",
        []);
}

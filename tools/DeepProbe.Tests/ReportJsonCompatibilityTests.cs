using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Tests;

public sealed class ReportJsonCompatibilityTests
{
    [Fact]
    public void UnknownOptionalFieldsAreIgnored()
    {
        const string json = """
        {
          "schemaVersion": "2.0",
          "generatedAt": "2026-08-03T00:00:00Z",
          "run": {
            "id": "11111111-1111-1111-1111-111111111111",
            "platform": "web",
            "architecture": null,
            "profile": "quick",
            "transferMethod": "compare",
            "startedAt": "2026-08-03T00:00:00Z",
            "completedAt": "2026-08-03T00:00:20Z",
            "includesLocalAddresses": false,
            "futureRunField": true
          },
          "transferPlan": {
            "profile": "quick",
            "method": "compare",
            "profileName": "Quick",
            "estimatedSeconds": 20,
            "transferCapBytes": 728000000,
            "includeServices": false,
            "downloadStages": [],
            "uploadStages": []
          },
          "internetTransfer": null,
          "deepDiagnostics": null,
          "localLink": null,
          "futureTopLevelField": { "value": 42 }
        }
        """;

        var report = NetworkDiagnosticsJson.Deserialize(json);

        Assert.Equal("2.0", report.SchemaVersion);
        Assert.Equal(TestProfileId.Quick, report.Run.Profile);
        Assert.Null(report.Measurement);
        Assert.Null(report.Findings);
    }

    [Fact]
    public void ActualWebsiteExportShapeIsNormalizedWithoutDroppingEvidence()
    {
        const string json = """
        {
          "id": "33333333-3333-3333-3333-333333333333",
          "startedAt": "2026-08-03T00:00:00Z",
          "completedAt": "2026-08-03T00:00:20Z",
          "mode": "quick",
          "transferMode": "compare",
          "edge": {
            "edge": "LAX",
            "network": "Example Fiber",
            "asn": 64500,
            "protocol": "h2",
            "tlsVersion": "TLSv1.3",
            "ipVersion": "IPv6"
          },
          "idleLatency": {
            "sent": 8,
            "received": 8,
            "lost": 0,
            "lossPercent": 0,
            "minMs": 12,
            "maxMs": 20,
            "meanMs": 15,
            "medianMs": 14,
            "p95Ms": 19,
            "jitterMs": 2,
            "samples": [12, 13, 14, 14, 15, 16, 17, 20]
          },
          "download": {
            "mbps": 510,
            "steadyMbps": 500,
            "bytes": 125000000,
            "durationMs": 2000,
            "peakMbps": 540,
            "stabilityPercent": 94,
            "rampRatio": 1.02,
            "capReached": false,
            "qualification": "qualified",
            "aggregation": "single",
            "timeline": [{ "elapsedMs": 250, "value": 480 }],
            "samples": []
          },
          "upload": {
            "mbps": 42,
            "steadyMbps": 40,
            "bytes": 10000000,
            "durationMs": 2000,
            "peakMbps": 45,
            "stabilityPercent": 91,
            "rampRatio": 1.0,
            "capReached": false,
            "qualification": "qualified",
            "timeline": [{ "elapsedMs": 250, "value": 39 }],
            "samples": []
          },
          "downloadLatency": {
            "sent": 5,
            "received": 5,
            "lost": 0,
            "lossPercent": 0,
            "minMs": 18,
            "maxMs": 28,
            "meanMs": 22,
            "medianMs": 21,
            "p95Ms": 27,
            "jitterMs": 3,
            "samples": [18, 20, 21, 23, 28],
            "increaseMs": 7,
            "grade": "A"
          },
          "uploadLatency": {
            "sent": 5,
            "received": 5,
            "lost": 0,
            "lossPercent": 0,
            "minMs": 22,
            "maxMs": 39,
            "meanMs": 29,
            "medianMs": 28,
            "p95Ms": 38,
            "jitterMs": 5,
            "samples": [22, 26, 28, 31, 39],
            "increaseMs": 14,
            "grade": "A"
          },
          "flowMeasurements": [
            {
              "strategy": "single",
              "concurrency": 1,
              "download": {
                "mbps": 320,
                "steadyMbps": 310,
                "bytes": 77500000,
                "durationMs": 2000,
                "peakMbps": 330,
                "stabilityPercent": 92,
                "rampRatio": 1.0,
                "capReached": false,
                "qualification": "qualified",
                "timeline": [],
                "samples": []
              }
            },
            {
              "strategy": "aggregate",
              "concurrency": 6,
              "download": {
                "mbps": 510,
                "steadyMbps": 500,
                "bytes": 125000000,
                "durationMs": 2000,
                "peakMbps": 540,
                "stabilityPercent": 94,
                "rampRatio": 1.02,
                "capReached": false,
                "qualification": "qualified",
                "timeline": [],
                "samples": []
              }
            }
          ],
          "downloadScaling": [],
          "services": [
            { "id": "github", "name": "GitHub", "reachable": true, "durationMs": 35 },
            { "id": "example", "name": "Example service", "reachable": false, "durationMs": null, "note": "timeout" }
          ],
          "dataUsedBytes": 135000000,
          "futureBrowserField": { "safe": true }
        }
        """;

        var report = NetworkDiagnosticsJson.Deserialize(json);

        Assert.Equal("2.0", report.SchemaVersion);
        Assert.Equal("web", report.Producer?.Application);
        Assert.Equal(TestProfileId.Quick, report.Run.Profile);
        Assert.Equal(500, report.InternetTransfer?.Download.SteadyMbps);
        Assert.Equal(6, report.InternetTransfer?.FlowMeasurements.Single(item => item.Strategy == TransferStrategy.Aggregate).Connections);
        Assert.Equal("LAX", report.BrowserEvidence?.Edge?.Edge);
        Assert.Equal("Example Fiber", report.BrowserEvidence?.Edge?.Network);
        Assert.Equal(2, report.BrowserEvidence?.ServiceChecks.Count);
        Assert.False(report.BrowserEvidence?.ServiceChecks.Single(item => item.Id == "example").Reachable);
        Assert.Contains("flow-comparison", report.Measurement?.Capabilities ?? []);
        Assert.NotEmpty(report.Findings ?? []);
    }

    [Fact]
    public void NativeMetadataFindingsAndBrowserEvidenceRoundTrip()
    {
        var now = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.ConnectionCheck, TransferMethod.Compare);
        var report = new NetworkDiagnosticsReportV2(
            "2.0",
            now,
            new DiagnosticRunMetadata(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "macOS",
                "Arm64",
                TestProfileId.ConnectionCheck,
                TransferMethod.Compare,
                now,
                now,
                false),
            NativeTransferPlanReport.FromPlan(plan),
            null,
            null,
            null,
            new ReportProducer("desktop", "1.0.0", "network-diagnostics-native"),
            new MeasurementContextReport(
                "1.0",
                "network-diagnostics-native",
                "1.0.0",
                ["http-latency"],
                new MeasurementEndpointReport(
                    "primary",
                    "Primary",
                    "Cloudflare",
                    "https://network.johnnyli.dev/",
                    "only-available",
                    18),
                []),
            [
                new DiagnosticFinding(
                    "example",
                    "summary",
                    "info",
                    "high",
                    "Example",
                    "Example finding",
                    [],
                    [])
            ],
            new BrowserReportEvidence(
                new BrowserEdgeEvidence("LAX", "Example Fiber", 64500, "h2", "TLSv1.3", "IPv6"),
                [new BrowserServiceCheck("github", "GitHub", true, 35, null)]));

        var parsed = NetworkDiagnosticsJson.Deserialize(NetworkDiagnosticsJson.Serialize(report));

        Assert.Equal("desktop", parsed.Producer?.Application);
        Assert.Equal("primary", parsed.Measurement?.SelectedEndpoint.Id);
        Assert.Equal("example", Assert.Single(parsed.Findings!).Id);
        Assert.Equal("LAX", parsed.BrowserEvidence?.Edge?.Edge);
        Assert.Equal("github", Assert.Single(parsed.BrowserEvidence!.ServiceChecks).Id);
    }
}

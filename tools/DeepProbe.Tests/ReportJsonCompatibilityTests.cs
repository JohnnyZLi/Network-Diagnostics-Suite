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
    public void NativeMetadataAndFindingsRoundTrip()
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
            ]);

        var parsed = NetworkDiagnosticsJson.Deserialize(NetworkDiagnosticsJson.Serialize(report));

        Assert.Equal("desktop", parsed.Producer?.Application);
        Assert.Equal("primary", parsed.Measurement?.SelectedEndpoint.Id);
        Assert.Equal("example", Assert.Single(parsed.Findings!).Id);
    }
}

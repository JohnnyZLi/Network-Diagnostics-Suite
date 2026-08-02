using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Tests;

public sealed class ReportV2Tests
{
    [Fact]
    public void CombinedReportUsesCamelCaseContractValues()
    {
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.Quick, TransferMethod.Compare);
        var now = new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero);
        var report = new NetworkDiagnosticsReportV2(
            "2.0",
            now,
            new DiagnosticRunMetadata(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Test OS",
                "X64",
                TestProfileId.Quick,
                TransferMethod.Compare,
                now,
                now,
                false),
            NativeTransferPlanReport.FromPlan(plan),
            null,
            null,
            null);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var json = JsonSerializer.Serialize(report, options);

        Assert.Contains("\"schemaVersion\": \"2.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"profile\": \"quick\"", json, StringComparison.Ordinal);
        Assert.Contains("\"transferMethod\": \"compare\"", json, StringComparison.Ordinal);
        Assert.Contains("\"direction\": \"download\"", json, StringComparison.Ordinal);
    }
}

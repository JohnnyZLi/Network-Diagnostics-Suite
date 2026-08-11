using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Services;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class ReportStoreTests
{
    [Fact]
    public async Task SavedReportCanBeDeletedAndRetentionPrunesOldFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "network-diagnostics-report-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ReportStore(directory);
            var first = await store.SaveAsync(CreateReport(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-10)), TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(first.Path, DateTime.UtcNow.AddDays(-10));
            var second = await store.SaveAsync(CreateReport(Guid.NewGuid(), DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

            Assert.Equal(1, store.Prune(5));
            Assert.False(File.Exists(first.Path));
            Assert.True(File.Exists(second.Path));
            Assert.True(store.Delete(second));
            Assert.False(File.Exists(second.Path));
            Assert.False(store.Delete(second));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static NetworkDiagnosticsReportV2 CreateReport(Guid id, DateTimeOffset generatedAt)
    {
        var plan = NativeTransferPlanBuilder.Build(TestProfileId.ConnectionCheck, TransferMethod.Compare);
        return new NetworkDiagnosticsReportV2(
            "2.0",
            generatedAt,
            new DiagnosticRunMetadata(
                id,
                "Test OS",
                "Arm64",
                TestProfileId.ConnectionCheck,
                TransferMethod.Compare,
                generatedAt,
                generatedAt,
                false),
            NativeTransferPlanReport.FromPlan(plan),
            null,
            null,
            null);
    }
}

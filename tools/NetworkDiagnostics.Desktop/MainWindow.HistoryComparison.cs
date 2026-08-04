using NetworkDeepProbe.Models;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private Guid? comparisonBaselineId;
    private Guid? comparisonCandidateId;
    private NetworkDiagnosticsReportV2? comparisonCandidateReport;

    private async Task RefreshComparisonHistoryAsync()
    {
        var reports = await reportStore.ListAsync();
        var baseline = FindStoredReport(reports, comparisonBaselineId);
        var candidate = FindStoredReport(reports, comparisonCandidateId);

        if (comparisonBaselineId is not null && baseline is null) comparisonBaselineId = null;
        if (comparisonCandidateId is not null && candidate is null) comparisonCandidateId = null;
        if (baseline is null && candidate is not null)
        {
            comparisonCandidateId = null;
            candidate = null;
        }

        comparisonBaselineReport = baseline?.Report;
        comparisonCandidateReport = candidate?.Report;
        savedReportCount = reports.Count;

        var trend = ReportComparisonService.AnalyzeTrend(reports);
        comparisonWorkspace?.Render(reports, comparisonBaselineId, comparisonCandidateId, trend);
    }

    private static StoredReport? FindStoredReport(
        IReadOnlyList<StoredReport> reports,
        Guid? reportId) => reportId is null
            ? null
            : reports.FirstOrDefault(item => item.Report.Run.Id == reportId.Value);
}

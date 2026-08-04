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

        HistoryCountText.Text = reports.Count == 1 ? "1 saved report" : $"{reports.Count} saved reports";

        var trend = ReportComparisonService.AnalyzeTrend(reports);
        comparisonWorkspace?.Render(reports, comparisonBaselineId, comparisonCandidateId, trend);

        if (reports.Count == 0)
        {
            HistoryFixtureTitle.Text = "No reports to compare";
            HistoryFixtureDetail.Text = "Completed diagnostics and imported schema 2.0 reports will appear here.";
            return;
        }

        if (baseline is null)
        {
            HistoryFixtureTitle.Text = "Choose a comparison baseline";
            HistoryFixtureDetail.Text = trend.Summary;
            return;
        }

        if (candidate is null)
        {
            HistoryFixtureTitle.Text = "Choose the candidate report";
            HistoryFixtureDetail.Text = $"Baseline: {HistorySelectionName(baseline)}\n\n{trend.Summary}";
            return;
        }

        var comparison = ReportComparisonService.Compare(baseline.Report, candidate.Report);
        HistoryFixtureTitle.Text = comparison.Comparable
            ? "Report comparison"
            : "Report comparison with cautions";
        HistoryFixtureDetail.Text = comparison.Summary;
    }

    private static StoredReport? FindStoredReport(
        IReadOnlyList<StoredReport> reports,
        Guid? reportId) => reportId is null
            ? null
            : reports.FirstOrDefault(item => item.Report.Run.Id == reportId.Value);

    private static string HistorySelectionName(StoredReport stored) =>
        $"{stored.Label ?? stored.ProfileName} · {stored.DisplayDate}\n{ReportComparisonService.ContextLabel(stored.Report)}";
}

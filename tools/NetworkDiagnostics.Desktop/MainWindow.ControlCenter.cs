using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private IReadOnlyList<StoredReport> controlCenterReports = [];

    private void WireControlCenterEvents()
    {
        if (testSetupWorkspace is null) return;
        testSetupWorkspace.DiagnosticLauncherRequested += ControlCenterDiagnosticLauncherRequested;
        testSetupWorkspace.DiagnosticLauncherDismissRequested += ControlCenterDiagnosticLauncherDismissRequested;
        testSetupWorkspace.RecentReportRequested += ControlCenterReportRequested;
        testSetupWorkspace.RecentReportEditRequested += ControlCenterReportEditRequested;
        testSetupWorkspace.InlineBaselineRequested += ControlCenterBaselineRequested;
        testSetupWorkspace.InlineCandidateRequested += ControlCenterCandidateRequested;
        testSetupWorkspace.InlineComparisonClearRequested += ControlCenterComparisonClearRequested;
        testSetupWorkspace.OpenHistoryRequested += ControlCenterOpenHistoryRequested;
        testSetupWorkspace.ImportHistoryRequested += ControlCenterImportHistoryRequested;
        testSetupWorkspace.OpenHistoryFolderRequested += ControlCenterOpenFolderRequested;
    }

    private void SetControlCenterReports(IReadOnlyList<StoredReport> reports)
    {
        controlCenterReports = reports;
        SyncControlCenterSections();
    }

    private void SyncControlCenterSections()
    {
        if (testSetupWorkspace is null) return;
        testSetupWorkspace.RenderControlCenter(new ControlCenterSectionModel(
            controlCenterReports,
            comparisonBaselineId,
            comparisonCandidateId,
            ReportComparisonService.AnalyzeTrend(controlCenterReports)));
    }

    private void ControlCenterDiagnosticLauncherRequested(object? sender, EventArgs eventArgs)
    {
        var content = testSetupWorkspace?.DiagnosticLauncherContent;
        if (workbenchShell is null || testSetupWorkspace is null || content is null) return;

        ShowControlCenterUnderlay();
        testSetupWorkspace.PrepareDiagnosticLauncherLayout();
        testSetupWorkspace.ApplyDiagnosticConfiguratorPolishSafely();
        testSetupWorkspace.InstallDiagnosticConfiguratorResponsiveGuard();
        testSetupWorkspace.DisableLegacyLayoutRefreshLoops();
        testSetupWorkspace.RefreshModelDependentVisuals();
        workbenchShell.SelectControlCenter();
        workbenchShell.OpenOverlay("Customize diagnostic", content, 960, 620, stretchWidth: true);
    }

    private void ControlCenterDiagnosticLauncherDismissRequested(object? sender, EventArgs eventArgs) =>
        workbenchShell?.CloseOverlay();

    private void ControlCenterReportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        OpenStoredReport(eventArgs.Report);

    private async void ControlCenterReportEditRequested(object? sender, StoredReportEventArgs eventArgs) =>
        await EditReportAnnotationsAsync(eventArgs.Report);

    private async void ControlCenterBaselineRequested(object? sender, StoredReportEventArgs eventArgs)
    {
        comparisonBaselineId = eventArgs.Report.Report.Run.Id;
        comparisonBaselineReport = eventArgs.Report.Report;
        if (comparisonCandidateId == comparisonBaselineId)
        {
            comparisonCandidateId = null;
            comparisonCandidateReport = null;
        }
        await RefreshComparisonHistoryAsync();
        SyncControlCenterSections();
    }

    private async void ControlCenterCandidateRequested(object? sender, StoredReportEventArgs eventArgs)
    {
        if (comparisonBaselineId is null || eventArgs.Report.Report.Run.Id == comparisonBaselineId) return;
        comparisonCandidateId = eventArgs.Report.Report.Run.Id;
        comparisonCandidateReport = eventArgs.Report.Report;
        await RefreshComparisonHistoryAsync();
        SyncControlCenterSections();
    }

    private async void ControlCenterComparisonClearRequested(object? sender, EventArgs eventArgs)
    {
        comparisonBaselineId = null;
        comparisonCandidateId = null;
        comparisonBaselineReport = null;
        comparisonCandidateReport = null;
        await RefreshComparisonHistoryAsync();
        SyncControlCenterSections();
    }

    private void ControlCenterOpenHistoryRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new ReportListDestination());

    private async void ControlCenterImportHistoryRequested(object? sender, EventArgs eventArgs) =>
        await ImportReportAsync();

    private void ControlCenterOpenFolderRequested(object? sender, EventArgs eventArgs) =>
        reportStore.OpenReportsFolder();
}

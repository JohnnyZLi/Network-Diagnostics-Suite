using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Workspaces;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private async void ReportBrowserImportRequested(object? sender, EventArgs eventArgs) =>
        await ImportReportAsync();

    private void ReportBrowserOpenFolderRequested(object? sender, EventArgs eventArgs) =>
        reportStore.OpenReportsFolder();

    private void ReportBrowserOpenReportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        OpenStoredReport(eventArgs.Report);

    private void ReportBrowserCompareReportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        CompareStoredReport(eventArgs.Report);

    private async void ReportBrowserEditReportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        await EditReportAnnotationsAsync(eventArgs.Report);

    private void ReportBrowserStateChanged(object? sender, ReportBrowserStateChangedEventArgs eventArgs)
    {
        if (applyingNavigation || navigationService.Current?.Destination is not ReportListDestination) return;
        navigationService.UpdateCurrentState(new NavigationViewState(
            SearchQuery: eventArgs.State.SearchQuery,
            SortKey: eventArgs.State.SortKey,
            SortDescending: eventArgs.State.SortDescending,
            SelectedReportId: eventArgs.State.SelectedReportId,
            InspectorOpen: workbenchShell?.InspectorOpen ?? true));
        RefreshWorkbenchChrome();
    }

    private void ReportDetailBackRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new ReportListDestination());

    private void ReportDetailCompareRequested(object? sender, StoredReportEventArgs eventArgs) =>
        CompareStoredReport(eventArgs.Report);

    private async void ReportDetailEditRequested(object? sender, StoredReportEventArgs eventArgs) =>
        await EditReportAnnotationsAsync(eventArgs.Report);

    private async void ReportDetailExportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        await ExportReportAsync(eventArgs.Report.Report);

    private void ComparisonWorkspaceClearRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new ComparisonDestination());

    private void ComparisonWorkspaceBaselineRequested(object? sender, StoredReportEventArgs eventArgs) =>
        NavigateToDestination(new ComparisonDestination(eventArgs.Report.Report.Run.Id));

    private void ComparisonWorkspaceCandidateRequested(object? sender, StoredReportEventArgs eventArgs)
    {
        if (comparisonBaselineId is null || comparisonBaselineId == eventArgs.Report.Report.Run.Id) return;
        NavigateToDestination(new ComparisonDestination(comparisonBaselineId, eventArgs.Report.Report.Run.Id));
    }

    private void ComparisonWorkspaceOpenReportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        OpenStoredReport(eventArgs.Report);

    private async void ComparisonWorkspaceEditReportRequested(object? sender, StoredReportEventArgs eventArgs) =>
        await EditReportAnnotationsAsync(eventArgs.Report);
}

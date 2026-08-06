using Avalonia.Threading;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private bool controlCenterOverlaysWired;

    private void WireControlCenterOverlays()
    {
        if (controlCenterOverlaysWired || workbenchShell is null) return;
        controlCenterOverlaysWired = true;
        workbenchShell.OverlayCloseRequested += ControlCenterOverlayCloseRequested;
        if (reportDetailWorkspace is not null)
        {
            reportDetailWorkspace.HomeRequested += ReportDetailHomeRequested;
            reportDetailWorkspace.RunAgainRequested += ReportDetailRunAgainRequested;
        }
        navigationService.Changed += ControlCenterOverlayNavigationChanged;
        ApplyControlCenterDestination(navigationService.Current?.Destination);
    }

    private async void ControlCenterOverlayNavigationChanged(object? sender, NavigationChangedEventArgs eventArgs)
    {
        switch (eventArgs.Current.Destination)
        {
            case RunningTestDestination:
                ShowControlCenterUnderlay();
                workbenchShell?.CloseOverlay();
                workbenchShell?.SelectControlCenter();
                SyncTestWorkspace();
                Dispatcher.UIThread.Post(() => testSetupWorkspace?.BringActiveRunIntoView());
                return;

            case TestResultDestination result when result.ReportId != Guid.Empty:
                ShowControlCenterUnderlay();
                workbenchShell?.SelectControlCenter();
                await OpenCurrentResultOverlayAsync(result);
                return;

            case SettingsDestination or ReportDetailDestination or ComparisonDestination:
                ShowControlCenterUnderlay();
                workbenchShell?.SelectControlCenter();
                break;
        }

        Dispatcher.UIThread.Post(() => ApplyControlCenterDestination(eventArgs.Current.Destination));
    }

    private void ApplyControlCenterDestination(AppDestination? destination)
    {
        if (workbenchShell is null || destination is null) return;

        switch (destination)
        {
            case RunningTestDestination:
                ShowControlCenterUnderlay();
                workbenchShell.CloseOverlay();
                workbenchShell.SelectControlCenter();
                SyncTestWorkspace();
                Dispatcher.UIThread.Post(() => testSetupWorkspace?.BringActiveRunIntoView());
                break;

            case TestResultDestination result when result.ReportId != Guid.Empty:
                _ = OpenCurrentResultOverlayAsync(result);
                break;

            case SettingsDestination settingsDestination when settingsWorkspace is not null:
                ShowControlCenterUnderlay();
                workbenchShell.SelectControlCenter();
                SyncSettingsWorkspace(settingsDestination.Section);
                workbenchShell.OpenOverlay(
                    "Settings",
                    settingsWorkspace,
                    maxWidth: 1100,
                    maxHeight: 760,
                    stretchWidth: true);
                break;

            case ReportDetailDestination when reportDetailWorkspace is not null:
                ShowControlCenterUnderlay();
                workbenchShell.SelectControlCenter();
                workbenchShell.OpenOverlay(
                    "Saved diagnostic",
                    reportDetailWorkspace,
                    maxWidth: 1160,
                    maxHeight: 820,
                    stretchWidth: true);
                break;

            case ComparisonDestination:
                workbenchShell.CloseOverlay();
                ShowControlCenterUnderlay();
                workbenchShell.SelectControlCenter();
                testSetupWorkspace?.OpenInlineComparison();
                break;

            case ReportListDestination:
                workbenchShell.CloseOverlay();
                workbenchShell.SelectControlCenter();
                break;

            default:
                workbenchShell.CloseOverlay();
                break;
        }
    }

    private async Task OpenCurrentResultOverlayAsync(TestResultDestination destination)
    {
        if (workbenchShell is null || reportDetailWorkspace is null) return;
        if (!await LoadReportForNavigationAsync(destination.ReportId)
            || selectedHistoryReport is null)
        {
            workbenchShell.CloseOverlay();
            NavigateToDestination(new TestSetupDestination());
            return;
        }

        ShowControlCenterUnderlay();
        workbenchShell.SelectControlCenter();
        reportDetailWorkspace.RenderCurrent(selectedHistoryReport, currentPresentation);
        workbenchShell.OpenOverlay(
            "Diagnostic result",
            reportDetailWorkspace,
            maxWidth: 1160,
            maxHeight: 820,
            stretchWidth: true);
    }

    private void ShowControlCenterUnderlay()
    {
        HideRedesignedWorkspaces();
        TestArea.IsVisible = true;
        currentTestState = TestViewState.Setup;
        SetupView.IsVisible = true;
        RunningView.IsVisible = false;
        ResultsView.IsVisible = false;
    }

    private void ReportDetailHomeRequested(object? sender, EventArgs eventArgs)
    {
        workbenchShell?.CloseOverlay();
        NavigateToDestination(new TestSetupDestination());
    }

    private void ReportDetailRunAgainRequested(object? sender, EventArgs eventArgs)
    {
        workbenchShell?.CloseOverlay();
        TestResultRunAgainRequested(sender, eventArgs);
    }

    private void ControlCenterOverlayCloseRequested(object? sender, EventArgs eventArgs)
    {
        if (navigationService.Current?.Destination is SettingsDestination
            or ReportDetailDestination
            or TestResultDestination)
        {
            NavigateToDestination(new TestSetupDestination());
            return;
        }

        workbenchShell?.CloseOverlay();
    }
}

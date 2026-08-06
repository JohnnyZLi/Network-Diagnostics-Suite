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
        }
        navigationService.Changed += ControlCenterOverlayNavigationChanged;
        ApplyControlCenterDestination(navigationService.Current?.Destination);
    }

    private void ControlCenterOverlayNavigationChanged(object? sender, NavigationChangedEventArgs eventArgs)
    {
        if (eventArgs.Current.Destination is SettingsDestination or ReportDetailDestination or ComparisonDestination)
        {
            ShowControlCenterUnderlay();
            workbenchShell?.SelectControlCenter();
        }

        Dispatcher.UIThread.Post(() => ApplyControlCenterDestination(eventArgs.Current.Destination));
    }

    private void ApplyControlCenterDestination(AppDestination? destination)
    {
        if (workbenchShell is null || destination is null) return;

        switch (destination)
        {
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

    private void ControlCenterOverlayCloseRequested(object? sender, EventArgs eventArgs)
    {
        if (navigationService.Current?.Destination is SettingsDestination or ReportDetailDestination)
        {
            NavigateToDestination(new TestSetupDestination());
            return;
        }

        workbenchShell?.CloseOverlay();
    }
}

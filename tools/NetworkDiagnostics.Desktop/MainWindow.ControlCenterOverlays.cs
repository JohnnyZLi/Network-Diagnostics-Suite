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

    private void ControlCenterOverlayNavigationChanged(object? sender, NavigationChangedEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(() => ApplyControlCenterDestination(eventArgs.Current.Destination));

    private void ApplyControlCenterDestination(AppDestination? destination)
    {
        if (workbenchShell is null || destination is null) return;

        switch (destination)
        {
            case SettingsDestination settingsDestination when settingsWorkspace is not null:
                ShowControlCenterUnderlay();
                SyncSettingsWorkspace(settingsDestination.Section);
                workbenchShell.OpenOverlay("Settings", settingsWorkspace, 1240);
                break;

            case ReportDetailDestination when reportDetailWorkspace is not null:
                ShowControlCenterUnderlay();
                workbenchShell.SelectControlCenter();
                workbenchShell.OpenOverlay("Saved diagnostic", reportDetailWorkspace, 1180);
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
        SyncTestWorkspace();
    }

    private void ReportDetailHomeRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new TestSetupDestination());

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

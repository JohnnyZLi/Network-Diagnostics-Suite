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
            reportDetailWorkspace.CloseRequested += ReportDetailCloseRequested;
            reportDetailWorkspace.RunAgainRequested += ReportDetailRunAgainRequested;
        }
    }

    private void ShowControlCenterUnderlay()
    {
        HideWhenNotInOverlay(reportBrowserWorkspace);
        HideWhenNotInOverlay(reportDetailWorkspace);
        HideWhenNotInOverlay(comparisonWorkspace);
        HideWhenNotInOverlay(settingsWorkspace);
        TestArea.IsVisible = true;
        currentTestState = TestViewState.Setup;
        SetupView.IsVisible = true;
    }

    private void HideWhenNotInOverlay(Avalonia.Controls.Control? surface)
    {
        if (surface is not null && workbenchShell?.IsOverlayContent(surface) != true)
        {
            surface.IsVisible = false;
        }
    }

    private void ReportDetailCloseRequested(object? sender, EventArgs eventArgs) =>
        NavigateToDestination(new TestSetupDestination());

    private void ReportDetailRunAgainRequested(object? sender, EventArgs eventArgs)
    {
        workbenchShell?.CloseOverlay();
        RunAgainClicked(sender, new Avalonia.Interactivity.RoutedEventArgs());
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

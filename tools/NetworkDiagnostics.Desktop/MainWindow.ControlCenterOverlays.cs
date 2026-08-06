using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private bool controlCenterOverlaysWired;

    private void WireControlCenterOverlays()
    {
        if (controlCenterOverlaysWired || workbenchShell is null) return;
        controlCenterOverlaysWired = true;
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
        SetupView.IsVisible = true;
    }

    private void HideWhenNotInOverlay(Avalonia.Controls.Control? surface)
    {
        // IsOverlayContent is now the compatibility hook for the currently focused
        // in-shell workspace. Keep that surface alive until the destination switch is
        // ready, while ordinary sibling workspaces remain hidden.
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
}

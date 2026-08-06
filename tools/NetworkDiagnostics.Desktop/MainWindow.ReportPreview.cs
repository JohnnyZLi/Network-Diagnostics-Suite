using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void PreviewSavedReport()
    {
        if (reportDetailWorkspace is null || workbenchShell is null) return;

        reportDetailWorkspace.RenderPreview(ConnectionCheckFixtures.Get(0));
        ShowControlCenterUnderlay();
        workbenchShell.SelectControlCenter();
        workbenchShell.OpenOverlay("Saved diagnostic", reportDetailWorkspace, 1180);
    }
}

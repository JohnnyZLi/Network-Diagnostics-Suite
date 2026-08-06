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
        workbenchShell.OpenOverlay(
            "Saved diagnostic",
            reportDetailWorkspace,
            maxWidth: 1160,
            maxHeight: 820,
            stretchWidth: true);
    }

    private void PreviewCurrentResult()
    {
        if (reportDetailWorkspace is null || workbenchShell is null) return;

        reportDetailWorkspace.RenderCurrentPreview(ConnectionCheckFixtures.Get(1));
        ShowControlCenterUnderlay();
        workbenchShell.SelectControlCenter();
        workbenchShell.OpenOverlay(
            "Diagnostic result",
            reportDetailWorkspace,
            maxWidth: 1160,
            maxHeight: 820,
            stretchWidth: true);
    }
}

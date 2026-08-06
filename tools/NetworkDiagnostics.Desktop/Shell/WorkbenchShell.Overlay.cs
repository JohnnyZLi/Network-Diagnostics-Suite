using Avalonia.Controls;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    private Control? focusedWorkspaceContent;
    private Panel? focusedWorkspaceParent;
    private Control? focusedPreviousSurface;

    public event EventHandler? OverlayCloseRequested;

    // Compatibility name for existing callers. Major content no longer uses an
    // overlay visual at all; this simply means a focused workspace is selected.
    public bool OverlayOpen => focusedWorkspaceContent is not null;

    public bool IsOverlayContent(Control? content) =>
        content is not null && ReferenceEquals(focusedWorkspaceContent, content);

    public void EnsureOverlay()
    {
        // Intentionally empty. Reports and Settings are existing siblings in the
        // main workspace grid, so no modal/backdrop/sheet visual needs to be created.
    }

    public void SetReducedMotion(bool value)
    {
        // Focused workspace changes are atomic and never animate.
    }

    public void OpenOverlay(
        string title,
        Control content,
        double maxWidth = 1180,
        double? maxHeight = null,
        bool stretchWidth = false,
        bool showHeader = true)
    {
        if (ReferenceEquals(focusedWorkspaceContent, content))
        {
            content.IsVisible = true;
            inspectorRequested = false;
            RefreshResponsiveChrome();
            return;
        }

        var parent = content.GetLogicalParent() as Panel;
        if (parent is null)
        {
            // The application-owned report/settings surfaces are always mounted in
            // the main workspace grid. Refuse to manufacture a fallback popup if a
            // future caller violates that ownership contract.
            return;
        }

        if (focusedWorkspaceContent is null)
        {
            focusedWorkspaceParent = parent;
            focusedPreviousSurface = parent.Children
                .OfType<Control>()
                .FirstOrDefault(control =>
                    !ReferenceEquals(control, content) && control.IsVisible);
        }
        else
        {
            focusedWorkspaceContent.IsVisible = false;
            if (!ReferenceEquals(focusedWorkspaceParent, parent))
            {
                focusedWorkspaceParent = parent;
                focusedPreviousSurface = parent.Children
                    .OfType<Control>()
                    .FirstOrDefault(control =>
                        !ReferenceEquals(control, content) && control.IsVisible);
            }
        }

        foreach (var sibling in parent.Children.OfType<Control>())
        {
            sibling.IsVisible = ReferenceEquals(sibling, content);
        }

        focusedWorkspaceContent = content;
        content.IsVisible = true;
        inspectorRequested = false;
        RefreshResponsiveChrome();
    }

    public void SelectControlCenter()
    {
        currentWorkspace = WorkspaceKind.Test;
        RenderProductContext();
        RefreshResponsiveChrome();
    }

    public void CloseOverlay()
    {
        if (focusedWorkspaceContent is null) return;

        // Navigation to Home prepares the previous surface before calling CloseOverlay,
        // so swap immediately in that case. Navigation to another deep workspace (for
        // example Report -> Library) may need async data first; keep the current surface
        // painted until that destination explicitly replaces it instead of flashing Home.
        var previousReady = focusedPreviousSurface?.IsVisible == true;
        if (previousReady || focusedPreviousSurface is null)
        {
            focusedWorkspaceContent.IsVisible = false;
        }

        focusedWorkspaceContent = null;
        focusedWorkspaceParent = null;
        focusedPreviousSurface = null;
        RefreshResponsiveChrome();
    }
}

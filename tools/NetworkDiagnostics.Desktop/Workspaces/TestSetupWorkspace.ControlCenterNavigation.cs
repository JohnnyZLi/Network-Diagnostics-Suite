using Avalonia.Threading;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    public void OpenInlineComparison()
    {
        compareModeExpanded = true;
        if (!EnsureControlCenterSections())
        {
            Dispatcher.UIThread.Post(OpenInlineComparison);
            return;
        }

        RenderRecentDiagnostics();
        RenderInlineComparison();
        Dispatcher.UIThread.Post(() => inlineComparisonSection?.BringIntoView());
    }
}

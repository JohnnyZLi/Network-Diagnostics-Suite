using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class ReportBrowserWorkspaceProductTests
{
    [AvaloniaFact]
    public void CompactLibraryUsesReportBrowserHierarchyWithoutInstructionTray()
    {
        var workspace = new ReportBrowserWorkspace();
        var window = new Window
        {
            Width = 760,
            Height = 680,
            Content = workspace
        };

        window.Show();
        try
        {
            Assert.Contains(
                workspace.GetVisualDescendants().OfType<TextBlock>(),
                block => string.Equals(block.Text, "Report library", StringComparison.Ordinal));

            var hint = Assert.IsType<Border>(workspace.FindControl<Border>("SelectionHintBorder"));
            var empty = Assert.IsType<Border>(workspace.FindControl<Border>("EmptyStateBorder"));
            Assert.False(hint.IsVisible);
            Assert.True(empty.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }
}

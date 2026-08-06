using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class TestSetupWorkspaceRecentDiagnosticsTests
{
    [AvaloniaFact]
    public void RecentDiagnosticsUsesCompactProductActionsAndStableCompareState()
    {
        var workspace = new TestSetupWorkspace();
        var window = new Window
        {
            Width = 1200,
            Height = 820,
            Content = workspace
        };

        window.Show();
        try
        {
            var model = new ControlCenterSectionModel(
                [],
                null,
                null,
                ReportComparisonService.AnalyzeTrend([]));
            workspace.RenderProductControlCenter(model);

            var buttons = workspace.GetVisualDescendants().OfType<Button>().ToArray();
            var compare = Assert.Single(
                buttons,
                button => string.Equals(button.Content?.ToString(), "Compare", StringComparison.Ordinal));

            Assert.False(compare.IsEnabled);
            Assert.Single(
                buttons,
                button => string.Equals(button.Content?.ToString(), "Library", StringComparison.Ordinal));
            Assert.Single(
                buttons,
                button => string.Equals(button.Content?.ToString(), "Import", StringComparison.Ordinal));
            Assert.DoesNotContain(
                buttons,
                button => button.IsVisible
                    && string.Equals(button.Content?.ToString(), "Open folder", StringComparison.Ordinal));

            workspace.OpenInlineComparison();
            workspace.ApplyRecentDiagnosticsProductFlow();

            buttons = workspace.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Single(
                buttons,
                button => string.Equals(button.Content?.ToString(), "Exit compare", StringComparison.Ordinal));
            Assert.Single(
                buttons,
                button => string.Equals(button.Content?.ToString(), "Start over", StringComparison.Ordinal));
            Assert.Contains(
                workspace.GetVisualDescendants().OfType<TextBlock>(),
                block => string.Equals(
                    block.Text,
                    "Pick a reference run from Recent diagnostics.",
                    StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }
}

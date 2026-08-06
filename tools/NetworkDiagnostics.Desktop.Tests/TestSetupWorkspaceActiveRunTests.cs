using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class TestSetupWorkspaceActiveRunTests
{
    [AvaloniaFact]
    public void LiveTelemetryAndCompletionReuseTheSameDiagnosticTileControls()
    {
        var workspace = new TestSetupWorkspace();
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = workspace
        };

        window.Show();
        try
        {
            var runId = Guid.NewGuid();
            var startedAt = DateTimeOffset.UtcNow;
            var running = new ActiveRunSnapshot(
                runId,
                ActiveRunStatus.Running,
                TestProfileId.Quick,
                TransferMethod.Compare,
                startedAt,
                "Download",
                "Measuring transfer capacity",
                34,
                LiveMbps: 92.4,
                LiveLatencyMs: 24.5,
                BytesTransferred: 4_000_000);

            workspace.SetActiveRunTileState(running);

            var status = FindLiveTileStatus(workspace, "DIAGNOSTIC IN PROGRESS");
            var stop = Assert.Single(
                workspace.GetVisualDescendants().OfType<Button>(),
                button => string.Equals(button.Content?.ToString(), "Stop test", StringComparison.Ordinal));

            workspace.RenderActiveRunSnapshot(running with
            {
                Phase = "Upload",
                Detail = "Measuring upload capacity",
                Progress = 71,
                LiveMbps = 51.2,
                BytesTransferred = 8_000_000
            });

            Assert.Same(status, FindLiveTileStatus(workspace, "DIAGNOSTIC IN PROGRESS"));
            Assert.Same(
                stop,
                Assert.Single(
                    workspace.GetVisualDescendants().OfType<Button>(),
                    button => string.Equals(button.Content?.ToString(), "Stop test", StringComparison.Ordinal)));

            workspace.HoldCompletedRunTile(running with
            {
                Status = ActiveRunStatus.Completed,
                Phase = "Complete",
                Detail = "Diagnostic completed and saved.",
                Progress = 100,
                ReportId = Guid.NewGuid()
            });

            Assert.Same(status, FindLiveTileStatus(workspace, "DIAGNOSTIC COMPLETE"));
            Assert.Contains(
                workspace.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == "Preparing the saved result…"
                    && BelongsToLiveTile(block));
            Assert.False(stop.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static TextBlock FindLiveTileStatus(TestSetupWorkspace workspace, string text) =>
        Assert.Single(
            workspace.GetVisualDescendants().OfType<TextBlock>(),
            block => string.Equals(block.Text, text, StringComparison.Ordinal)
                && BelongsToLiveTile(block));

    private static bool BelongsToLiveTile(Control control) =>
        control.GetVisualAncestors()
            .OfType<Border>()
            .Any(border => border.Classes.Contains("accentSurface")
                && border.GetVisualDescendants()
                    .OfType<Button>()
                    .Any(button => string.Equals(
                        button.Content?.ToString(),
                        "Stop test",
                        StringComparison.Ordinal)));
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class TestSetupWorkspaceResponsiveScoreTests
{
    [AvaloniaFact]
    public void CompactOverviewReducesScoreCardHeightPressure()
    {
        var workspace = new TestSetupWorkspace();
        var window = new Window
        {
            Width = 900,
            Height = 700,
            Content = workspace
        };

        window.Show();
        try
        {
            var aura = Assert.IsType<Border>(workspace.FindControl<Border>("ScoreAura"));
            var alerts = Assert.IsType<StackPanel>(workspace.FindControl<StackPanel>("AlertsPanel"));
            var responsivenessScore = Assert.IsType<TextBlock>(workspace.FindControl<TextBlock>("ResponsivenessScoreText"));
            var componentGrid = Assert.IsType<Grid>(
                responsivenessScore.GetLogicalParent()?.GetLogicalParent());

            Assert.InRange(aura.Width, 170, 180);
            Assert.InRange(aura.Height, 170, 180);
            Assert.False(alerts.IsVisible);
            Assert.False(componentGrid.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }
}

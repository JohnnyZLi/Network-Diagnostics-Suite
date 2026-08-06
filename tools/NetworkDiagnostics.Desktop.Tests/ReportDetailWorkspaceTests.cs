using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class ReportDetailWorkspaceTests
{
    [AvaloniaFact]
    public void HomeButtonRaisesHomeRequested()
    {
        var workspace = new ReportDetailWorkspace();
        var requested = false;
        workspace.HomeRequested += (_, _) => requested = true;

        var homeButton = Assert.IsType<Button>(workspace.FindControl<Button>("HomeButton"));
        homeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(requested);
    }

    [AvaloniaFact]
    public void TechnicalEvidenceUsesAStableDisclosureButton()
    {
        var workspace = new ReportDetailWorkspace();
        var toggle = Assert.IsType<Button>(workspace.FindControl<Button>("EvidenceToggleButton"));
        var body = Assert.IsType<Border>(workspace.FindControl<Border>("EvidenceBody"));
        var label = Assert.IsType<TextBlock>(workspace.FindControl<TextBlock>("EvidenceToggleLabelText"));

        Assert.False(body.IsVisible);
        Assert.Equal("Show details", label.Text);

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(body.IsVisible);
        Assert.Equal("Hide details", label.Text);

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(body.IsVisible);
        Assert.Equal("Show details", label.Text);
    }

    [AvaloniaFact]
    public void CurrentResultModeAddsRunAgainWithoutReplacingTheReportViewer()
    {
        var workspace = new ReportDetailWorkspace();
        var requested = false;
        workspace.RunAgainRequested += (_, _) => requested = true;

        workspace.RenderCurrentPreview(ConnectionCheckFixtures.Get(1));

        var runAgain = Assert.Single(
            workspace.GetLogicalDescendants()
                .OfType<Button>()
                .Where(button => string.Equals(button.Content?.ToString(), "Run again", StringComparison.Ordinal)));
        var compare = Assert.Single(
            workspace.GetLogicalDescendants()
                .OfType<Button>()
                .Where(button => string.Equals(button.Content?.ToString(), "Compare", StringComparison.Ordinal)));

        Assert.True(runAgain.IsVisible);
        Assert.Contains("primary", runAgain.Classes);
        Assert.Contains("secondary", compare.Classes);

        runAgain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(requested);
    }
}

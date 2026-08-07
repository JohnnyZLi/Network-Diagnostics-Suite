using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Workspaces;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class ReportDetailWorkspaceTests
{
    [AvaloniaFact]
    public void CloseButtonRaisesCloseRequested()
    {
        var workspace = new ReportDetailWorkspace();
        var requested = false;
        workspace.CloseRequested += (_, _) => requested = true;

        var closeButton = Assert.IsType<Button>(workspace.FindControl<Button>("CloseButton"));
        closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
    public void TechnicalEvidenceKeepsViewportDrivenReportWidth()
    {
        var workspace = new ReportDetailWorkspace
        {
            Width = 1000,
            Height = 760
        };
        workspace.RenderCurrentPreview(ConnectionCheckFixtures.Get(1));

        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = workspace
        };
        window.Show();
        try
        {
            var reportBody = Assert.IsType<StackPanel>(workspace.FindControl<StackPanel>("ReportBody"));
            var toggle = Assert.IsType<Button>(workspace.FindControl<Button>("EvidenceToggleButton"));
            var evidenceBody = Assert.IsType<Border>(workspace.FindControl<Border>("EvidenceBody"));
            var collapsedWidth = reportBody.Width;

            Assert.InRange(collapsedWidth, 951, 953);
            Assert.False(evidenceBody.IsVisible);

            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(evidenceBody.IsVisible);
            Assert.Equal(collapsedWidth, reportBody.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CurrentResultModeUsesTheSameToolbarWithRunAgainAsPrimary()
    {
        var workspace = new ReportDetailWorkspace();
        var requested = false;
        workspace.RunAgainRequested += (_, _) => requested = true;

        workspace.RenderCurrentPreview(ConnectionCheckFixtures.Get(1));

        var runAgain = Assert.IsType<Button>(workspace.FindControl<Button>("RunAgainButton"));
        var compare = Assert.IsType<Button>(workspace.FindControl<Button>("CompareButton"));
        var close = Assert.IsType<Button>(workspace.FindControl<Button>("CloseButton"));

        Assert.True(runAgain.IsVisible);
        Assert.Contains("primary", runAgain.Classes);
        Assert.Contains("secondary", compare.Classes);
        Assert.True(close.IsVisible);

        runAgain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(requested);
    }

    [AvaloniaFact]
    public void SavedReportModeKeepsComparePrimaryAndHidesRunAgain()
    {
        var workspace = new ReportDetailWorkspace();

        workspace.RenderPreview(ConnectionCheckFixtures.Get(0));

        var runAgain = Assert.IsType<Button>(workspace.FindControl<Button>("RunAgainButton"));
        var compare = Assert.IsType<Button>(workspace.FindControl<Button>("CompareButton"));

        Assert.False(runAgain.IsVisible);
        Assert.Contains("primary", compare.Classes);
        Assert.DoesNotContain("secondary", compare.Classes);
    }
}

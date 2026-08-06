using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Shell;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class MainWindowBootstrapTests
{
    [AvaloniaFact]
    public void ConstructorInstallsWorkbenchShellWithoutLogicalTreeReparentingFailure()
    {
        var window = new MainWindow();

        var shell = Assert.IsType<WorkbenchShell>(window.Content);
        Assert.NotNull(shell.WorkspaceContent);
        Assert.False(shell.InspectorOpen);
        Assert.False(shell.OverlayOpen);
        Assert.NotNull(shell.FindControl<Button>("HomeButton"));
        Assert.NotNull(shell.FindControl<Button>("SettingsToolbarButton"));
        Assert.NotNull(shell.FindControl<Button>("CommandToolbarButton"));
        Assert.NotNull(shell.FindControl<Button>("InspectorToggleButton"));
    }

    [AvaloniaFact]
    public void OverlayReparentsExistingWorkspaceWithoutLogicalTreeFailure()
    {
        var shell = new WorkbenchShell();
        var originalParent = new Grid();
        var workspace = new Border();
        originalParent.Children.Add(workspace);

        var home = Assert.IsType<Button>(shell.FindControl<Button>("HomeButton"));
        var settings = Assert.IsType<Button>(shell.FindControl<Button>("SettingsToolbarButton"));
        shell.OpenOverlay("Settings", workspace);

        Assert.True(shell.OverlayOpen);
        Assert.DoesNotContain(workspace, originalParent.Children);
        Assert.True(home.IsVisible);
        Assert.True(settings.IsVisible);

        shell.CloseOverlay();
        Assert.False(shell.OverlayOpen);
        Assert.True(home.IsVisible);
        Assert.True(settings.IsVisible);
    }

    [AvaloniaFact]
    public void BoundedOverlayReceivesARealBodyHeightBeforeItIsShown()
    {
        var shell = new WorkbenchShell();
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = shell
        };
        window.Show();
        try
        {
            var workspace = new Border { MinHeight = 420 };
            shell.OpenOverlay(
                "Diagnostic result",
                workspace,
                maxWidth: 1160,
                maxHeight: 820,
                stretchWidth: true);

            var host = Assert.Single(
                shell.GetVisualDescendants().OfType<ContentControl>(),
                control => ReferenceEquals(control.Content, workspace));
            var sheet = Assert.Single(
                shell.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("modalSheet"));

            Assert.True(shell.OverlayOpen);
            Assert.Same(workspace, host.Content);
            Assert.True(workspace.IsVisible);
            Assert.True(sheet.Height >= 600);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HeaderlessOverlayLeavesOnlyTheReportToolbarVisible()
    {
        var shell = new WorkbenchShell();
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = shell
        };
        window.Show();
        try
        {
            shell.OpenOverlay(
                "Diagnostic result",
                new Border(),
                maxHeight: 700,
                showHeader: false);

            var modalHeader = Assert.Single(
                shell.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("modalHeader"));
            Assert.False(modalHeader.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReducedMotionShowsSheetWithoutAnOpacityRamp()
    {
        var shell = new WorkbenchShell();
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = shell
        };
        window.Classes.Add("reducedMotion");
        window.Show();
        try
        {
            var workspace = new Border();
            shell.OpenOverlay("Settings", workspace, maxHeight: 700);

            var sheet = Assert.Single(
                shell.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("modalSheet"));
            var root = Assert.IsType<Grid>(sheet.GetVisualParent());
            Assert.Equal(1, root.Opacity);
            Assert.Equal(1, sheet.Opacity);
            Assert.Null(sheet.Transitions);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NormalMotionAlsoMountsTheSheetAtomically()
    {
        var shell = new WorkbenchShell();
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = shell
        };
        window.Show();
        try
        {
            shell.OpenOverlay("Settings", new Border(), maxHeight: 700);

            var sheet = Assert.Single(
                shell.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("modalSheet"));
            var root = Assert.IsType<Grid>(sheet.GetVisualParent());

            Assert.Equal(1, root.Opacity);
            Assert.Equal(1, sheet.Opacity);
            Assert.Null(sheet.Transitions);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ReopeningDuringCloseKeepsTheNewSheetMounted()
    {
        var shell = new WorkbenchShell();
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = shell
        };
        window.Show();
        try
        {
            var first = new Border();
            var second = new Border { MinHeight = 420 };
            shell.OpenOverlay("Settings", first, maxHeight: 700);
            shell.CloseOverlay();
            shell.OpenOverlay("Diagnostic result", second, maxHeight: 820);

            await Task.Delay(200);

            var host = Assert.Single(
                shell.GetVisualDescendants().OfType<ContentControl>(),
                control => ReferenceEquals(control.Content, second));
            Assert.True(shell.OverlayOpen);
            Assert.Same(second, host.Content);
            Assert.False(first.IsVisible);
            Assert.True(second.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HomeTestHubUsesSelectionBeforeRunAndOnePrimaryActionPerGroup()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
            var content = Assert.Single(buttons, button => button.Name == "ContentSpeedChoice");
            var peak = Assert.Single(buttons, button => button.Name == "PeakSpeedChoice");
            var speedRun = Assert.Single(buttons, button => button.Name == "RunSelectedSpeedTestButton");
            var customize = Assert.Single(buttons, button => button.Name == "CustomizeDiagnosticButton");
            var diagnosticRun = Assert.Single(buttons, button => button.Name == "RunSelectedDiagnosticButton");

            Assert.Contains("selected", content.Classes);
            Assert.DoesNotContain("selected", peak.Classes);
            Assert.Equal("Run content test", speedRun.Content);

            peak.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.DoesNotContain("selected", content.Classes);
            Assert.Contains("selected", peak.Classes);
            Assert.Equal("Run peak test", speedRun.Content);
            Assert.Contains("secondary", customize.Classes);
            Assert.DoesNotContain("primary", customize.Classes);
            Assert.Contains("primary", diagnosticRun.Classes);
        }
        finally
        {
            window.Close();
        }
    }
}

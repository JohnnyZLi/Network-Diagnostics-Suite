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
    public void FocusedWorkspaceSwitchesExistingSiblingWithoutReparenting()
    {
        var shell = new WorkbenchShell();
        var controlCenter = new Border { IsVisible = true };
        var settingsWorkspace = new Border { IsVisible = false };
        var workspaceGrid = new Grid();
        workspaceGrid.Children.Add(controlCenter);
        workspaceGrid.Children.Add(settingsWorkspace);
        shell.WorkspaceContent = workspaceGrid;

        var home = Assert.IsType<Button>(shell.FindControl<Button>("HomeButton"));
        var settings = Assert.IsType<Button>(shell.FindControl<Button>("SettingsToolbarButton"));

        shell.OpenOverlay("Settings", settingsWorkspace);

        Assert.True(shell.OverlayOpen);
        Assert.Contains(settingsWorkspace, workspaceGrid.Children);
        Assert.False(controlCenter.IsVisible);
        Assert.True(settingsWorkspace.IsVisible);
        Assert.True(home.IsVisible);
        Assert.True(settings.IsVisible);

        shell.CloseOverlay();

        Assert.False(shell.OverlayOpen);
        Assert.Contains(settingsWorkspace, workspaceGrid.Children);
        Assert.True(controlCenter.IsVisible);
        Assert.False(settingsWorkspace.IsVisible);
    }

    [AvaloniaFact]
    public void FocusedWorkspaceSwitchesDirectlyBetweenDeepSurfaces()
    {
        var shell = new WorkbenchShell();
        var controlCenter = new Border { IsVisible = true };
        var reportWorkspace = new Border { IsVisible = false };
        var settingsWorkspace = new Border { IsVisible = false };
        var workspaceGrid = new Grid();
        workspaceGrid.Children.Add(controlCenter);
        workspaceGrid.Children.Add(reportWorkspace);
        workspaceGrid.Children.Add(settingsWorkspace);
        shell.WorkspaceContent = workspaceGrid;

        shell.OpenOverlay("Diagnostic result", reportWorkspace);
        Assert.False(controlCenter.IsVisible);
        Assert.True(reportWorkspace.IsVisible);
        Assert.False(settingsWorkspace.IsVisible);

        shell.OpenOverlay("Settings", settingsWorkspace);
        Assert.False(controlCenter.IsVisible);
        Assert.False(reportWorkspace.IsVisible);
        Assert.True(settingsWorkspace.IsVisible);

        shell.CloseOverlay();
        Assert.True(controlCenter.IsVisible);
        Assert.False(reportWorkspace.IsVisible);
        Assert.False(settingsWorkspace.IsVisible);
    }

    [AvaloniaFact]
    public void FocusedWorkspaceCreatesNoModalBackdropOrSheetVisuals()
    {
        var shell = new WorkbenchShell();
        var controlCenter = new Border { IsVisible = true };
        var reportWorkspace = new Border { IsVisible = false };
        var workspaceGrid = new Grid();
        workspaceGrid.Children.Add(controlCenter);
        workspaceGrid.Children.Add(reportWorkspace);
        shell.WorkspaceContent = workspaceGrid;

        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = shell
        };
        window.Show();
        try
        {
            shell.OpenOverlay("Diagnostic result", reportWorkspace);

            Assert.True(shell.OverlayOpen);
            Assert.Empty(
                shell.GetVisualDescendants()
                    .OfType<Border>()
                    .Where(border => border.Classes.Contains("modalBackdrop")
                        || border.Classes.Contains("modalSheet")
                        || border.Classes.Contains("modalHeader")));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FocusedWorkspaceOpenIsIdempotent()
    {
        var shell = new WorkbenchShell();
        var controlCenter = new Border { IsVisible = true };
        var reportWorkspace = new Border { IsVisible = false };
        var workspaceGrid = new Grid();
        workspaceGrid.Children.Add(controlCenter);
        workspaceGrid.Children.Add(reportWorkspace);
        shell.WorkspaceContent = workspaceGrid;

        shell.OpenOverlay("Diagnostic result", reportWorkspace);
        shell.OpenOverlay("Diagnostic result", reportWorkspace);

        Assert.True(shell.OverlayOpen);
        Assert.False(controlCenter.IsVisible);
        Assert.True(reportWorkspace.IsVisible);
        Assert.Contains(reportWorkspace, workspaceGrid.Children);

        shell.CloseOverlay();
        Assert.True(controlCenter.IsVisible);
    }

    [AvaloniaFact]
    public void MotionPreferenceDoesNotCreateASecondWorkspacePath()
    {
        var shell = new WorkbenchShell();
        var controlCenter = new Border { IsVisible = true };
        var settingsWorkspace = new Border { IsVisible = false };
        var workspaceGrid = new Grid();
        workspaceGrid.Children.Add(controlCenter);
        workspaceGrid.Children.Add(settingsWorkspace);
        shell.WorkspaceContent = workspaceGrid;

        shell.SetReducedMotion(true);
        shell.OpenOverlay("Settings", settingsWorkspace);

        Assert.True(shell.OverlayOpen);
        Assert.True(settingsWorkspace.IsVisible);
        Assert.False(controlCenter.IsVisible);
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

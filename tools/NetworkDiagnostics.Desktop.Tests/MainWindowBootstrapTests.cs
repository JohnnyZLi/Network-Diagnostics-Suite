using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}

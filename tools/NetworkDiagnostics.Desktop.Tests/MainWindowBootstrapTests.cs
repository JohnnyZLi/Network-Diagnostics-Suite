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
    }

    [AvaloniaFact]
    public void OverlayReparentsExistingWorkspaceWithoutLogicalTreeFailure()
    {
        var shell = new WorkbenchShell();
        var originalParent = new Grid();
        var workspace = new Border();
        originalParent.Children.Add(workspace);

        shell.OpenOverlay("Settings", workspace);

        Assert.True(shell.OverlayOpen);
        Assert.DoesNotContain(workspace, originalParent.Children);

        shell.CloseOverlay();
        Assert.False(shell.OverlayOpen);
    }
}

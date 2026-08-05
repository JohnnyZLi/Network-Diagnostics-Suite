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
    }
}

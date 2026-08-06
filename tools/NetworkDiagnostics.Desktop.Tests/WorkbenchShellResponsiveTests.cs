using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetworkDiagnostics.Desktop.Shell;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class WorkbenchShellResponsiveTests
{
    [AvaloniaFact]
    public void ActiveRunGetsHeaderPriorityAtMidWidth()
    {
        var shell = new WorkbenchShell();
        var window = new Window
        {
            Width = 900,
            Height = 700,
            Content = shell
        };

        window.Show();
        try
        {
            var product = Assert.IsType<StackPanel>(shell.FindControl<StackPanel>("ProductStack"));
            var activeRun = Assert.IsType<Button>(shell.FindControl<Button>("ActiveRunPanel"));

            Assert.True(product.IsVisible);

            shell.SetActiveRun(
                true,
                "Quick test",
                "Download · Measuring transfer capacity",
                48);

            Assert.False(product.IsVisible);
            Assert.True(activeRun.IsVisible);
            Assert.InRange(activeRun.Width, 128, 136);

            shell.SetActiveRun(false, string.Empty, string.Empty, 0);

            Assert.True(product.IsVisible);
            Assert.False(activeRun.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }
}

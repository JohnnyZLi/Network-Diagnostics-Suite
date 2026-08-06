using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    protected override void OnOpened(EventArgs eventArgs)
    {
        base.OnOpened(eventArgs);
        ApplyViewportAwareLayouts();
        SizeChanged += VisualLayoutWindowSizeChanged;
    }

    private void VisualLayoutWindowSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyViewportAwareLayouts();

    private void ApplyViewportAwareLayouts()
    {
        if (testSetupWorkspace?.FindControl<Grid>("OverviewGrid") is { } overviewGrid)
        {
            var available = Math.Max(720, testSetupWorkspace.Bounds.Width - 48);
            overviewGrid.Width = Math.Min(1360, available);
            overviewGrid.HorizontalAlignment = HorizontalAlignment.Center;
        }

        if (reportBrowserWorkspace?.FindControl<Border>("EmptyStateBorder") is { } historyEmpty)
        {
            historyEmpty.MaxHeight = 560;
            historyEmpty.VerticalAlignment = VerticalAlignment.Top;
        }

        if (comparisonWorkspace?.FindControl<Border>("EmptyLibraryPanel") is { } compareEmpty)
        {
            compareEmpty.MaxHeight = 560;
            compareEmpty.VerticalAlignment = VerticalAlignment.Top;
        }
    }
}

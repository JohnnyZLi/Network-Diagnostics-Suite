using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportBrowserWorkspace
{
    private StackPanel? headerActions;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        LayoutUpdated += ReportVisualLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        LayoutUpdated -= ReportVisualLayoutUpdated;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void ReportVisualLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        headerActions ??= FindHeaderActions();
        if (headerActions is not null)
        {
            headerActions.IsVisible = !EmptyStateBorder.IsVisible;
        }
    }

    private StackPanel? FindHeaderActions()
    {
        if (Content is not Grid root) return null;
        var header = root.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        return header?.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 1);
    }
}

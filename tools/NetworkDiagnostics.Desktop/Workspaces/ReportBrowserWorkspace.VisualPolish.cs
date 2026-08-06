using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportBrowserWorkspace
{
    private StackPanel? headerActions;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += ReportVisualSizeChanged;
        ApplyReportVisualPolish(Bounds.Width);
        LayoutUpdated += ReportVisualLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= ReportVisualSizeChanged;
        LayoutUpdated -= ReportVisualLayoutUpdated;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void ReportVisualSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyReportVisualPolish(eventArgs.NewSize.Width);

    private void ReportVisualLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        headerActions ??= FindHeaderActions();
        if (headerActions is not null)
        {
            headerActions.IsVisible = !EmptyStateBorder.IsVisible;
        }
    }

    private void ApplyReportVisualPolish(double width)
    {
        if (Content is Grid root)
        {
            root.Margin = new Thickness(30, 24, 30, 30);
            root.RowSpacing = 16;
            root.MaxWidth = 1380;
        }

        AddSurfaceClass(FilterBar);
        AddSurfaceClass(ReportTableBorder);
        AddSurfaceClass(EmptyStateBorder);
        EmptyStateBorder.CornerRadius = new CornerRadius(14);
        EmptyStateBorder.VerticalAlignment = VerticalAlignment.Top;
        EmptyStateBorder.Height = width < 900 ? 310 : 350;

        foreach (var text in this.GetLogicalDescendants().OfType<TextBlock>())
        {
            if (string.Equals(text.Text, "Diagnostics history", StringComparison.Ordinal))
            {
                text.FontSize = 28;
            }
            else if (string.Equals(text.Text, "Build a diagnostic history", StringComparison.Ordinal))
            {
                text.FontSize = 22;
            }
        }
    }

    private static void AddSurfaceClass(Border border)
    {
        if (!border.Classes.Contains("surface")) border.Classes.Add("surface");
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

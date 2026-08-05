using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += VisualLayoutSizeChanged;
        ApplyRenderedVisualLayout(Bounds.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= VisualLayoutSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualLayoutSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyRenderedVisualLayout(eventArgs.NewSize.Width);

    private void ApplyRenderedVisualLayout(double width)
    {
        var available = Math.Max(720, width - 48);
        OverviewGrid.Width = Math.Min(1360, available);

        DiagnosticExpander.Background = Brushes.Transparent;
        DiagnosticDetailsGrid.Background = Brushes.Transparent;
        ProfileGrid.Background = Brushes.Transparent;

        CompareMethodButton.Padding = new Thickness(5, 5);
        SingleMethodButton.Padding = new Thickness(5, 5);
        AggregateMethodButton.Padding = new Thickness(5, 5);
        CompareMethodButton.FontSize = 10;
        SingleMethodButton.FontSize = 10;
        AggregateMethodButton.FontSize = 10;

        if (width < 1180) return;

        DiagnosticDetailsGrid.ColumnDefinitions.Clear();
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.25, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.8, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
    }
}

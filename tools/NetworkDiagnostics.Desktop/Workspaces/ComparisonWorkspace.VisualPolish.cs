using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ComparisonWorkspace
{
    private Grid? comparisonRoot;
    private Border? baselineCard;
    private Border? candidateCard;
    private Border? reportPicker;
    private ScrollViewer? comparisonResults;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        ResolveComparisonLayout();
        ApplyComparisonPolish(Bounds.Width);
        SizeChanged += ComparisonVisualSizeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= ComparisonVisualSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void ComparisonVisualSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyComparisonPolish(eventArgs.NewSize.Width);

    private void ResolveComparisonLayout()
    {
        comparisonRoot ??= Content as Grid;
        baselineCard ??= SelectionGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        candidateCard ??= SelectionGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 2);
        reportPicker ??= ComparisonBody.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 0);
        comparisonResults ??= ComparisonBody.Children
            .OfType<ScrollViewer>()
            .FirstOrDefault();
    }

    private void ApplyComparisonPolish(double width)
    {
        ResolveComparisonLayout();
        if (comparisonRoot is not null)
        {
            var gutter = WorkspaceLayoutMetrics.HorizontalGutter(width);
            comparisonRoot.Margin = new Thickness(
                gutter,
                24,
                gutter,
                WorkspaceLayoutMetrics.BottomInset(width));
            comparisonRoot.RowSpacing = 16;
            comparisonRoot.MaxWidth = WorkspaceLayoutMetrics.ComparisonMaxWidth;
        }

        foreach (var text in this.GetLogicalDescendants().OfType<TextBlock>())
        {
            if (string.Equals(text.Text, "Compare network conditions", StringComparison.Ordinal))
            {
                text.FontSize = 28;
            }
        }

        EmptyLibraryPanel.VerticalAlignment = VerticalAlignment.Top;
        EmptyLibraryPanel.Height = width < 900 ? 340 : 390;
        if (!EmptyLibraryPanel.Classes.Contains("surface")) EmptyLibraryPanel.Classes.Add("surface");

        var compact = width < 1060;
        ConfigureSelectionLayout(compact);
        ConfigureBodyLayout(compact);
    }

    private void ConfigureSelectionLayout(bool compact)
    {
        if (baselineCard is null || candidateCard is null) return;
        SelectionGrid.ColumnDefinitions.Clear();
        SelectionGrid.RowDefinitions.Clear();

        if (!compact)
        {
            SelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
            SelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SelectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(baselineCard, 0);
            Grid.SetRow(baselineCard, 0);
            Grid.SetColumn(CompatibilityBanner, 1);
            Grid.SetRow(CompatibilityBanner, 0);
            Grid.SetColumnSpan(CompatibilityBanner, 1);
            Grid.SetColumn(candidateCard, 2);
            Grid.SetRow(candidateCard, 0);
        }
        else
        {
            SelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SelectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SelectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SelectionGrid.RowSpacing = 12;
            Grid.SetColumn(baselineCard, 0);
            Grid.SetRow(baselineCard, 0);
            Grid.SetColumn(candidateCard, 1);
            Grid.SetRow(candidateCard, 0);
            Grid.SetColumn(CompatibilityBanner, 0);
            Grid.SetRow(CompatibilityBanner, 1);
            Grid.SetColumnSpan(CompatibilityBanner, 2);
        }
    }

    private void ConfigureBodyLayout(bool compact)
    {
        if (reportPicker is null || comparisonResults is null) return;
        ComparisonBody.ColumnDefinitions.Clear();
        ComparisonBody.RowDefinitions.Clear();

        if (!compact)
        {
            ComparisonBody.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(360)));
            ComparisonBody.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ComparisonBody.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(reportPicker, 0);
            Grid.SetRow(reportPicker, 0);
            reportPicker.Height = double.NaN;
            Grid.SetColumn(comparisonResults, 1);
            Grid.SetRow(comparisonResults, 0);
        }
        else
        {
            ComparisonBody.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ComparisonBody.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ComparisonBody.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            ComparisonBody.RowSpacing = 14;
            Grid.SetColumn(reportPicker, 0);
            Grid.SetRow(reportPicker, 0);
            reportPicker.Height = 250;
            Grid.SetColumn(comparisonResults, 0);
            Grid.SetRow(comparisonResults, 1);
        }
    }
}

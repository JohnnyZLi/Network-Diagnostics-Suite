using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestResultWorkspace
{
    private Grid? resultRoot;
    private Grid? resultHeader;
    private StackPanel? resultActions;
    private Border? resultTabsSurface;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        ResolveResultLayout();
        InstallResultTabsSurface();
        ApplyResultVisualPolish(Bounds.Width);
        SizeChanged += ResultVisualSizeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= ResultVisualSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void ResultVisualSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyResultVisualPolish(eventArgs.NewSize.Width);

    private void ResolveResultLayout()
    {
        resultRoot ??= Content as Grid;
        if (resultRoot is null) return;
        resultHeader ??= resultRoot.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 0);
        resultActions ??= resultHeader?.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 2);
    }

    private void InstallResultTabsSurface()
    {
        if (resultTabsSurface is not null || resultRoot is null) return;
        var tabs = resultRoot.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => Grid.GetRow(grid) == 1);
        if (tabs is null) return;

        var margin = tabs.Margin;
        tabs.Margin = new Thickness(0);
        resultRoot.Children.Remove(tabs);
        resultTabsSurface = new Border
        {
            Child = tabs,
            Padding = new Thickness(3),
            Margin = margin,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(10)
        };
        resultTabsSurface.Classes.Add("surfaceSubtle");
        Grid.SetRow(resultTabsSurface, 1);
        resultRoot.Children.Add(resultTabsSurface);
    }

    private void ApplyResultVisualPolish(double width)
    {
        ResolveResultLayout();
        if (resultRoot is null || resultHeader is null) return;

        resultRoot.Margin = new Thickness(30, 24, 30, 30);
        resultRoot.MaxWidth = 1340;
        VerdictText.FontSize = 30;
        VerdictText.LineHeight = 36;
        SummaryText.MaxWidth = 820;

        foreach (var button in new[] { ReportsButton, CompareButton, ExportButton })
        {
            button.MinHeight = 34;
            button.Padding = new Thickness(11, 6);
        }

        if (resultActions is not null)
        {
            resultActions.Spacing = 5;
        }

        var compact = width < 1040;
        resultHeader.RowDefinitions.Clear();
        resultHeader.ColumnDefinitions.Clear();
        resultHeader.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        resultHeader.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (!compact)
        {
            resultHeader.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            resultHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            if (resultActions is not null)
            {
                Grid.SetColumn(resultActions, 2);
                Grid.SetRow(resultActions, 0);
                resultActions.Margin = new Thickness(0);
                resultActions.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }
        else
        {
            resultHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            resultHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            if (resultActions is not null)
            {
                Grid.SetColumn(resultActions, 1);
                Grid.SetRow(resultActions, 1);
                resultActions.Margin = new Thickness(0, 12, 0, 0);
                resultActions.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
    }
}

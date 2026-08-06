using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestResultWorkspace
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
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

    private void ApplyResultVisualPolish(double width)
    {
        ResultRoot.Margin = width < 820
            ? new Thickness(18, 18, 18, 24)
            : new Thickness(28, 22, 28, 28);
        ResultRoot.MaxWidth = 1320;

        foreach (var button in new[] { ReportsButton, CompareButton, ExportButton })
        {
            button.MinHeight = 32;
            button.Padding = new Thickness(9, 5);
            button.FontSize = 9;
        }

        RunAgainButton.MinHeight = 36;
        RunAgainButton.HorizontalAlignment = HorizontalAlignment.Stretch;

        var compactHeader = width < 1040;
        ResultHeaderGrid.ColumnDefinitions.Clear();
        ResultHeaderGrid.RowDefinitions.Clear();
        if (!compactHeader)
        {
            ResultHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ResultHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
            ResultHeaderGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ResultActionsPanel, 1);
            Grid.SetRow(ResultActionsPanel, 0);
            ResultActionsPanel.Margin = new Thickness(0);
            ResultActionsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            ResultHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ResultHeaderGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ResultHeaderGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ResultActionsPanel, 0);
            Grid.SetRow(ResultActionsPanel, 1);
            ResultActionsPanel.Margin = new Thickness(0, 12, 0, 0);
            ResultActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            ResultActionsPanel.Width = Math.Min(280, Math.Max(220, width - 36));
        }

        ConfigureTwoColumnLayout(
            ResultSummaryGrid,
            NextActionCard,
            RunContextCard,
            width >= 900,
            1.22,
            0.78);
        ConfigureTwoColumnLayout(
            EvidenceLayoutGrid,
            MeasurementsCard,
            EvidenceSideStack,
            width >= 900,
            1.12,
            0.88);
    }

    private static void ConfigureTwoColumnLayout(
        Grid grid,
        Control first,
        Control second,
        bool wide,
        double firstWeight,
        double secondWeight)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (wide)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(firstWeight, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(secondWeight, GridUnitType.Star)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 1);
            Grid.SetRow(second, 0);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 0);
            Grid.SetRow(second, 1);
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportBrowserWorkspace
{
    private StackPanel? headerActions;
    private int reportTableLayoutMode = -1;
    private int polishedReportRowCount = -1;
    private Button? firstPolishedReportRow;

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
        PolishHeaderActions();

        // Selecting a row is self-evident in a desktop table. Keeping a permanent
        // instruction strip between filters and data made the secondary library read
        // like an admin workflow and consumed a full row of vertical space.
        SelectionHintBorder.IsVisible = false;
        ApplySelectionTrayLayout(Bounds.Width);
        ApplyReportTableResponsiveLayout(Bounds.Width);
    }

    private void ApplyReportVisualPolish(double width)
    {
        if (Content is Grid root)
        {
            root.Margin = width < 820
                ? new Thickness(22, 20, 22, 26)
                : new Thickness(30, 24, 30, 30);
            root.RowSpacing = 14;
            root.MaxWidth = 1380;
        }

        AddSurfaceClass(FilterBar);
        AddSurfaceClass(ReportTableBorder);
        AddSurfaceClass(EmptyStateBorder);
        EmptyStateBorder.CornerRadius = new CornerRadius(14);
        EmptyStateBorder.HorizontalAlignment = HorizontalAlignment.Center;
        EmptyStateBorder.VerticalAlignment = VerticalAlignment.Top;
        EmptyStateBorder.MaxWidth = 920;
        EmptyStateBorder.Width = double.NaN;
        EmptyStateBorder.Height = width < 900 ? 300 : 330;

        foreach (var text in this.GetLogicalDescendants().OfType<TextBlock>())
        {
            if (string.Equals(text.Text, "HISTORY", StringComparison.Ordinal))
            {
                text.Text = "REPORTS";
            }
            else if (string.Equals(text.Text, "Diagnostics history", StringComparison.Ordinal)
                     || string.Equals(text.Text, "Report library", StringComparison.Ordinal))
            {
                text.Text = "Report library";
                text.FontSize = width < 820 ? 25 : 28;
            }
            else if (string.Equals(
                         text.Text,
                         "Review past network conditions, reopen evidence, or choose two comparable runs.",
                         StringComparison.Ordinal))
            {
                text.Text = "Search saved diagnostics, reopen evidence, or compare network conditions over time.";
            }
            else if (string.Equals(text.Text, "Build a diagnostic history", StringComparison.Ordinal))
            {
                text.FontSize = 22;
            }
        }

        headerActions ??= FindHeaderActions();
        PolishHeaderActions();
        SelectionHintBorder.IsVisible = false;
        ApplySelectionTrayLayout(width);
        ApplyReportTableResponsiveLayout(width, force: true);
    }

    private void PolishHeaderActions()
    {
        if (headerActions is null) return;
        headerActions.IsVisible = !EmptyStateBorder.IsVisible;

        foreach (var button in headerActions.Children.OfType<Button>())
        {
            var label = button.Content?.ToString();
            if (string.Equals(label, "Open data folder", StringComparison.Ordinal))
            {
                button.IsVisible = false;
                continue;
            }

            if (string.Equals(label, "Import JSON", StringComparison.Ordinal)
                || string.Equals(label, "Import", StringComparison.Ordinal))
            {
                button.Content = "Import";
                button.Classes.Remove("primary");
                if (!button.Classes.Contains("secondary")) button.Classes.Add("secondary");
            }
        }
    }

    private void ApplySelectionTrayLayout(double width)
    {
        if (SelectionPanel.Child is not Grid grid) return;

        var selected = FindTraySection(grid, "SELECTED REPORT");
        var context = FindTraySection(grid, "CONTEXT");
        var library = FindTraySection(grid, "LIBRARY");
        var actions = grid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Orientation == Orientation.Horizontal);
        if (selected is null || context is null || actions is null) return;

        if (library is not null) library.IsVisible = false;
        SelectionPanel.Padding = width < 900 ? new Thickness(14) : new Thickness(16);
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (width >= 900)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.15, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.ColumnSpacing = 20;
            grid.RowSpacing = 0;

            context.IsVisible = true;
            SetTrayPosition(selected, 0, 0);
            SetTrayPosition(context, 0, 1);
            SetTrayPosition(actions, 0, 2);
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.Margin = new Thickness(0);
            return;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (width >= 780) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.ColumnSpacing = 0;
        grid.RowSpacing = 10;

        SetTrayPosition(selected, 0, 0);
        if (width >= 780)
        {
            context.IsVisible = true;
            SetTrayPosition(context, 1, 0);
            SetTrayPosition(actions, 2, 0);
        }
        else
        {
            context.IsVisible = false;
            SetTrayPosition(actions, 1, 0);
        }
        actions.HorizontalAlignment = HorizontalAlignment.Left;
        actions.Margin = new Thickness(0, 2, 0, 0);
    }

    private void ApplyReportTableResponsiveLayout(double width, bool force = false)
    {
        var mode = width >= 1100 ? 2 : width >= 820 ? 1 : 0;
        var rows = ReportListPanel.Children.OfType<Button>().ToArray();
        var firstRow = rows.FirstOrDefault();
        if (!force
            && mode == reportTableLayoutMode
            && rows.Length == polishedReportRowCount
            && ReferenceEquals(firstRow, firstPolishedReportRow))
        {
            return;
        }

        reportTableLayoutMode = mode;
        polishedReportRowCount = rows.Length;
        firstPolishedReportRow = firstRow;

        if (ReportTableBorder.Child is not Grid tableRoot) return;
        var headerGrid = tableRoot.Children
            .OfType<Border>()
            .Select(border => border.Child)
            .OfType<Grid>()
            .FirstOrDefault();
        if (headerGrid is not null)
        {
            ConfigureReportColumns(headerGrid, mode);
            var contextHeader = headerGrid.Children
                .OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 3);
            if (contextHeader is not null) contextHeader.IsVisible = mode > 0;
        }

        foreach (var button in rows)
        {
            if (button.Content is not Grid rowGrid) continue;
            ConfigureReportColumns(rowGrid, mode);
            var context = rowGrid.Children
                .OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 3);
            if (context is not null) context.IsVisible = mode > 0;
        }
    }

    private static void ConfigureReportColumns(Grid grid, int mode)
    {
        grid.ColumnDefinitions.Clear();
        switch (mode)
        {
            case 2:
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(148)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(112)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
                grid.ColumnSpacing = 14;
                break;
            case 1:
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(132)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(96)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
                grid.ColumnSpacing = 12;
                break;
            default:
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(118)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(88)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
                grid.ColumnSpacing = 10;
                break;
        }
    }

    private static StackPanel? FindTraySection(Grid grid, string eyebrow) =>
        grid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Children
                .OfType<TextBlock>()
                .Any(text => string.Equals(text.Text, eyebrow, StringComparison.Ordinal)));

    private static void SetTrayPosition(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetRowSpan(control, 1);
        Grid.SetColumnSpan(control, 1);
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

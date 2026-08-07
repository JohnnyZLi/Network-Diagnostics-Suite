using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportDetailWorkspace
{
    private double? disclosureLockedReportWidth;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        ApplyReportDetailPolish(Bounds.Width);
        SizeChanged += ReportDetailSizeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= ReportDetailSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void ReportDetailSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyReportDetailPolish(eventArgs.NewSize.Width);

    private void ResetReportDisclosureWidth()
    {
        disclosureLockedReportWidth = null;
        ApplyReportDetailPolish(Bounds.Width);
    }

    private void LockReportWidthForEvidenceDisclosure()
    {
        if (disclosureLockedReportWidth is not null || ReportBody.Bounds.Width <= 0) return;

        disclosureLockedReportWidth = ReportBody.Bounds.Width;
        ApplyReportDetailPolish(Bounds.Width);
    }

    private void ApplyReportDetailPolish(double width)
    {
        var horizontalGutter = WorkspaceLayoutMetrics.HorizontalGutter(width);
        var bottomInset = WorkspaceLayoutMetrics.BottomInset(width);
        var availableWidth = Math.Max(0, width - (horizontalGutter * 2));
        var contentWidth = Math.Min(WorkspaceLayoutMetrics.ReportDetailMaxWidth, availableWidth);

        ReportBody.Margin = new Thickness(horizontalGutter, width < 720 ? 16 : 22, horizontalGutter, bottomInset);
        ReportBody.MaxWidth = WorkspaceLayoutMetrics.ReportDetailMaxWidth;
        ReportBody.Width = width > 0 ? contentWidth : double.NaN;
        ReportBody.Spacing = 0;

        ReportToolbarGrid.MaxWidth = WorkspaceLayoutMetrics.ReportDetailMaxWidth;
        ReportToolbarGrid.Width = width > 0 ? contentWidth : double.NaN;
        ReportToolbarGrid.HorizontalAlignment = HorizontalAlignment.Center;
        ReportToolbarGrid.Margin = new Thickness(0);

        CloseButton.IsVisible = false;
        ToolbarTitleText.IsVisible = width >= 700;
        ToolbarMetaText.IsVisible = width >= 900;

        VerdictText.HorizontalAlignment = HorizontalAlignment.Left;
        SummaryText.HorizontalAlignment = HorizontalAlignment.Left;
        if (VerdictText.GetLogicalParent() is StackPanel heroContent)
        {
            heroContent.Spacing = width < 720 ? 5 : 6;
            heroContent.VerticalAlignment = VerticalAlignment.Top;
        }

        GeneratedText.FontWeight = FontWeight.Normal;
        ProfileText.FontWeight = FontWeight.Normal;
        MethodText.FontWeight = FontWeight.Normal;
        ContextText.FontWeight = FontWeight.Normal;

        ReportContextGrid.Margin = new Thickness(0, width < 720 ? 12 : 16, 0, 0);
        if (ReportBody.Children.OfType<Border>().FirstOrDefault(border => border.Classes.Contains("divider")) is { } sectionDivider)
        {
            sectionDivider.Margin = new Thickness(0, width < 720 ? 12 : 16, 0, 0);
        }

        if (ReportOverviewSurface.GetLogicalParent() is StackPanel atAGlanceSection)
        {
            atAGlanceSection.Margin = new Thickness(0, width < 720 ? 14 : 17, 0, 0);
            atAGlanceSection.Spacing = width < 720 ? 7 : 8;
        }
        ReportOverviewSurface.Padding = new Thickness(width < 720 ? 15 : 18, width < 720 ? 13 : 14);

        FindingsSection.Margin = new Thickness(0, width < 720 ? 15 : 18, 0, 0);
        FindingsSection.Spacing = width < 720 ? 5 : 6;
        ReportEvidenceCard.Margin = new Thickness(0, width < 720 ? 12 : 14, 0, 0);
        EvidenceToggleButton.Padding = new Thickness(16, width < 720 ? 10 : 11);

        ApplyHeroLayout(width);
        ApplyContextLayout(width);
        ApplySignalLayout(width);
        ApplyMetricLayout(width);
        ApplyFindingLayout(width);
    }

    private void ApplyHeroLayout(double width)
    {
        ReportHeroGrid.ColumnDefinitions.Clear();
        ReportHeroGrid.RowDefinitions.Clear();

        if (width >= 940)
        {
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(340)));
            ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportHeroGrid.ColumnSpacing = 40;
            Grid.SetColumn(ReportNextActionCard, 1);
            Grid.SetRow(ReportNextActionCard, 0);
            ReportNextActionCard.Margin = new Thickness(0, 1, 0, 0);
            ReportNextActionCard.Padding = new Thickness(18, 0, 0, 0);
            ReportNextActionCard.VerticalAlignment = VerticalAlignment.Top;
            return;
        }

        ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ReportHeroGrid.ColumnSpacing = 0;
        Grid.SetColumn(ReportNextActionCard, 0);
        Grid.SetRow(ReportNextActionCard, 1);
        ReportNextActionCard.Margin = new Thickness(0, 12, 0, 0);
        ReportNextActionCard.Padding = new Thickness(14, 0, 0, 0);
        ReportNextActionCard.VerticalAlignment = VerticalAlignment.Top;
    }

    private void ApplyContextLayout(double width)
    {
        ReportContextGrid.ColumnDefinitions.Clear();
        ReportContextGrid.RowDefinitions.Clear();

        var items = new Control[]
        {
            GeneratedContextItem,
            ProfileContextItem,
            MethodContextItem,
            RouteContextItem
        };

        if (width >= 900)
        {
            ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.05, GridUnitType.Star)));
            ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.75, GridUnitType.Star)));
            ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.75, GridUnitType.Star)));
            ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.65, GridUnitType.Star)));
            ReportContextGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportContextGrid.ColumnSpacing = 24;
            ReportContextGrid.RowSpacing = 0;
            for (var index = 0; index < items.Length; index++)
            {
                SetContextPosition(items[index], 0, index);
            }
            return;
        }

        if (width >= 600)
        {
            ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportContextGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportContextGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportContextGrid.ColumnSpacing = 18;
            ReportContextGrid.RowSpacing = 9;
            SetContextPosition(GeneratedContextItem, 0, 0);
            SetContextPosition(ProfileContextItem, 0, 1);
            SetContextPosition(MethodContextItem, 1, 0);
            SetContextPosition(RouteContextItem, 1, 1);
            return;
        }

        ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ReportContextGrid.ColumnSpacing = 0;
        ReportContextGrid.RowSpacing = 8;
        for (var index = 0; index < items.Length; index++)
        {
            ReportContextGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetContextPosition(items[index], index, 0);
        }
    }

    private void ApplySignalLayout(double width)
    {
        var signals = new[]
        {
            ResponsivenessSignal,
            ReliabilitySignal,
            ThroughputSignal
        };

        SignalGrid.ColumnDefinitions.Clear();
        SignalGrid.RowDefinitions.Clear();

        if (width >= 840)
        {
            for (var index = 0; index < signals.Length; index++)
            {
                SignalGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
            SignalGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SignalGrid.ColumnSpacing = 20;
            SignalGrid.RowSpacing = 0;

            for (var index = 0; index < signals.Length; index++)
            {
                Grid.SetColumn(signals[index], index);
                Grid.SetRow(signals[index], 0);
                signals[index].Padding = index < signals.Length - 1
                    ? new Thickness(0, 0, 18, 0)
                    : new Thickness(0);
                signals[index].BorderThickness = index < signals.Length - 1
                    ? new Thickness(0, 0, 1, 0)
                    : new Thickness(0);
            }
            return;
        }

        SignalGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var index = 0; index < signals.Length; index++)
        {
            SignalGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(signals[index], 0);
            Grid.SetRow(signals[index], index);
            signals[index].Padding = index < signals.Length - 1
                ? new Thickness(0, 0, 0, 11)
                : new Thickness(0);
            signals[index].BorderThickness = index < signals.Length - 1
                ? new Thickness(0, 0, 0, 1)
                : new Thickness(0);
        }
        SignalGrid.ColumnSpacing = 0;
        SignalGrid.RowSpacing = 11;
    }

    private void ApplyMetricLayout(double width)
    {
        var metrics = MetricGrid.Children.OfType<Control>().ToArray();
        MetricGrid.ColumnDefinitions.Clear();
        MetricGrid.RowDefinitions.Clear();
        if (metrics.Length == 0) return;

        var columnCount = width >= 1050
            ? metrics.Length <= 4 ? metrics.Length : 3
            : width >= 560 ? 2 : 1;
        var rowCount = (int)Math.Ceiling(metrics.Length / (double)columnCount);

        for (var column = 0; column < columnCount; column++)
        {
            MetricGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }
        for (var row = 0; row < rowCount; row++)
        {
            MetricGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        MetricGrid.ColumnSpacing = width < 720 ? 16 : 22;
        MetricGrid.RowSpacing = width < 720 ? 11 : 12;
        for (var index = 0; index < metrics.Length; index++)
        {
            Grid.SetColumn(metrics[index], index % columnCount);
            Grid.SetRow(metrics[index], index / columnCount);
        }
    }

    private void ApplyFindingLayout(double width)
    {
        foreach (var container in FindingsPanel.Children.OfType<Border>())
        {
            if (container.Child is not Grid row || row.Children.Count < 2) continue;
            row.ColumnDefinitions.Clear();
            row.RowDefinitions.Clear();
            if (width >= 700)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(96)));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                row.ColumnSpacing = 14;
                row.RowSpacing = 0;
                Grid.SetColumn(row.Children[0], 0);
                Grid.SetRow(row.Children[0], 0);
                Grid.SetColumn(row.Children[1], 1);
                Grid.SetRow(row.Children[1], 0);
                container.Padding = new Thickness(0, 9, 0, 11);
                continue;
            }

            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            row.RowSpacing = 4;
            row.ColumnSpacing = 0;
            Grid.SetColumn(row.Children[0], 0);
            Grid.SetRow(row.Children[0], 0);
            Grid.SetColumn(row.Children[1], 0);
            Grid.SetRow(row.Children[1], 1);
            container.Padding = new Thickness(0, 9, 0, 10);
        }
    }

    private static void SetContextPosition(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, 1);
    }
}

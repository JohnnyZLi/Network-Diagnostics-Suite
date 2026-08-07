using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
        var compact = width < 720;

        ReportBody.Margin = new Thickness(horizontalGutter, compact ? 18 : 24, horizontalGutter, bottomInset);
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
        HeroContentStack.Spacing = compact ? 5 : 6;
        HeroContentStack.VerticalAlignment = VerticalAlignment.Top;

        OverviewSection.Margin = new Thickness(0, compact ? 20 : 26, 0, 0);
        OverviewSection.Spacing = compact ? 7 : 8;
        ReportOverviewSurface.Padding = new Thickness(compact ? 15 : 20, compact ? 14 : 18);

        FindingsSection.Margin = new Thickness(0, compact ? 18 : 22, 0, 0);
        FindingsSection.Spacing = compact ? 5 : 6;
        ReportEvidenceCard.Margin = new Thickness(0, compact ? 10 : 12, 0, 0);
        EvidenceToggleButton.Padding = new Thickness(16, compact ? 10 : 11);

        ApplyHeroLayout(width);
        ApplySignalLayout(width);
    }

    private void ApplyHeroLayout(double width)
    {
        ReportHeroGrid.ColumnDefinitions.Clear();
        ReportHeroGrid.RowDefinitions.Clear();

        if (width >= 960)
        {
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(320)));
            ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportHeroGrid.ColumnSpacing = 38;
            Grid.SetColumn(HeroContentStack, 0);
            Grid.SetRow(HeroContentStack, 0);
            Grid.SetColumn(ReportNextActionCard, 1);
            Grid.SetRow(ReportNextActionCard, 0);
            ReportNextActionCard.Margin = new Thickness(0, 1, 0, 0);
            ReportNextActionCard.Padding = new Thickness(16, 0, 0, 0);
            ReportNextActionCard.VerticalAlignment = VerticalAlignment.Top;
            return;
        }

        ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ReportHeroGrid.ColumnSpacing = 0;
        Grid.SetColumn(HeroContentStack, 0);
        Grid.SetRow(HeroContentStack, 0);
        Grid.SetColumn(ReportNextActionCard, 0);
        Grid.SetRow(ReportNextActionCard, 1);
        ReportNextActionCard.Margin = new Thickness(0, 13, 0, 0);
        ReportNextActionCard.Padding = new Thickness(14, 0, 0, 0);
        ReportNextActionCard.VerticalAlignment = VerticalAlignment.Top;
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
            SignalGrid.ColumnSpacing = 22;
            SignalGrid.RowSpacing = 0;

            for (var index = 0; index < signals.Length; index++)
            {
                Grid.SetColumn(signals[index], index);
                Grid.SetRow(signals[index], 0);
                signals[index].Padding = index < signals.Length - 1
                    ? new Thickness(0, 0, 20, 0)
                    : new Thickness(0);
                signals[index].BorderThickness = index < signals.Length - 1
                    ? new Thickness(0, 0, 1, 0)
                    : new Thickness(0);
            }
            return;
        }

        SignalGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        SignalGrid.ColumnSpacing = 0;
        SignalGrid.RowSpacing = 14;
        for (var index = 0; index < signals.Length; index++)
        {
            SignalGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(signals[index], 0);
            Grid.SetRow(signals[index], index);
            signals[index].Padding = index < signals.Length - 1
                ? new Thickness(0, 0, 0, 14)
                : new Thickness(0);
            signals[index].BorderThickness = index < signals.Length - 1
                ? new Thickness(0, 0, 0, 1)
                : new Thickness(0);
        }
    }
}

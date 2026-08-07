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

        ReportBody.Margin = new Thickness(horizontalGutter, 18, horizontalGutter, bottomInset);
        ReportBody.MaxWidth = WorkspaceLayoutMetrics.ReportDetailMaxWidth;
        ReportBody.Width = width > 0 ? contentWidth : double.NaN;
        ReportBody.Spacing = width < 720 ? 13 : 15;

        // The toolbar and report body share one horizontal grid. This removes the
        // large empty gutters on desktop while preserving a bounded reading width.
        ReportToolbarGrid.MaxWidth = WorkspaceLayoutMetrics.ReportDetailMaxWidth;
        ReportToolbarGrid.Width = width > 0 ? contentWidth : double.NaN;
        ReportToolbarGrid.HorizontalAlignment = HorizontalAlignment.Center;
        ReportToolbarGrid.Margin = new Thickness(0);

        // Report detail is a normal focused workspace under the persistent shell.
        CloseButton.IsVisible = false;
        ToolbarTitleText.IsVisible = width >= 700;
        ToolbarMetaText.IsVisible = width >= 900;

        ApplyHeroLayout(width);
        ApplyContextLayout(width);
    }

    private void ApplyHeroLayout(double width)
    {
        ReportHeroGrid.ColumnDefinitions.Clear();
        ReportHeroGrid.RowDefinitions.Clear();

        if (width >= 900)
        {
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));
            ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ReportNextActionCard, 1);
            Grid.SetRow(ReportNextActionCard, 0);
            ReportNextActionCard.Margin = new Thickness(0);
            return;
        }

        ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetColumn(ReportNextActionCard, 0);
        Grid.SetRow(ReportNextActionCard, 1);
        ReportNextActionCard.Margin = new Thickness(0, 12, 0, 0);
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
            SetContextPosition(GeneratedContextItem, 0, 0);
            SetContextPosition(ProfileContextItem, 0, 1);
            SetContextPosition(MethodContextItem, 1, 0);
            SetContextPosition(RouteContextItem, 1, 1);
            return;
        }

        ReportContextGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var index = 0; index < items.Length; index++)
        {
            ReportContextGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetContextPosition(items[index], index, 0);
        }
    }

    private static void SetContextPosition(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, 1);
    }
}

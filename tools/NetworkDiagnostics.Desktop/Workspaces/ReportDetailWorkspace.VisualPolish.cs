using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportDetailWorkspace
{
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

    private void ApplyReportDetailPolish(double width)
    {
        HomeButton.IsVisible = false;
        ReportBody.Margin = width < 820
            ? new Thickness(18, 18, 18, 28)
            : new Thickness(28, 22, 28, 36);
        ReportBody.MaxWidth = 1320;
        ReportBody.Spacing = 18;

        ToolbarTitleText.IsVisible = width >= 760;
        ToolbarMetaText.IsVisible = width >= 900;

        var compact = width < 980;
        ReportHeroGrid.ColumnDefinitions.Clear();
        ReportHeroGrid.RowDefinitions.Clear();
        ReportLowerGrid.ColumnDefinitions.Clear();
        ReportLowerGrid.RowDefinitions.Clear();

        if (!compact)
        {
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
            ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ReportNextActionCard, 1);
            Grid.SetRow(ReportNextActionCard, 0);
            ReportNextActionCard.Margin = new Thickness(0);

            ReportLowerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.12, GridUnitType.Star)));
            ReportLowerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.88, GridUnitType.Star)));
            ReportLowerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ReportFindingsCard, 0);
            Grid.SetRow(ReportFindingsCard, 0);
            Grid.SetColumn(ReportSideStack, 1);
            Grid.SetRow(ReportSideStack, 0);
        }
        else
        {
            ReportHeroGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportHeroGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ReportNextActionCard, 0);
            Grid.SetRow(ReportNextActionCard, 1);
            ReportNextActionCard.Margin = new Thickness(0, 14, 0, 0);

            ReportLowerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportLowerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportLowerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ReportFindingsCard, 0);
            Grid.SetRow(ReportFindingsCard, 0);
            Grid.SetColumn(ReportSideStack, 0);
            Grid.SetRow(ReportSideStack, 1);
        }
    }
}

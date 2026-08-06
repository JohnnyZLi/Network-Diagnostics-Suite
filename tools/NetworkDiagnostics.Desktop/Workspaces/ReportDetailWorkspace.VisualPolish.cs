using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportDetailWorkspace
{
    private Grid? reportDetailRoot;
    private Border? reportToolbar;
    private StackPanel? reportBody;
    private Grid? reportHero;
    private Border? reportHeroContext;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        ResolveReportDetailLayout();
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

    private void ResolveReportDetailLayout()
    {
        reportDetailRoot ??= Content as Grid;
        if (reportDetailRoot is null) return;
        reportToolbar ??= reportDetailRoot.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        reportBody ??= reportDetailRoot
            .GetLogicalDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.MaxWidth >= 1300);
        reportHero ??= OutcomeIndicator.GetLogicalParent() as Grid;
        reportHeroContext ??= reportHero?.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetColumn(border) == 2);
    }

    private void ApplyReportDetailPolish(double width)
    {
        ResolveReportDetailLayout();
        if (reportDetailRoot is null) return;

        reportDetailRoot.RowDefinitions[0].Height = new GridLength(50);
        HomeButton.IsVisible = false;
        if (reportToolbar is not null)
        {
            reportToolbar.Padding = new Thickness(14, 0);
        }
        if (reportBody is not null)
        {
            reportBody.Margin = new Thickness(30, 24, 30, 36);
            reportBody.Spacing = 18;
            reportBody.MaxWidth = 1320;
        }

        VerdictText.FontSize = 30;
        VerdictText.LineHeight = 36;
        SummaryText.MaxWidth = 780;

        if (reportHero is null || reportHeroContext is null) return;
        var compact = width < 980;
        reportHero.ColumnDefinitions.Clear();
        reportHero.RowDefinitions.Clear();
        reportHero.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        reportHero.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (!compact)
        {
            reportHero.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(320)));
            reportHero.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(reportHeroContext, 2);
            Grid.SetRow(reportHeroContext, 0);
            reportHeroContext.Margin = new Thickness(0);
        }
        else
        {
            reportHero.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            reportHero.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(reportHeroContext, 1);
            Grid.SetRow(reportHeroContext, 1);
            reportHeroContext.Margin = new Thickness(0, 14, 0, 0);
        }
    }
}

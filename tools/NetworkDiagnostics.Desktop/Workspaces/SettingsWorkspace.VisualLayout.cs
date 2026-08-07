using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class SettingsWorkspace
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += VisualSettingsSizeChanged;
        ApplySettingsVisualLayout(Bounds.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= VisualSettingsSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualSettingsSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplySettingsVisualLayout(eventArgs.NewSize.Width);

    private void ApplySettingsVisualLayout(double width)
    {
        ApplyContentBounds(width);
        ApplyFieldSizing();
        ApplySettingsChromePolish(width);
        ApplyComponentPolish();
        ApplySettingsCardLayout(width);
    }

    private void ApplyContentBounds(double width)
    {
        SettingsContentContainer.MaxWidth = 1040;
        SettingsContentContainer.Margin = width < 760
            ? new Thickness(18, 20, 18, 28)
            : new Thickness(28, 24, 28, 36);
    }

    private void ApplyFieldSizing()
    {
        ExpectedDownloadTextBox.Width = double.NaN;
        ExpectedDownloadTextBox.MaxWidth = 320;
        ExpectedDownloadTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        ExpectedUploadTextBox.Width = double.NaN;
        ExpectedUploadTextBox.MaxWidth = 320;
        ExpectedUploadTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        AlertThresholdTextBox.Width = double.NaN;
        AlertThresholdTextBox.MaxWidth = 320;
        AlertThresholdTextBox.HorizontalAlignment = HorizontalAlignment.Left;
    }

    private void ApplySettingsChromePolish(double width)
    {
        SectionEyebrowText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionTitleText.FontSize = 26;
        SectionTitleText.LineHeight = 31;
        SectionTitleText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionTitleText.TextAlignment = TextAlignment.Left;
        SectionDetailText.FontSize = 11;
        SectionDetailText.LineHeight = 17;
        SectionDetailText.MaxWidth = 760;
        SectionDetailText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionDetailText.TextAlignment = TextAlignment.Left;

        var navigationBar = SettingsRootGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (navigationBar is not null)
        {
            var horizontalInset = width > 1080
                ? Math.Max(20, (width - 1040) / 2 + 20)
                : 20;
            navigationBar.Padding = new Thickness(horizontalInset, 8);
        }

        foreach (var card in SettingsRootGrid
                     .GetLogicalDescendants()
                     .OfType<Border>()
                     .Where(border => border.Classes.Contains("settingsCard")))
        {
            card.Padding = new Thickness(18);
            card.CornerRadius = new CornerRadius(12);
            card.BorderThickness = new Thickness(1);
        }

        foreach (var status in SettingsRootGrid
                     .GetLogicalDescendants()
                     .OfType<Border>()
                     .Where(border => border.Classes.Contains("settingsStatus")))
        {
            status.Padding = new Thickness(12);
            status.CornerRadius = new CornerRadius(9);
        }
    }

    private void ApplySettingsCardLayout(double width)
    {
        var columns = width >= 820 ? 2 : 1;
        ConfigureDirectCardGrids(GeneralPanel, columns);
        ConfigureDirectCardGrids(MonitoringPanel, columns);
        ConfigureDirectCardGrids(MeasurementPanel, columns);
        ConfigureDirectCardGrids(PrivacyPanel, columns);
        ConfigureDirectCardGrids(StoragePanel, columns);
        ConfigureDirectCardGrids(DeveloperPanel, columns);
    }

    private static void ConfigureDirectCardGrids(StackPanel panel, int maximumColumns)
    {
        foreach (var grid in panel.Children.OfType<Grid>())
        {
            var cards = grid.Children.OfType<Border>()
                .Where(border => border.Classes.Contains("settingsCard"))
                .ToArray();
            if (cards.Length < 2 || cards.Length != grid.Children.Count) continue;

            var columnCount = Math.Clamp(maximumColumns, 1, cards.Length);
            var rowCount = (int)Math.Ceiling(cards.Length / (double)columnCount);
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var column = 0; column < columnCount; column++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
            for (var row = 0; row < rowCount; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            grid.ColumnSpacing = 14;
            grid.RowSpacing = 14;

            for (var index = 0; index < cards.Length; index++)
            {
                Grid.SetColumn(cards[index], index % columnCount);
                Grid.SetRow(cards[index], index / columnCount);
                Grid.SetColumnSpan(cards[index], 1);
                Grid.SetRowSpan(cards[index], 1);
            }

            if (columnCount > 1 && cards.Length % columnCount == 1)
            {
                var finalCard = cards[^1];
                Grid.SetColumn(finalCard, 0);
                Grid.SetColumnSpan(finalCard, columnCount);
            }
        }
    }
}

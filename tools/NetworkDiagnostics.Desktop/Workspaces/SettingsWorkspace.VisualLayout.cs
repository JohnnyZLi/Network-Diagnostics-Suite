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
        ApplyFieldSizing();
        ApplySettingsChromePolish();
        ApplyComponentPolish();
        ApplySettingsCardLayout(Bounds.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= VisualSettingsSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualSettingsSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        ApplySettingsChromePolish();
        ApplyComponentPolish();
        ApplySettingsCardLayout(eventArgs.NewSize.Width);
    }

    private void ApplyFieldSizing()
    {
        SettingsContentContainer.MaxWidth = 1020;
        SettingsContentContainer.Margin = new Thickness(28, 24, 28, 32);

        ExpectedDownloadTextBox.Width = double.NaN;
        ExpectedDownloadTextBox.MaxWidth = 280;
        ExpectedDownloadTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        ExpectedUploadTextBox.Width = double.NaN;
        ExpectedUploadTextBox.MaxWidth = 280;
        ExpectedUploadTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        AlertThresholdTextBox.Width = double.NaN;
        AlertThresholdTextBox.MaxWidth = 280;
        AlertThresholdTextBox.HorizontalAlignment = HorizontalAlignment.Left;
    }

    private void ApplySettingsChromePolish()
    {
        SectionEyebrowText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionTitleText.FontSize = 26;
        SectionTitleText.LineHeight = 31;
        SectionTitleText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionTitleText.TextAlignment = TextAlignment.Left;
        SectionDetailText.MaxWidth = 760;
        SectionDetailText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionDetailText.TextAlignment = TextAlignment.Left;

        var navigationBar = SettingsRootGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (navigationBar is not null)
        {
            navigationBar.Padding = new Thickness(18, 8);
        }

        if (SettingsContentContainer.Children.OfType<StackPanel>().FirstOrDefault() is { } contentStack
            && contentStack.Children.OfType<Grid>().FirstOrDefault() is { } sectionHeader)
        {
            sectionHeader.ColumnDefinitions.Clear();
            sectionHeader.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            sectionHeader.ColumnSpacing = 0;
            foreach (var badge in sectionHeader.Children.OfType<Border>())
            {
                badge.IsVisible = false;
            }
        }

        foreach (var card in SettingsRootGrid
                     .GetLogicalDescendants()
                     .OfType<Border>()
                     .Where(border => border.Classes.Contains("settingsCard")))
        {
            card.Padding = new Thickness(16);
            card.CornerRadius = new CornerRadius(12);
        }

        foreach (var status in SettingsRootGrid
                     .GetLogicalDescendants()
                     .OfType<Border>()
                     .Where(border => border.Classes.Contains("settingsStatus")))
        {
            status.Padding = new Thickness(12);
            status.CornerRadius = new CornerRadius(10);
        }
    }

    private void ApplySettingsCardLayout(double width)
    {
        var columns = width >= 760 ? 2 : 1;
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
            var cards = grid.Children.OfType<Border>().ToArray();
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
            grid.ColumnSpacing = 12;
            grid.RowSpacing = 12;

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

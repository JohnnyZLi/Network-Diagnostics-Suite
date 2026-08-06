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
        ApplyContentBounds(Bounds.Width);
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
        ApplyContentBounds(eventArgs.NewSize.Width);
        ApplySettingsChromePolish();
        ApplyComponentPolish();
        ApplySettingsCardLayout(eventArgs.NewSize.Width);
    }

    private void ApplyContentBounds(double width)
    {
        SettingsContentContainer.MaxWidth = 980;
        SettingsContentContainer.Margin = width < 760
            ? new Thickness(18, 20, 18, 28)
            : new Thickness(26, 22, 26, 30);
    }

    private void ApplyFieldSizing()
    {
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
        SectionTitleText.FontSize = 24;
        SectionTitleText.LineHeight = 29;
        SectionTitleText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionTitleText.TextAlignment = TextAlignment.Left;
        SectionDetailText.MaxWidth = 720;
        SectionDetailText.HorizontalAlignment = HorizontalAlignment.Left;
        SectionDetailText.TextAlignment = TextAlignment.Left;

        var navigationBar = SettingsRootGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (navigationBar is not null)
        {
            navigationBar.Padding = new Thickness(16, 7);
            navigationBar.Background = Brushes.Transparent;
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

        // General is ordinary application preference UI, not a telemetry dashboard.
        // Remove the extra card boxes while retaining spacing and strong controls.
        foreach (var card in DirectCards(GeneralPanel))
        {
            card.Background = Brushes.Transparent;
            card.BorderThickness = new Thickness(0);
            card.Padding = new Thickness(4, 6);
            card.CornerRadius = new CornerRadius(0);
            card.BoxShadow = default;
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
        ConfigureDirectCardGrids(GeneralPanel, columns, generalPreferences: true);
        ConfigureDirectCardGrids(MonitoringPanel, columns);
        ConfigureDirectCardGrids(MeasurementPanel, columns);
        ConfigureDirectCardGrids(PrivacyPanel, columns);
        ConfigureDirectCardGrids(StoragePanel, columns);
        ConfigureDirectCardGrids(DeveloperPanel, columns);
    }

    private static IEnumerable<Border> DirectCards(StackPanel panel) =>
        panel.Children
            .OfType<Grid>()
            .SelectMany(grid => grid.Children.OfType<Border>())
            .Where(border => border.Classes.Contains("settingsCard"));

    private static void ConfigureDirectCardGrids(
        StackPanel panel,
        int maximumColumns,
        bool generalPreferences = false)
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
            grid.ColumnSpacing = generalPreferences ? 28 : 12;
            grid.RowSpacing = generalPreferences ? 18 : 12;

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

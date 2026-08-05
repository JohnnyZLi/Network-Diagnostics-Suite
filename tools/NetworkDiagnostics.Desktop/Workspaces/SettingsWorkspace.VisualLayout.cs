using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class SettingsWorkspace
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += VisualSettingsSizeChanged;
        ApplyFieldSizing();
        ApplySettingsCardLayout(Bounds.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= VisualSettingsSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualSettingsSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplySettingsCardLayout(eventArgs.NewSize.Width);

    private void ApplyFieldSizing()
    {
        SettingsContentContainer.MaxWidth = 1160;

        ExpectedDownloadTextBox.Width = 220;
        ExpectedDownloadTextBox.HorizontalAlignment = HorizontalAlignment.Left;
        ExpectedUploadTextBox.Width = 220;
        ExpectedUploadTextBox.HorizontalAlignment = HorizontalAlignment.Left;
        AlertThresholdTextBox.Width = 220;
        AlertThresholdTextBox.HorizontalAlignment = HorizontalAlignment.Left;
    }

    private void ApplySettingsCardLayout(double width)
    {
        var columns = width >= 1080 ? 3 : width >= 720 ? 2 : 1;
        ConfigureDirectCardGrids(GeneralPanel, columns);
        ConfigureDirectCardGrids(MonitoringPanel, Math.Min(columns, 2));
        ConfigureDirectCardGrids(MeasurementPanel, Math.Min(columns, 2));
        ConfigureDirectCardGrids(PrivacyPanel, Math.Min(columns, 2));
        ConfigureDirectCardGrids(StoragePanel, Math.Min(columns, 2));
        ConfigureDirectCardGrids(DeveloperPanel, Math.Min(columns, 2));
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
            }
        }
    }
}

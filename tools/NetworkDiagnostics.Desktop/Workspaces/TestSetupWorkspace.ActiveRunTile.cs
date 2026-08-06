using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private Border? testHubActiveRunTile;
    private Grid? testHubActiveMetricsGrid;
    private TextBlock? testHubActiveTitleText;
    private TextBlock? testHubActiveDetailText;
    private TextBlock? testHubActivePhaseText;
    private TextBlock? testHubActiveProgressText;
    private TextBlock? testHubActiveRateText;
    private TextBlock? testHubActiveLatencyText;
    private TextBlock? testHubActiveTransferText;
    private ProgressBar? testHubActiveProgressBar;
    private Button? testHubActiveStopButton;
    private bool activeRunTileInstalled;
    private bool activeRunTileVisible;
    private bool activeRunLayoutApplied;

    public event EventHandler? ActiveRunStopRequested;

    public void SetActiveRunTileState(ActiveRunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        activeRunTileVisible = snapshot.IsActive;
        EnsureActiveRunTile();

        if (snapshot.IsActive)
        {
            RenderActiveRunSnapshot(snapshot);
            ActiveRunBorder.IsVisible = false;
        }

        ApplyActiveRunTileLayout(testHubRoot?.Bounds.Width ?? Bounds.Width);
    }

    public void RenderActiveRunSnapshot(ActiveRunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        activeRunTileVisible = snapshot.IsActive;
        EnsureActiveRunTile();
        if (!snapshot.IsActive || testHubActiveRunTile is null) return;

        SetActiveText(
            testHubActiveTitleText,
            $"{DiagnosticReportPresenter.ProfileName(snapshot.Profile)} · {ActiveMethodName(snapshot.Method)}");
        SetActiveText(testHubActiveDetailText, snapshot.Detail);
        SetActiveText(testHubActivePhaseText, snapshot.Phase);
        SetActiveText(testHubActiveProgressText, $"{snapshot.Progress:0}%");
        SetActiveText(
            testHubActiveRateText,
            snapshot.LiveMbps is { } rate ? $"{FormatActiveValue(rate)} Mbps" : "—");
        SetActiveText(
            testHubActiveLatencyText,
            snapshot.LiveLatencyMs is { } latency ? $"{FormatActiveValue(latency)} ms" : "—");
        SetActiveText(
            testHubActiveTransferText,
            snapshot.BytesTransferred > 0
                ? $"{snapshot.BytesTransferred / 1_000_000d:0.0} MB"
                : "—");

        if (testHubActiveProgressBar is not null
            && Math.Abs(testHubActiveProgressBar.Value - snapshot.Progress) >= 0.05)
        {
            testHubActiveProgressBar.Value = snapshot.Progress;
        }

        if (testHubActiveStopButton is not null)
        {
            var canStop = snapshot.Status is ActiveRunStatus.Preparing or ActiveRunStatus.Running;
            testHubActiveStopButton.IsEnabled = canStop;
            var stopLabel = snapshot.Status == ActiveRunStatus.Cancelling ? "Stopping…" : "Stop test";
            if (!string.Equals(testHubActiveStopButton.Content?.ToString(), stopLabel, StringComparison.Ordinal))
            {
                testHubActiveStopButton.Content = stopLabel;
            }
        }

        ActiveRunBorder.IsVisible = false;
        ApplyActiveRunTileLayout(testHubRoot?.Bounds.Width ?? Bounds.Width);
    }

    public void BringActiveRunIntoView()
    {
        if (activeRunTileVisible)
        {
            testHubActiveRunTile?.BringIntoView();
        }
    }

    private void EnsureActiveRunTile()
    {
        if (activeRunTileInstalled || testHubTilesGrid is null || testHubRoot is null) return;

        testHubActiveTitleText = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            LineHeight = 23,
            TextWrapping = TextWrapping.Wrap
        };
        testHubActiveDetailText = new TextBlock
        {
            FontSize = 11,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 820
        };
        testHubActiveDetailText.Classes.Add("secondary");

        testHubActivePhaseText = ActiveMetricValue("Preparing");
        testHubActiveProgressText = ActiveMetricValue("0%");
        testHubActiveRateText = ActiveMetricValue("—");
        testHubActiveLatencyText = ActiveMetricValue("—");
        testHubActiveTransferText = ActiveMetricValue("—");

        testHubActiveMetricsGrid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        testHubActiveMetricsGrid.Children.Add(ActiveMetric("Phase", testHubActivePhaseText));
        testHubActiveMetricsGrid.Children.Add(ActiveMetric("Progress", testHubActiveProgressText));
        testHubActiveMetricsGrid.Children.Add(ActiveMetric("Live rate", testHubActiveRateText));
        testHubActiveMetricsGrid.Children.Add(ActiveMetric("Latency", testHubActiveLatencyText));
        testHubActiveMetricsGrid.Children.Add(ActiveMetric("Transferred", testHubActiveTransferText));

        testHubActiveProgressBar = new ProgressBar
        {
            Height = 5,
            Minimum = 0,
            Maximum = 100,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        testHubActiveStopButton = new Button
        {
            Content = "Stop test",
            MinHeight = 34,
            MinWidth = 104,
            Padding = new Thickness(13, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        testHubActiveStopButton.Classes.Add("secondary");
        testHubActiveStopButton.Click += ActiveRunStopClicked;

        var indicator = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center
        };
        indicator.Classes.Add("indicatorAccent");
        var eyebrow = new TextBlock
        {
            Text = "DIAGNOSTIC IN PROGRESS",
            VerticalAlignment = VerticalAlignment.Center
        };
        eyebrow.Classes.Add("eyebrow");
        var status = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        status.Children.Add(indicator);
        status.Children.Add(eyebrow);

        var heading = new StackPanel { Spacing = 5 };
        heading.Children.Add(status);
        heading.Children.Add(testHubActiveTitleText);
        heading.Children.Add(testHubActiveDetailText);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        header.Children.Add(heading);
        Grid.SetColumn(testHubActiveStopButton, 1);
        header.Children.Add(testHubActiveStopButton);

        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(header);
        content.Children.Add(testHubActiveProgressBar);
        content.Children.Add(testHubActiveMetricsGrid);

        testHubActiveRunTile = new Border
        {
            Padding = new Thickness(18, 16),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsVisible = false,
            Child = content
        };
        testHubActiveRunTile.Classes.Add("accentSurface");
        testHubTilesGrid.Children.Add(testHubActiveRunTile);
        testHubRoot.SizeChanged += ActiveRunTileSizeChanged;
        activeRunTileInstalled = true;
        ApplyActiveRunTileLayout(testHubRoot.Bounds.Width);
    }

    private void ActiveRunTileSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyActiveRunTileLayout(eventArgs.NewSize.Width);

    private void ApplyActiveRunTileLayout(double width)
    {
        if (!activeRunTileInstalled
            || testHubTilesGrid is null
            || testHubActiveRunTile is null
            || testHubSpeedTile is null
            || testHubDiagnosticTile is null
            || testHubActiveMetricsGrid is null)
        {
            return;
        }

        var resolvedWidth = width > 0 ? width : Bounds.Width;
        if (!activeRunTileVisible)
        {
            testHubActiveRunTile.IsVisible = false;
            testHubSpeedTile.IsVisible = true;
            testHubDiagnosticTile.IsVisible = true;
            if (activeRunLayoutApplied)
            {
                activeRunLayoutApplied = false;
                ApplyTestHubResponsiveLayout(resolvedWidth);
            }
            return;
        }

        testHubSpeedTile.IsVisible = false;
        testHubDiagnosticTile.IsVisible = false;
        testHubActiveRunTile.IsVisible = true;
        activeRunLayoutApplied = true;

        testHubTilesGrid.ColumnDefinitions.Clear();
        testHubTilesGrid.RowDefinitions.Clear();
        testHubTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        testHubTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        SetGridPosition(testHubActiveRunTile, 0, 0);

        var compactMetrics = resolvedWidth < 760;
        testHubActiveMetricsGrid.ColumnDefinitions.Clear();
        testHubActiveMetricsGrid.RowDefinitions.Clear();
        var columnCount = compactMetrics ? 2 : 5;
        var rowCount = compactMetrics ? 3 : 1;
        for (var column = 0; column < columnCount; column++)
        {
            testHubActiveMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }
        for (var row = 0; row < rowCount; row++)
        {
            testHubActiveMetricsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        for (var index = 0; index < testHubActiveMetricsGrid.Children.Count; index++)
        {
            Grid.SetColumn(testHubActiveMetricsGrid.Children[index], index % columnCount);
            Grid.SetRow(testHubActiveMetricsGrid.Children[index], index / columnCount);
        }
    }

    private static Border ActiveMetric(string label, TextBlock value)
    {
        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 9
        };
        labelText.Classes.Add("muted");
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(labelText);
        stack.Children.Add(value);
        var border = new Border
        {
            Padding = new Thickness(12, 9),
            Child = stack
        };
        border.Classes.Add("surfaceSubtle");
        return border;
    }

    private static TextBlock ActiveMetricValue(string value) => new()
    {
        Text = value,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    private void ActiveRunStopClicked(object? sender, RoutedEventArgs eventArgs) =>
        ActiveRunStopRequested?.Invoke(this, EventArgs.Empty);

    private static void SetActiveText(TextBlock? block, string value)
    {
        if (block is not null && !string.Equals(block.Text, value, StringComparison.Ordinal))
        {
            block.Text = value;
        }
    }

    private static string ActiveMethodName(TransferMethod method) => method switch
    {
        TransferMethod.Single => "Single",
        TransferMethod.Aggregate => "Aggregate",
        _ => "Compare"
    };

    private static string FormatActiveValue(double value) =>
        value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture);
}

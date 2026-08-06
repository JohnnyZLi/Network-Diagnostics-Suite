using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Monitoring;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private const int MinimumTimelineSlots = 48;
    private const string SparseTimelineHintName = "SparseTimelineHint";
    private Button? sevenDaysButton;
    private bool speedActionsPolished;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += VisualLayoutSizeChanged;
        LayoutUpdated += RenderedLayoutUpdated;
        InstallDiagnosticLauncher();
        EnsureSevenDayButton();
        PolishRangeSelector();
        ApplyRenderedVisualLayout(Bounds.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= VisualLayoutSizeChanged;
        LayoutUpdated -= RenderedLayoutUpdated;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualLayoutSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyRenderedVisualLayout(eventArgs.NewSize.Width);

    private void RenderedLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        EnsureSevenDayButton();
        PolishRangeSelector();
        PolishSpeedActions();
        NormalizeSparseTimeline(ResponsivenessTimelineGrid, colorByLatency: true);
        NormalizeSparseTimeline(ReliabilityTimelineGrid, colorByLatency: false);
        SyncSevenDaySelection();
    }

    private void ApplyRenderedVisualLayout(double width)
    {
        var available = Math.Max(720, width - 48);
        OverviewGrid.Width = Math.Min(1360, available);

        DiagnosticExpander.Background = Brushes.Transparent;
        DiagnosticDetailsGrid.Background = Brushes.Transparent;
        ProfileGrid.Background = Brushes.Transparent;

        CompareMethodButton.Padding = new Thickness(5, 5);
        SingleMethodButton.Padding = new Thickness(5, 5);
        AggregateMethodButton.Padding = new Thickness(5, 5);
        CompareMethodButton.FontSize = 10;
        SingleMethodButton.FontSize = 10;
        AggregateMethodButton.FontSize = 10;

        if (width < 1180) return;

        DiagnosticDetailsGrid.ColumnDefinitions.Clear();
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.25, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.8, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
    }

    private void PolishRangeSelector()
    {
        var buttons = new[]
        {
            OneMinuteButton,
            FiveMinutesButton,
            OneHourButton,
            TwentyFourHoursButton,
            sevenDaysButton
        }.Where(button => button is not null).Cast<Button>().ToArray();

        if (buttons.Length == 0) return;
        if (buttons[0].GetVisualParent() is StackPanel rangePanel)
        {
            rangePanel.Spacing = 0;
            rangePanel.VerticalAlignment = VerticalAlignment.Center;
            if (rangePanel.GetVisualParent() is Border rangeSurface)
            {
                rangeSurface.Padding = new Thickness(3);
                rangeSurface.CornerRadius = new CornerRadius(10);
                rangeSurface.VerticalAlignment = VerticalAlignment.Bottom;
            }
        }

        foreach (var button in buttons)
        {
            button.Height = 32;
            button.MinHeight = 32;
            button.MinWidth = button == TwentyFourHoursButton ? 84 : 68;
            button.Padding = new Thickness(10, 0);
            button.CornerRadius = new CornerRadius(7);
            button.FontSize = 10;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }

    private void PolishSpeedActions()
    {
        if (speedActionsPolished) return;

        var buttons = this.GetVisualDescendants()
            .OfType<Button>()
            .ToDictionary(button => button.Content?.ToString() ?? string.Empty);
        if (!buttons.TryGetValue("Run content test", out var contentButton)
            || !buttons.TryGetValue("Run peak test", out var peakButton)
            || !buttons.TryGetValue("Speed settings", out var settingsButton)
            || contentButton.GetVisualParent() is not StackPanel actionPanel
            || actionPanel.GetVisualParent() is not StackPanel speedPanel)
        {
            return;
        }

        var actionIndex = speedPanel.Children.IndexOf(actionPanel);
        if (actionIndex < 0) return;

        actionPanel.Children.Remove(contentButton);
        actionPanel.Children.Remove(peakButton);
        actionPanel.Children.Remove(settingsButton);
        speedPanel.Children.Remove(actionPanel);

        contentButton.MinWidth = 132;
        contentButton.Height = 36;
        contentButton.MinHeight = 36;
        contentButton.Padding = new Thickness(13, 0);
        contentButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        contentButton.VerticalContentAlignment = VerticalAlignment.Center;

        peakButton.MinWidth = 132;
        peakButton.Height = 36;
        peakButton.MinHeight = 36;
        peakButton.Padding = new Thickness(13, 0);
        peakButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        peakButton.VerticalContentAlignment = VerticalAlignment.Center;

        settingsButton.Height = 36;
        settingsButton.MinHeight = 36;
        settingsButton.Padding = new Thickness(8, 0);
        settingsButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        settingsButton.VerticalContentAlignment = VerticalAlignment.Center;

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 8,
            Margin = new Thickness(0, 3, 0, 0)
        };

        Grid.SetColumn(settingsButton, 0);
        Grid.SetColumn(contentButton, 2);
        Grid.SetColumn(peakButton, 3);
        footer.Children.Add(settingsButton);
        footer.Children.Add(contentButton);
        footer.Children.Add(peakButton);
        speedPanel.Children.Insert(actionIndex, footer);
        speedActionsPolished = true;
    }

    private void EnsureSevenDayButton()
    {
        if (sevenDaysButton is not null
            || TwentyFourHoursButton.GetVisualParent() is not StackPanel rangePanel)
        {
            return;
        }

        sevenDaysButton = new Button
        {
            Content = "7 days",
            Tag = "7d"
        };
        sevenDaysButton.Classes.Add("range");
        sevenDaysButton.Click += SevenDaysClicked;
        rangePanel.Children.Add(sevenDaysButton);
    }

    private void SevenDaysClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        SetSelected(OneMinuteButton, false);
        SetSelected(FiveMinutesButton, false);
        SetSelected(OneHourButton, false);
        SetSelected(TwentyFourHoursButton, false);
        if (sevenDaysButton is not null) SetSelected(sevenDaysButton, true);
        MonitorWindowRequested?.Invoke(this, new MonitorWindowRequestedEventArgs(MonitorWindow.SevenDays));
    }

    private void SyncSevenDaySelection()
    {
        if (sevenDaysButton is null) return;
        var shorterWindowSelected = OneMinuteButton.Classes.Contains("selected")
            || FiveMinutesButton.Classes.Contains("selected")
            || OneHourButton.Classes.Contains("selected")
            || TwentyFourHoursButton.Classes.Contains("selected");
        SetSelected(sevenDaysButton, !shorterWindowSelected);
    }

    private static void NormalizeSparseTimeline(Grid grid, bool colorByLatency)
    {
        var bars = grid.Children.OfType<Border>().ToArray();
        if (bars.Length == 0)
        {
            RemoveSparseTimelineHint(grid);
            return;
        }

        if (IsIntentionalEmptyTimeline(bars))
        {
            RemoveSparseTimelineHint(grid);
            return;
        }

        while (grid.ColumnDefinitions.Count < MinimumTimelineSlots)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        var offset = Math.Max(0, grid.ColumnDefinitions.Count - bars.Length);
        for (var index = 0; index < bars.Length; index++)
        {
            Grid.SetColumn(bars[index], offset + index);
        }

        UpdateSparseTimelineHint(grid, bars.Length, colorByLatency);

        if (!colorByLatency) return;
        foreach (var bar in bars)
        {
            AlignBarGeometryAndTone(bar);
        }
    }

    private static void UpdateSparseTimelineHint(Grid grid, int sampleCount, bool colorByLatency)
    {
        var existing = grid.Children
            .OfType<TextBlock>()
            .FirstOrDefault(item => item.Name == SparseTimelineHintName);
        var shouldShow = colorByLatency && sampleCount is > 0 and < 4;
        if (!shouldShow)
        {
            if (existing is not null) grid.Children.Remove(existing);
            return;
        }

        var hint = existing ?? new TextBlock
        {
            Name = SparseTimelineHintName,
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
            Opacity = 0.82
        };
        hint.Classes.Add("muted");
        hint.Text = sampleCount == 1
            ? "Collecting history · 1 sample"
            : $"Collecting history · {sampleCount} samples";
        Grid.SetColumn(hint, 0);
        Grid.SetColumnSpan(hint, Math.Max(1, grid.ColumnDefinitions.Count));
        if (existing is null) grid.Children.Add(hint);
    }

    private static void RemoveSparseTimelineHint(Grid grid)
    {
        var existing = grid.Children
            .OfType<TextBlock>()
            .FirstOrDefault(item => item.Name == SparseTimelineHintName);
        if (existing is not null) grid.Children.Remove(existing);
    }

    private static bool IsIntentionalEmptyTimeline(IReadOnlyList<Border> bars) =>
        bars.Count == 1
        && bars[0].Classes.Contains("timelineInactive")
        && ToolTip.GetTip(bars[0]) is null
        && bars[0].CornerRadius.TopLeft >= 4;

    private static void AlignBarGeometryAndTone(Border bar)
    {
        var tooltip = ToolTip.GetTip(bar)?.ToString();
        if (string.IsNullOrWhiteSpace(tooltip)) return;
        var token = tooltip.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var latency)) return;

        bar.Height = Math.Clamp(8 + latency / 4, 10, 58);
        RemoveScoreClasses(bar);
        bar.Classes.Add(latency switch
        {
            <= 50 => "scoreExcellent",
            <= 100 => "scoreGood",
            <= 180 => "scoreFair",
            _ => "scoreDegraded"
        });
    }
}

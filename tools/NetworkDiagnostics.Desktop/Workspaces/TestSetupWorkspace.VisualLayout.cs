using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Monitoring;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private const int MinimumTimelineSlots = 48;
    private Button? sevenDaysButton;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += VisualLayoutSizeChanged;
        LayoutUpdated += RenderedLayoutUpdated;
        EnsureSevenDayButton();
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

    private void PolishSpeedActions()
    {
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
        {
            switch (button.Content?.ToString())
            {
                case "Run content test":
                case "Run peak test":
                    if (!button.Classes.Contains("speedAction")) button.Classes.Add("speedAction");
                    button.MinWidth = 126;
                    break;
                case "Speed settings":
                    button.Classes.Remove("ghost");
                    if (!button.Classes.Contains("linkAction")) button.Classes.Add("linkAction");
                    break;
            }
        }
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
        if (bars.Length == 0 || grid.ColumnDefinitions.Count != bars.Length) return;
        if (IsIntentionalEmptyTimeline(bars)) return;

        if (grid.ColumnDefinitions.Count < MinimumTimelineSlots)
        {
            while (grid.ColumnDefinitions.Count < MinimumTimelineSlots)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            var offset = MinimumTimelineSlots - bars.Length;
            for (var index = 0; index < bars.Length; index++)
            {
                Grid.SetColumn(bars[index], offset + index);
            }
        }

        if (!colorByLatency) return;
        foreach (var bar in bars)
        {
            AlignBarGeometryAndTone(bar);
        }
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

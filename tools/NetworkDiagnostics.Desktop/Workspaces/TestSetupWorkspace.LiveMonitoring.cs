using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private const int StableTimelineSlots = 72;
    private static readonly string[] ScoreClassNames =
    [
        "scoreExcellent", "scoreGood", "scoreFair", "scoreDegraded", "scorePoor", "scoreUnavailable",
        "scoreSoftExcellent", "scoreSoftGood", "scoreSoftFair", "scoreSoftDegraded", "scoreSoftPoor",
        "timelineInactive"
    ];

    private readonly Dictionary<Grid, MetricGridState> stableMetricGrids = new();
    private TimelineGridState? stableResponsivenessTimeline;
    private TimelineGridState? stableReliabilityTimeline;
    private AlertListState? stableAlertList;

    public void RenderMonitoringSnapshot(NetworkExperiencePresentation experience)
    {
        ArgumentNullException.ThrowIfNull(experience);

        SetTextStable(DeviceNameText, experience.DeviceName);
        SetTextStable(InterfaceNameText, experience.InterfaceName);
        SetTextStable(LastUpdatedText, experience.LastUpdated);
        SetTextStable(
            OverallScoreText,
            experience.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—");
        SetTextStable(OverallStatusText, experience.Status);
        SetTextStable(OverallSummaryText, experience.Summary);
        SetTextStable(ResponsivenessScoreText, ScoreText(experience.Responsiveness.Score));
        SetTextStable(ReliabilityScoreText, ScoreText(experience.Reliability.Score));
        SetTextStable(SpeedScoreText, ScoreText(experience.Speed.Score));
        SetContentStable(
            MonitoringToggleButton,
            experience.MonitoringEnabled ? "Pause monitoring" : "Resume monitoring");

        ApplyScoreClassStable(ScoreRing, experience.Band, soft: false);
        ApplyScoreClassStable(ScoreAura, experience.Band, soft: true);
        ApplyScoreClassStable(ScoreOrbCore, experience.Band, soft: true);
        RenderComponentStable(
            experience.Responsiveness,
            ResponsivenessBadge,
            ResponsivenessStatusText,
            ResponsivenessSummaryText,
            ResponsivenessMetricsGrid);
        RenderComponentStable(
            experience.Reliability,
            ReliabilityBadge,
            ReliabilityStatusText,
            ReliabilitySummaryText,
            ReliabilityMetricsGrid);
        RenderComponentStable(
            experience.Speed,
            SpeedBadge,
            SpeedStatusText,
            SpeedSummaryText,
            SpeedMetricsGrid);
        RenderTimelineStable(ResponsivenessTimelineGrid, experience.Timeline, responsiveness: true);
        RenderTimelineStable(ReliabilityTimelineGrid, experience.Timeline, responsiveness: false);
        RenderAlertsStable(experience.Alerts, experience.UnreadAlertCount);
        SelectWindowStable(experience.Window);
        RefreshModelDependentVisuals();
    }

    public void RefreshModelDependentVisuals()
    {
        SyncDiagnosticLauncherState();
        SyncTestHubLayout();
        SyncSevenDaySelection();
        RefreshPolishedConfiguratorVisualState();
    }

    private void RenderComponentStable(
        ExperienceComponentPresentation component,
        Border badge,
        TextBlock status,
        TextBlock summary,
        Grid metricsGrid)
    {
        SetTextStable(
            status,
            component.Score is null ? component.Status : $"{component.Score} · {component.Status}");
        SetTextStable(summary, component.Summary);
        ApplyScoreClassStable(badge, component.Band, soft: true);

        var state = EnsureMetricGridState(metricsGrid);
        var metrics = component.Metrics.Take(4).ToArray();
        state.EmptyText.IsVisible = metrics.Length == 0;

        for (var index = 0; index < state.Cells.Length; index++)
        {
            var cell = state.Cells[index];
            var visible = index < metrics.Length;
            cell.Root.IsVisible = visible;
            SetClassStable(cell.Root, "last", visible && index == metrics.Length - 1);
            if (!visible) continue;

            var metric = metrics[index];
            SetTextStable(cell.Label, metric.Label);
            SetTextStable(cell.Value, metric.Value);
            SetToolTipStable(cell.Label, metric.Label);
            SetToolTipStable(cell.Value, metric.Value);
        }
    }

    private MetricGridState EnsureMetricGridState(Grid grid)
    {
        if (stableMetricGrids.TryGetValue(grid, out var existing)
            && existing.Cells.All(cell => grid.Children.Contains(cell.Root))
            && grid.Children.Contains(existing.EmptyText))
        {
            return existing;
        }

        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        for (var index = 0; index < 4; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        var empty = new TextBlock
        {
            Text = "Waiting for measurements",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        empty.Classes.Add("muted");
        Grid.SetColumnSpan(empty, 4);
        grid.Children.Add(empty);

        var cells = new MetricCellState[4];
        for (var index = 0; index < cells.Length; index++)
        {
            var label = new TextBlock
            {
                FontSize = 9,
                Height = 15,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label.Classes.Add("muted");

            var value = new TextBlock
            {
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                LineHeight = 18,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var content = new Grid
            {
                RowDefinitions = new RowDefinitions("15,Auto"),
                RowSpacing = 2
            };
            content.Children.Add(label);
            Grid.SetRow(value, 1);
            content.Children.Add(value);

            var root = new Border
            {
                Child = content,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsVisible = false
            };
            root.Classes.Add("telemetryMetric");
            Grid.SetColumn(root, index);
            grid.Children.Add(root);
            cells[index] = new MetricCellState(root, label, value);
        }

        var state = new MetricGridState(cells, empty);
        stableMetricGrids[grid] = state;
        return state;
    }

    private void RenderTimelineStable(
        Grid grid,
        IReadOnlyList<MonitorSample> source,
        bool responsiveness)
    {
        var state = EnsureTimelineState(grid, responsiveness);
        var samples = source.Count <= StableTimelineSlots
            ? source
            : source.Skip(source.Count - StableTimelineSlots).ToArray();
        var start = StableTimelineSlots - samples.Count;

        var measuredLatencies = responsiveness
            ? samples
                .Where(sample => sample.LatencyMs is not null)
                .Select(sample => sample.LatencyMs!.Value)
                .OrderBy(value => value)
                .ToArray()
            : Array.Empty<double>();
        var chartCeiling = measuredLatencies.Length == 0
            ? 80d
            : Math.Max(80d, Percentile(measuredLatencies, 0.95) * 1.15);

        for (var slot = 0; slot < state.Bars.Length; slot++)
        {
            var bar = state.Bars[slot];
            if (slot < start)
            {
                bar.IsVisible = false;
                continue;
            }

            var sample = samples[slot - start];
            bar.IsVisible = true;
            if (responsiveness)
            {
                bar.Height = sample.State switch
                {
                    MonitorSampleState.Unresponsive => 66,
                    MonitorSampleState.Inactive => 5,
                    _ => Math.Clamp(8 + ((sample.LatencyMs ?? 0) / chartCeiling * 54), 8, 64)
                };
            }

            var tooltip = sample.State == MonitorSampleState.Unresponsive
                ? $"Unreachable · {sample.Timestamp.ToLocalTime():h:mm:ss tt}"
                : $"{sample.LatencyMs:0.#} ms · {sample.Timestamp.ToLocalTime():h:mm:ss tt}";
            SetToolTipStable(bar, tooltip);
            SetExclusiveScoreClass(bar, SampleClass(sample));
        }

        var showHint = responsiveness && samples.Count is > 0 and < 4;
        state.Hint.IsVisible = showHint;
        if (showHint)
        {
            SetTextStable(
                state.Hint,
                samples.Count == 1
                    ? "Collecting history · 1 sample"
                    : $"Collecting history · {samples.Count} samples");
        }
    }

    private TimelineGridState EnsureTimelineState(Grid grid, bool responsiveness)
    {
        var existing = responsiveness ? stableResponsivenessTimeline : stableReliabilityTimeline;
        if (existing is not null
            && existing.Bars.All(grid.Children.Contains)
            && grid.Children.Contains(existing.Hint))
        {
            return existing;
        }

        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        var bars = new Border[StableTimelineSlots];
        for (var index = 0; index < StableTimelineSlots; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var bar = new Border
            {
                MinWidth = 2,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = responsiveness ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
                Height = responsiveness ? 8 : double.NaN,
                IsVisible = false
            };
            Grid.SetColumn(bar, index);
            grid.Children.Add(bar);
            bars[index] = bar;
        }

        var hint = new TextBlock
        {
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
            Opacity = 0.82,
            IsVisible = false
        };
        hint.Classes.Add("muted");
        Grid.SetColumn(hint, 0);
        Grid.SetColumnSpan(hint, StableTimelineSlots);
        Avalonia.Controls.Panel.SetZIndex(hint, 1);
        grid.Children.Add(hint);

        var state = new TimelineGridState(bars, hint);
        if (responsiveness)
        {
            stableResponsivenessTimeline = state;
        }
        else
        {
            stableReliabilityTimeline = state;
        }
        return state;
    }

    private void RenderAlertsStable(IReadOnlyList<MonitorAlert> alerts, int unreadCount)
    {
        var state = EnsureAlertListState();
        SetTextStable(
            AlertSummaryText,
            unreadCount == 0
                ? alerts.Count == 0 ? "No recent alerts" : "All caught up"
                : $"{unreadCount} unread alert{(unreadCount == 1 ? "" : "s")}");
        MarkAlertsReadButton.IsVisible = unreadCount > 0;
        ClearAlertsButton.IsVisible = alerts.Count > 0;
        state.EmptyText.IsVisible = alerts.Count == 0;

        for (var index = 0; index < state.Rows.Length; index++)
        {
            var row = state.Rows[index];
            var visible = index < alerts.Count && index < 4;
            row.Root.IsVisible = visible;
            if (!visible) continue;

            var alert = alerts[index];
            SetExclusiveScoreClass(row.Indicator, AlertClass(alert.Severity));
            SetTextStable(row.Title, alert.Title);
            row.Title.FontWeight = alert.IsRead ? FontWeight.Normal : FontWeight.SemiBold;
            SetTextStable(row.Detail, alert.Detail);
            SetTextStable(row.Time, alert.Timestamp.ToLocalTime().ToString("MMM d · h:mm tt"));
        }
    }

    private AlertListState EnsureAlertListState()
    {
        if (stableAlertList is not null
            && stableAlertList.Rows.All(row => AlertsPanel.Children.Contains(row.Root))
            && AlertsPanel.Children.Contains(stableAlertList.EmptyText))
        {
            return stableAlertList;
        }

        AlertsPanel.Children.Clear();
        var empty = new TextBlock
        {
            Text = "Outages, score drops, network changes, and bandwidth changes will appear here.",
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap
        };
        empty.Classes.Add("muted");
        AlertsPanel.Children.Add(empty);

        var rows = new AlertRowState[4];
        for (var index = 0; index < rows.Length; index++)
        {
            var indicator = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            var title = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            var detail = new TextBlock
            {
                FontSize = 9,
                LineHeight = 14,
                TextWrapping = TextWrapping.Wrap
            };
            detail.Classes.Add("muted");
            var time = new TextBlock { FontSize = 9 };
            time.Classes.Add("muted");

            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(title);
            content.Children.Add(detail);
            content.Children.Add(time);

            var root = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8,
                IsVisible = false
            };
            root.Children.Add(indicator);
            Grid.SetColumn(content, 1);
            root.Children.Add(content);
            AlertsPanel.Children.Add(root);
            rows[index] = new AlertRowState(root, indicator, title, detail, time);
        }

        stableAlertList = new AlertListState(rows, empty);
        return stableAlertList;
    }

    private void SelectWindowStable(MonitorWindow window)
    {
        SetSelectedStable(OneMinuteButton, window == MonitorWindow.OneMinute);
        SetSelectedStable(FiveMinutesButton, window == MonitorWindow.FiveMinutes);
        SetSelectedStable(OneHourButton, window == MonitorWindow.OneHour);
        SetSelectedStable(TwentyFourHoursButton, window == MonitorWindow.TwentyFourHours);
        if (sevenDaysButton is not null)
        {
            SetSelectedStable(sevenDaysButton, window == MonitorWindow.SevenDays);
        }
    }

    private static void ApplyScoreClassStable(Border border, ExperienceBand band, bool soft)
    {
        var desired = (band, soft) switch
        {
            (ExperienceBand.Excellent, false) => "scoreExcellent",
            (ExperienceBand.Good, false) => "scoreGood",
            (ExperienceBand.Fair, false) => "scoreFair",
            (ExperienceBand.Degraded, false) => "scoreDegraded",
            (ExperienceBand.Poor, false) => "scorePoor",
            (ExperienceBand.Excellent, true) => "scoreSoftExcellent",
            (ExperienceBand.Good, true) => "scoreSoftGood",
            (ExperienceBand.Fair, true) => "scoreSoftFair",
            (ExperienceBand.Degraded, true) => "scoreSoftDegraded",
            (ExperienceBand.Poor, true) => "scoreSoftPoor",
            _ => "scoreUnavailable"
        };
        SetExclusiveScoreClass(border, desired);
    }

    private static string SampleClass(MonitorSample sample) => sample.State switch
    {
        MonitorSampleState.Unresponsive => "scorePoor",
        MonitorSampleState.Laggy => "scoreDegraded",
        MonitorSampleState.Responsive when sample.LatencyMs <= 50 => "scoreExcellent",
        MonitorSampleState.Responsive => "scoreGood",
        _ => "timelineInactive"
    };

    private static string AlertClass(MonitorAlertSeverity severity) => severity switch
    {
        MonitorAlertSeverity.Critical => "scorePoor",
        MonitorAlertSeverity.Warning => "scoreDegraded",
        _ => "scoreExcellent"
    };

    private static void SetExclusiveScoreClass(Border border, string desired)
    {
        if (border.Classes.Contains(desired)) return;
        foreach (var className in ScoreClassNames)
        {
            border.Classes.Remove(className);
        }
        border.Classes.Add(desired);
    }

    private static void SetClassStable(Control control, string className, bool enabled)
    {
        var present = control.Classes.Contains(className);
        if (enabled == present) return;
        if (enabled) control.Classes.Add(className);
        else control.Classes.Remove(className);
    }

    private static void SetSelectedStable(Button button, bool selected) =>
        SetClassStable(button, "selected", selected);

    private static void SetTextStable(TextBlock text, string value)
    {
        if (!string.Equals(text.Text, value, StringComparison.Ordinal))
        {
            text.Text = value;
        }
    }

    private static void SetContentStable(ContentControl control, object value)
    {
        if (!Equals(control.Content, value))
        {
            control.Content = value;
        }
    }

    private static void SetToolTipStable(Control control, string value)
    {
        if (!string.Equals(ToolTip.GetTip(control)?.ToString(), value, StringComparison.Ordinal))
        {
            ToolTip.SetTip(control, value);
        }
    }

    private sealed record MetricCellState(Border Root, TextBlock Label, TextBlock Value);
    private sealed record MetricGridState(MetricCellState[] Cells, TextBlock EmptyText);
    private sealed record TimelineGridState(Border[] Bars, TextBlock Hint);
    private sealed record AlertRowState(
        Grid Root,
        Border Indicator,
        TextBlock Title,
        TextBlock Detail,
        TextBlock Time);
    private sealed record AlertListState(AlertRowState[] Rows, TextBlock EmptyText);
}

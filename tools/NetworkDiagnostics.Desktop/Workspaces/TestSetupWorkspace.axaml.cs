using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Monitoring;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed record TestSetupWorkspaceModel(
    int ProfileIndex,
    int MethodIndex,
    string Question,
    string Purpose,
    string MethodDetail,
    string EstimatedTime,
    string TransferCap,
    string Confirmation,
    string Availability,
    string Interface,
    string Endpoint,
    string Network,
    bool RunActive,
    string ActiveRunTitle,
    string ActiveRunDetail,
    double ActiveRunProgress,
    NetworkExperiencePresentation Experience);

public sealed class IndexRequestedEventArgs(int index) : EventArgs
{
    public int Index { get; } = index;
}

public sealed class MonitorWindowRequestedEventArgs(MonitorWindow window) : EventArgs
{
    public MonitorWindow Window { get; } = window;
}

public sealed partial class TestSetupWorkspace : UserControl
{
    public TestSetupWorkspace()
    {
        InitializeComponent();
        SizeChanged += WorkspaceSizeChanged;
    }

    public event EventHandler<IndexRequestedEventArgs>? ProfileRequested;

    public event EventHandler<IndexRequestedEventArgs>? MethodRequested;

    public event EventHandler? RunRequested;

    public event EventHandler? ActiveRunRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler<MonitorWindowRequestedEventArgs>? MonitorWindowRequested;

    public event EventHandler? MonitoringToggleRequested;

    public event EventHandler? ContentSpeedRequested;

    public event EventHandler? PeakSpeedRequested;

    public event EventHandler? MarkAlertsReadRequested;

    public event EventHandler? ClearAlertsRequested;

    public void Render(TestSetupWorkspaceModel model)
    {
        SetSelected(ConnectionProfileButton, model.ProfileIndex == 0);
        SetSelected(QuickProfileButton, model.ProfileIndex == 1);
        SetSelected(FullProfileButton, model.ProfileIndex == 2);
        SetSelected(StressProfileButton, model.ProfileIndex == 3);
        SetSelected(CompareMethodButton, model.MethodIndex == 0);
        SetSelected(SingleMethodButton, model.MethodIndex == 1);
        SetSelected(AggregateMethodButton, model.MethodIndex == 2);

        QuestionText.Text = model.Question;
        PurposeText.Text = model.Purpose;
        MethodDetailText.Text = model.MethodDetail;
        EstimatedTimeText.Text = model.EstimatedTime;
        TransferCapText.Text = model.TransferCap;
        ConfirmationText.Text = model.Confirmation;
        AvailabilityText.Text = model.Availability;
        InterfaceText.Text = $"Interface · {Fallback(model.Interface, "Automatic")}";
        EndpointText.Text = $"Endpoint · {Fallback(model.Endpoint, "Checking")}";
        NetworkText.Text = $"Network · {Fallback(model.Network, "Unknown")}";

        ActiveRunBorder.IsVisible = model.RunActive;
        ActiveRunTitleText.Text = model.ActiveRunTitle;
        ActiveRunDetailText.Text = model.ActiveRunDetail;
        ActiveRunProgress.Value = Math.Clamp(model.ActiveRunProgress, 0, 100);
        RunButton.IsEnabled = !model.RunActive;
        RunButton.Content = model.RunActive ? "Diagnostic running" : "Run diagnostic";

        RenderExperience(model.Experience);
    }

    private void RenderExperience(NetworkExperiencePresentation experience)
    {
        DeviceNameText.Text = experience.DeviceName;
        InterfaceNameText.Text = experience.InterfaceName;
        LastUpdatedText.Text = experience.LastUpdated;
        OverallScoreText.Text = experience.Score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
        OverallStatusText.Text = experience.Status;
        OverallSummaryText.Text = experience.Summary;
        ResponsivenessScoreText.Text = ScoreText(experience.Responsiveness.Score);
        ReliabilityScoreText.Text = ScoreText(experience.Reliability.Score);
        SpeedScoreText.Text = ScoreText(experience.Speed.Score);
        MonitoringToggleButton.Content = experience.MonitoringEnabled ? "Pause monitoring" : "Resume monitoring";

        ApplyScoreClass(ScoreRing, experience.Band, soft: false);
        ApplyScoreClass(ScoreAura, experience.Band, soft: true);
        ApplyScoreClass(ScoreOrbCore, experience.Band, soft: true);
        RenderComponent(
            experience.Responsiveness,
            ResponsivenessBadge,
            ResponsivenessStatusText,
            ResponsivenessSummaryText,
            ResponsivenessMetricsGrid);
        RenderComponent(
            experience.Reliability,
            ReliabilityBadge,
            ReliabilityStatusText,
            ReliabilitySummaryText,
            ReliabilityMetricsGrid);
        RenderComponent(
            experience.Speed,
            SpeedBadge,
            SpeedStatusText,
            SpeedSummaryText,
            SpeedMetricsGrid);
        RenderResponsivenessTimeline(experience.Timeline);
        RenderReliabilityTimeline(experience.Timeline);
        RenderAlerts(experience.Alerts, experience.UnreadAlertCount);
        SelectWindow(experience.Window);
    }

    private static string ScoreText(int? score) =>
        score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    private static void RenderComponent(
        ExperienceComponentPresentation component,
        Border badge,
        TextBlock status,
        TextBlock summary,
        Grid metricsGrid)
    {
        status.Text = component.Score is null ? component.Status : $"{component.Score} · {component.Status}";
        summary.Text = component.Summary;
        ApplyScoreClass(badge, component.Band, soft: true);
        metricsGrid.Children.Clear();
        metricsGrid.ColumnDefinitions.Clear();

        var metrics = component.Metrics.Take(4).ToArray();
        if (metrics.Length == 0)
        {
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var empty = new TextBlock
            {
                Text = "Waiting for measurements",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            empty.Classes.Add("muted");
            metricsGrid.Children.Add(empty);
            return;
        }

        for (var index = 0; index < metrics.Length; index++)
        {
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var metric = metrics[index];
            var label = new TextBlock
            {
                Text = metric.Label,
                FontSize = 9,
                Height = 15,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label.Classes.Add("muted");
            ToolTip.SetTip(label, metric.Label);

            var value = new TextBlock
            {
                Text = metric.Value,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                LineHeight = 18,
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ToolTip.SetTip(value, metric.Value);

            var content = new Grid
            {
                RowDefinitions = new RowDefinitions("15,Auto"),
                RowSpacing = 2
            };
            content.Children.Add(label);
            Grid.SetRow(value, 1);
            content.Children.Add(value);

            var cell = new Border
            {
                Child = content,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            cell.Classes.Add("telemetryMetric");
            if (index == metrics.Length - 1) cell.Classes.Add("last");
            Grid.SetColumn(cell, index);
            metricsGrid.Children.Add(cell);
        }
    }

    private void RenderResponsivenessTimeline(IReadOnlyList<MonitorSample> samples)
    {
        ResponsivenessTimelineGrid.Children.Clear();
        ResponsivenessTimelineGrid.ColumnDefinitions.Clear();
        var timeline = CompressTimeline(samples, 96);
        if (timeline.Count == 0)
        {
            ResponsivenessTimelineGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var empty = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            empty.Classes.Add("timelineInactive");
            ResponsivenessTimelineGrid.Children.Add(empty);
            return;
        }

        var measuredLatencies = timeline
            .Where(sample => sample.LatencyMs is not null)
            .Select(sample => sample.LatencyMs!.Value)
            .OrderBy(value => value)
            .ToArray();
        var chartCeiling = measuredLatencies.Length == 0
            ? 80d
            : Math.Max(80d, Percentile(measuredLatencies, 0.95) * 1.15);

        for (var index = 0; index < timeline.Count; index++)
        {
            ResponsivenessTimelineGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var sample = timeline[index];
            var height = sample.State switch
            {
                MonitorSampleState.Unresponsive => 66,
                MonitorSampleState.Inactive => 5,
                _ => Math.Clamp(8 + ((sample.LatencyMs ?? 0) / chartCeiling * 54), 8, 64)
            };
            var bar = new Border
            {
                Height = height,
                MinWidth = 2,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            ToolTip.SetTip(
                bar,
                sample.State == MonitorSampleState.Unresponsive
                    ? $"Unreachable · {sample.Timestamp.ToLocalTime():h:mm:ss tt}"
                    : $"{sample.LatencyMs:0.#} ms · {sample.Timestamp.ToLocalTime():h:mm:ss tt}");
            ApplySampleClass(bar, sample);
            Grid.SetColumn(bar, index);
            ResponsivenessTimelineGrid.Children.Add(bar);
        }
    }

    private void RenderReliabilityTimeline(IReadOnlyList<MonitorSample> samples)
    {
        ReliabilityTimelineGrid.Children.Clear();
        ReliabilityTimelineGrid.ColumnDefinitions.Clear();
        var timeline = CompressTimeline(samples, 120);
        if (timeline.Count == 0)
        {
            ReliabilityTimelineGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var empty = new Border
            {
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            empty.Classes.Add("timelineInactive");
            ReliabilityTimelineGrid.Children.Add(empty);
            return;
        }

        for (var index = 0; index < timeline.Count; index++)
        {
            ReliabilityTimelineGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var segment = new Border
            {
                MinWidth = 2,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ApplySampleClass(segment, timeline[index]);
            Grid.SetColumn(segment, index);
            ReliabilityTimelineGrid.Children.Add(segment);
        }
    }

    private void RenderAlerts(IReadOnlyList<MonitorAlert> alerts, int unreadCount)
    {
        AlertsPanel.Children.Clear();
        AlertSummaryText.Text = unreadCount == 0
            ? alerts.Count == 0 ? "No recent alerts" : "All caught up"
            : $"{unreadCount} unread alert{(unreadCount == 1 ? "" : "s")}";
        MarkAlertsReadButton.IsVisible = unreadCount > 0;
        ClearAlertsButton.IsVisible = alerts.Count > 0;

        if (alerts.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "Outages, score drops, network changes, and bandwidth changes will appear here.",
                FontSize = 10,
                LineHeight = 15,
                TextWrapping = TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            AlertsPanel.Children.Add(empty);
            return;
        }

        foreach (var alert in alerts.Take(4))
        {
            var indicator = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            ApplyAlertClass(indicator, alert.Severity);
            var title = new TextBlock
            {
                Text = alert.Title,
                FontSize = 11,
                FontWeight = alert.IsRead ? FontWeight.Normal : FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            var detail = new TextBlock
            {
                Text = alert.Detail,
                FontSize = 9,
                LineHeight = 14,
                TextWrapping = TextWrapping.Wrap
            };
            detail.Classes.Add("muted");
            var time = new TextBlock
            {
                Text = alert.Timestamp.ToLocalTime().ToString("MMM d · h:mm tt"),
                FontSize = 9
            };
            time.Classes.Add("muted");
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(title);
            content.Children.Add(detail);
            content.Children.Add(time);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
            row.Children.Add(indicator);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            AlertsPanel.Children.Add(row);
        }
    }

    private static IReadOnlyList<MonitorSample> CompressTimeline(
        IReadOnlyList<MonitorSample> samples,
        int maximumSamples)
    {
        if (samples.Count <= maximumSamples) return samples;

        var compressed = new List<MonitorSample>(maximumSamples);
        var bucketWidth = samples.Count / (double)maximumSamples;
        for (var bucket = 0; bucket < maximumSamples; bucket++)
        {
            var start = (int)Math.Floor(bucket * bucketWidth);
            var end = Math.Min(samples.Count, (int)Math.Ceiling((bucket + 1) * bucketWidth));
            if (start >= end) continue;

            var representative = samples[start];
            for (var index = start + 1; index < end; index++)
            {
                var candidate = samples[index];
                if (StateSeverity(candidate.State) > StateSeverity(representative.State)
                    || (candidate.State == representative.State
                        && (candidate.LatencyMs ?? 0) > (representative.LatencyMs ?? 0)))
                {
                    representative = candidate;
                }
            }
            compressed.Add(representative);
        }
        return compressed;
    }

    private static int StateSeverity(MonitorSampleState state) => state switch
    {
        MonitorSampleState.Unresponsive => 4,
        MonitorSampleState.Laggy => 3,
        MonitorSampleState.Responsive => 2,
        _ => 1
    };

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        var position = Math.Clamp(percentile, 0, 1) * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return sortedValues[lower];
        var fraction = position - lower;
        return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
    }

    private static void ApplySampleClass(Border border, MonitorSample sample)
    {
        RemoveScoreClasses(border);
        switch (sample.State)
        {
            case MonitorSampleState.Unresponsive:
                border.Classes.Add("scorePoor");
                break;
            case MonitorSampleState.Laggy:
                border.Classes.Add("scoreDegraded");
                break;
            case MonitorSampleState.Responsive when sample.LatencyMs <= 50:
                border.Classes.Add("scoreExcellent");
                break;
            case MonitorSampleState.Responsive:
                border.Classes.Add("scoreGood");
                break;
            default:
                border.Classes.Add("timelineInactive");
                break;
        }
    }

    private static void ApplyAlertClass(Border border, MonitorAlertSeverity severity)
    {
        RemoveScoreClasses(border);
        border.Classes.Add(severity switch
        {
            MonitorAlertSeverity.Critical => "scorePoor",
            MonitorAlertSeverity.Warning => "scoreDegraded",
            _ => "scoreExcellent"
        });
    }

    private static void ApplyScoreClass(Border border, ExperienceBand band, bool soft)
    {
        RemoveScoreClasses(border);
        border.Classes.Add((band, soft) switch
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
        });
    }

    private static void RemoveScoreClasses(Border border)
    {
        foreach (var value in new[]
        {
            "scoreExcellent", "scoreGood", "scoreFair", "scoreDegraded", "scorePoor", "scoreUnavailable",
            "scoreSoftExcellent", "scoreSoftGood", "scoreSoftFair", "scoreSoftDegraded", "scoreSoftPoor",
            "timelineInactive"
        })
        {
            border.Classes.Remove(value);
        }
    }

    private void SelectWindow(MonitorWindow window)
    {
        SetSelected(OneMinuteButton, window == MonitorWindow.OneMinute);
        SetSelected(FiveMinutesButton, window == MonitorWindow.FiveMinutes);
        SetSelected(OneHourButton, window == MonitorWindow.OneHour);
        SetSelected(TwentyFourHoursButton, window == MonitorWindow.TwentyFourHours);
    }

    private void WorkspaceSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        ConfigureOverviewLayout(eventArgs.NewSize.Width);
        ConfigureDiagnosticLayout(eventArgs.NewSize.Width);
    }

    private void ConfigureOverviewLayout(double width)
    {
        var stacked = width < 960;
        OverviewGrid.ColumnDefinitions.Clear();
        OverviewGrid.RowDefinitions.Clear();

        if (stacked)
        {
            OverviewGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var index = 0; index < 4; index++)
            {
                OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            OverviewGrid.ColumnSpacing = 0;
            OverviewGrid.RowSpacing = 12;
            SetGridPosition(DeviceHeader, 0, 0);
            SetGridPosition(ScoreColumn, 1, 0);
            SetGridPosition(TelemetryHeader, 2, 0);
            SetGridPosition(TelemetryColumn, 3, 0);
            TelemetryHeader.Margin = new Thickness(2, 10, 2, 0);
        }
        else
        {
            OverviewGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(320)));
            OverviewGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            OverviewGrid.ColumnSpacing = 20;
            OverviewGrid.RowSpacing = 12;
            SetGridPosition(DeviceHeader, 0, 0);
            SetGridPosition(TelemetryHeader, 0, 1);
            SetGridPosition(ScoreColumn, 1, 0);
            SetGridPosition(TelemetryColumn, 1, 1);
            TelemetryHeader.Margin = new Thickness(2, 0, 2, 0);
        }
    }

    private void ConfigureDiagnosticLayout(double width)
    {
        var wide = width >= 1180;
        var medium = width >= 760;

        ProfileGrid.ColumnDefinitions.Clear();
        ProfileGrid.RowDefinitions.Clear();
        if (wide)
        {
            for (var index = 0; index < 4; index++)
            {
                ProfileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
            ProfileGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(ConnectionProfileButton, 0, 0);
            SetGridPosition(QuickProfileButton, 0, 1);
            SetGridPosition(FullProfileButton, 0, 2);
            SetGridPosition(StressProfileButton, 0, 3);
        }
        else
        {
            ProfileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ProfileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ProfileGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ProfileGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(ConnectionProfileButton, 0, 0);
            SetGridPosition(QuickProfileButton, 0, 1);
            SetGridPosition(FullProfileButton, 1, 0);
            SetGridPosition(StressProfileButton, 1, 1);
        }

        DiagnosticDetailsGrid.ColumnDefinitions.Clear();
        DiagnosticDetailsGrid.RowDefinitions.Clear();
        if (wide)
        {
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)));
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)));
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.72, GridUnitType.Star)));
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(SelectedQuestionPanel, 0, 0);
            SetGridPosition(MethodPanel, 0, 1);
            SetGridPosition(RunPlanPanel, 0, 2);
            SetGridPosition(RunActionPanel, 0, 3);
        }
        else if (medium)
        {
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(SelectedQuestionPanel, 0, 0);
            SetGridPosition(MethodPanel, 0, 1);
            SetGridPosition(RunPlanPanel, 1, 0);
            SetGridPosition(RunActionPanel, 1, 1);
        }
        else
        {
            DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var index = 0; index < 4; index++)
            {
                DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            SetGridPosition(SelectedQuestionPanel, 0, 0);
            SetGridPosition(MethodPanel, 1, 0);
            SetGridPosition(RunPlanPanel, 2, 0);
            SetGridPosition(RunActionPanel, 3, 0);
        }
    }

    private static void SetGridPosition(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetRowSpan(control, 1);
        Grid.SetColumnSpan(control, 1);
    }

    private void ProfileClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var index))
        {
            ProfileRequested?.Invoke(this, new IndexRequestedEventArgs(index));
        }
    }

    private void MethodClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var index))
        {
            MethodRequested?.Invoke(this, new IndexRequestedEventArgs(index));
        }
    }

    private void WindowClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string value })
        {
            MonitorWindowRequested?.Invoke(this, new MonitorWindowRequestedEventArgs(MonitorWindowExtensions.Parse(value)));
        }
    }

    private void MonitoringToggleClicked(object? sender, RoutedEventArgs eventArgs) =>
        MonitoringToggleRequested?.Invoke(this, EventArgs.Empty);

    private void ContentSpeedClicked(object? sender, RoutedEventArgs eventArgs) =>
        ContentSpeedRequested?.Invoke(this, EventArgs.Empty);

    private void PeakSpeedClicked(object? sender, RoutedEventArgs eventArgs) =>
        PeakSpeedRequested?.Invoke(this, EventArgs.Empty);

    private void MarkAlertsReadClicked(object? sender, RoutedEventArgs eventArgs) =>
        MarkAlertsReadRequested?.Invoke(this, EventArgs.Empty);

    private void ClearAlertsClicked(object? sender, RoutedEventArgs eventArgs) =>
        ClearAlertsRequested?.Invoke(this, EventArgs.Empty);

    private void RunClicked(object? sender, RoutedEventArgs eventArgs) =>
        RunRequested?.Invoke(this, EventArgs.Empty);

    private void ActiveRunClicked(object? sender, RoutedEventArgs eventArgs) =>
        ActiveRunRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsClicked(object? sender, RoutedEventArgs eventArgs) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private static void SetSelected(Button button, bool selected)
    {
        if (selected)
        {
            if (!button.Classes.Contains("selected")) button.Classes.Add("selected");
        }
        else
        {
            button.Classes.Remove("selected");
        }
    }

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

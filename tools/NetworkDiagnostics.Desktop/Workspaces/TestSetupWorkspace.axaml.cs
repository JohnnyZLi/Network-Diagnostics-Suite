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
        MonitoringToggleButton.Content = experience.MonitoringEnabled ? "Pause" : "Start";

        ApplyScoreClass(ScoreOrbCore, experience.Band, soft: false);
        ApplyScoreClass(ScoreGlow, experience.Band, soft: true);
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

    private static string ScoreText(int? score) => score?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";

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

        var metrics = component.Metrics.Take(4).ToArray();
        for (var index = 0; index < metrics.Length; index++)
        {
            var metric = metrics[index];
            var label = new TextBlock
            {
                Text = metric.Label,
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap
            };
            label.Classes.Add("muted");
            var value = new TextBlock
            {
                Text = metric.Value,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            var stack = new StackPanel { Spacing = 3 };
            stack.Children.Add(label);
            stack.Children.Add(value);
            var cell = new Border { Child = stack };
            cell.Classes.Add("telemetryMetric");
            if (index == metrics.Length - 1) cell.Classes.Add("last");
            Grid.SetColumn(cell, index);
            metricsGrid.Children.Add(cell);
        }

        if (metrics.Length == 0)
        {
            var empty = new TextBlock
            {
                Text = "Waiting for measurements",
                FontSize = 10
            };
            empty.Classes.Add("muted");
            metricsGrid.Children.Add(empty);
        }
    }

    private void RenderResponsivenessTimeline(IReadOnlyList<MonitorSample> samples)
    {
        ResponsivenessTimelinePanel.Children.Clear();
        if (samples.Count == 0)
        {
            var empty = new Border { Width = 240, Height = 4, CornerRadius = new CornerRadius(2) };
            empty.Classes.Add("timelineInactive");
            ResponsivenessTimelinePanel.Children.Add(empty);
            return;
        }

        foreach (var sample in samples)
        {
            var latency = sample.LatencyMs ?? 0;
            var height = sample.State switch
            {
                MonitorSampleState.Unresponsive => 56,
                MonitorSampleState.Inactive => 5,
                _ => Math.Clamp(7 + latency / 4, 7, 54)
            };
            var bar = new Border
            {
                Width = samples.Count > 50 ? 4 : 6,
                Height = height,
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            ToolTip.SetTip(
                bar,
                sample.State == MonitorSampleState.Unresponsive
                    ? $"Unreachable · {sample.Timestamp.ToLocalTime():h:mm:ss tt}"
                    : $"{sample.LatencyMs:0.#} ms · {sample.Timestamp.ToLocalTime():h:mm:ss tt}");
            ApplySampleClass(bar, sample);
            ResponsivenessTimelinePanel.Children.Add(bar);
        }
    }

    private void RenderReliabilityTimeline(IReadOnlyList<MonitorSample> samples)
    {
        ReliabilityTimelineGrid.Children.Clear();
        ReliabilityTimelineGrid.ColumnDefinitions.Clear();
        if (samples.Count == 0)
        {
            ReliabilityTimelineGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var empty = new Border { CornerRadius = new CornerRadius(4) };
            empty.Classes.Add("timelineInactive");
            ReliabilityTimelineGrid.Children.Add(empty);
            return;
        }

        for (var index = 0; index < samples.Count; index++)
        {
            ReliabilityTimelineGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var segment = new Border { CornerRadius = new CornerRadius(2) };
            ApplySampleClass(segment, samples[index]);
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
        var stacked = eventArgs.NewSize.Width < 980;
        OverviewGrid.ColumnDefinitions[0].Width = stacked ? GridLength.Star : new GridLength(340);
        OverviewGrid.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : GridLength.Star;
        OverviewGrid.RowDefinitions.Clear();
        OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (stacked)
        {
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(ScoreColumn, 0);
            Grid.SetRow(ScoreColumn, 0);
            Grid.SetColumn(TelemetryColumn, 0);
            Grid.SetRow(TelemetryColumn, 1);
            TelemetryColumn.Margin = new Thickness(0, 8, 0, 0);
        }
        else
        {
            Grid.SetColumn(ScoreColumn, 0);
            Grid.SetRow(ScoreColumn, 0);
            Grid.SetColumn(TelemetryColumn, 1);
            Grid.SetRow(TelemetryColumn, 0);
            TelemetryColumn.Margin = new Thickness(0);
        }
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

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class RunningTestWorkspace : UserControl
{
    private static readonly PhaseModel[] PhaseModels =
    [
        new("Network", "Reachability and path readiness", 4, 20, false),
        new("Latency", "Idle responsiveness and request loss", 20, 30, false),
        new("Download", "Capacity and loaded responsiveness", 30, 58, false),
        new("Upload", "Capacity and loaded responsiveness", 58, 76, false),
        new("Deep diagnostics", "Gateway, route, DNS, services, Wi-Fi, protocol, and LAN evidence", 76, 100, true)
    ];

    private readonly List<PhaseView> phaseViews = [];
    private IReadOnlyList<string> renderedEventKeys = [];
    private Guid renderedRunId = Guid.Empty;
    private TextBlock? emptyEventText;

    public RunningTestWorkspace()
    {
        InitializeComponent();
        BuildPhaseRows();
    }

    public event EventHandler? StopRequested;

    public void Render(ActiveRunSnapshot snapshot, IReadOnlyList<ActiveRunEvent> events)
    {
        var profile = DiagnosticReportPresenter.ProfileName(snapshot.Profile);
        SetText(TitleText, $"{profile} test");
        SetText(SubtitleText, $"{MethodName(snapshot.Method)} method · started {StartedLabel(snapshot.StartedAt)} · run {RunLabel(snapshot.RunId)}");
        SetText(PhaseText, snapshot.Phase);
        SetText(DetailText, snapshot.Detail);
        SetText(ProgressText, $"{snapshot.Progress:0}%");
        if (Math.Abs(ProgressBar.Value - snapshot.Progress) >= 0.05)
        {
            ProgressBar.Value = snapshot.Progress;
        }
        SetText(RateText, snapshot.LiveMbps is { } rate ? $"{Format(rate)} Mbps" : "—");
        SetText(LatencyText, snapshot.LiveLatencyMs is { } latency ? $"{Format(latency)} ms" : "—");
        SetText(BytesText, snapshot.BytesTransferred > 0
            ? $"{snapshot.BytesTransferred / 1_000_000d:0.0} MB"
            : "—");

        var canStop = snapshot.Status is ActiveRunStatus.Preparing or ActiveRunStatus.Running;
        if (StopButton.IsEnabled != canStop) StopButton.IsEnabled = canStop;
        var stopLabel = snapshot.Status == ActiveRunStatus.Cancelling ? "Stopping…" : "Stop test";
        if (!string.Equals(StopButton.Content?.ToString(), stopLabel, StringComparison.Ordinal))
        {
            StopButton.Content = stopLabel;
        }

        UpdatePhases(snapshot);
        UpdateEvents(snapshot.RunId, events);
    }

    private void BuildPhaseRows()
    {
        PhaseItems.Items.Clear();
        phaseViews.Clear();

        foreach (var phase in PhaseModels)
        {
            var marker = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new Avalonia.CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };
            marker.Classes.Add("indicatorNeutral");

            var title = new TextBlock
            {
                Text = phase.Title,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            };
            var detail = new TextBlock
            {
                Text = phase.Detail,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            detail.Classes.Add("muted");

            var statusText = new TextBlock
            {
                Text = "Waiting",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            statusText.Classes.Add("muted");

            var text = new StackPanel { Spacing = 3 };
            text.Children.Add(title);
            text.Children.Add(detail);

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            grid.Children.Add(marker);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);

            var border = new Border
            {
                Child = grid,
                Padding = new Avalonia.Thickness(0, 3, 0, 11),
                Margin = new Avalonia.Thickness(0, 0, 0, 10)
            };
            border.Classes.Add("divider");
            PhaseItems.Items.Add(border);
            phaseViews.Add(new PhaseView(phase, marker, statusText));
        }
    }

    private void UpdatePhases(ActiveRunSnapshot snapshot)
    {
        foreach (var view in phaseViews)
        {
            var status = PhaseStatus(snapshot, view.Model);
            SetText(view.StatusText, status);
            SetIndicatorClass(view.Marker, status);
            view.StatusText.Classes.Remove("secondary");
            view.StatusText.Classes.Remove("muted");
            view.StatusText.Classes.Add(status == "In progress" ? "secondary" : "muted");
        }
    }

    private void UpdateEvents(Guid runId, IReadOnlyList<ActiveRunEvent> events)
    {
        if (renderedRunId != runId)
        {
            renderedRunId = runId;
            renderedEventKeys = [];
            EventPanel.Children.Clear();
            emptyEventText = null;
        }

        var visible = events.TakeLast(20).Reverse().ToArray();
        var keys = visible.Select(EventKey).ToArray();
        if (keys.SequenceEqual(renderedEventKeys)) return;

        if (visible.Length == 0)
        {
            EventPanel.Children.Clear();
            emptyEventText = new TextBlock
            {
                Text = "Waiting for the first measurement update…",
                TextWrapping = TextWrapping.Wrap
            };
            emptyEventText.Classes.Add("muted");
            EventPanel.Children.Add(emptyEventText);
            renderedEventKeys = keys;
            return;
        }

        var isSingleAppend = renderedEventKeys.Count > 0
            && keys.Length >= 1
            && keys.Skip(1).SequenceEqual(renderedEventKeys.Take(keys.Length - 1));

        if (isSingleAppend)
        {
            if (emptyEventText is not null)
            {
                EventPanel.Children.Remove(emptyEventText);
                emptyEventText = null;
            }
            EventPanel.Children.Insert(0, BuildEventRow(visible[0]));
            while (EventPanel.Children.Count > 20)
            {
                EventPanel.Children.RemoveAt(EventPanel.Children.Count - 1);
            }
        }
        else
        {
            EventPanel.Children.Clear();
            emptyEventText = null;
            foreach (var item in visible)
            {
                EventPanel.Children.Add(BuildEventRow(item));
            }
        }

        renderedEventKeys = keys;
    }

    private static Border BuildEventRow(ActiveRunEvent item)
    {
        var time = new TextBlock
        {
            Text = item.Timestamp.ToLocalTime().ToString("h:mm:ss tt", CultureInfo.InvariantCulture),
            FontSize = 10
        };
        time.Classes.Add("muted");

        var phase = new TextBlock
        {
            Text = item.Phase.ToUpperInvariant()
        };
        phase.Classes.Add("eyebrow");

        var detail = new TextBlock
        {
            Text = item.Detail,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("secondary");

        var values = new List<string>();
        if (item.LiveMbps is { } rate) values.Add($"{Format(rate)} Mbps");
        if (item.LiveLatencyMs is { } latency) values.Add($"{Format(latency)} ms");
        if (item.BytesTransferred > 0) values.Add($"{item.BytesTransferred / 1_000_000d:0.0} MB");

        var content = new StackPanel { Spacing = 4 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(phase);
        Grid.SetColumn(time, 1);
        header.Children.Add(time);
        content.Children.Add(header);
        content.Children.Add(detail);

        if (values.Count > 0)
        {
            var valueText = new TextBlock
            {
                Text = string.Join(" · ", values),
                FontSize = 10
            };
            valueText.Classes.Add("muted");
            content.Children.Add(valueText);
        }

        var row = new Border
        {
            Padding = new Avalonia.Thickness(0, 10, 0, 11),
            Child = content
        };
        row.Classes.Add("divider");
        return row;
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) =>
        StopRequested?.Invoke(this, EventArgs.Empty);

    private static string PhaseStatus(ActiveRunSnapshot snapshot, PhaseModel phase)
    {
        var included = !phase.DeepOnly
            || snapshot.Profile is TestProfileId.Standard or TestProfileId.Extended;
        if (!included) return "Not included";
        if (snapshot.Status == ActiveRunStatus.Completed || snapshot.Progress >= phase.End) return "Complete";
        if (snapshot.Progress >= phase.Start) return "In progress";
        return "Waiting";
    }

    private static void SetIndicatorClass(Border marker, string status)
    {
        marker.Classes.Remove("indicatorSuccess");
        marker.Classes.Remove("indicatorAccent");
        marker.Classes.Remove("indicatorNeutral");
        marker.Classes.Add(status switch
        {
            "Complete" => "indicatorSuccess",
            "In progress" => "indicatorAccent",
            _ => "indicatorNeutral"
        });
    }

    private static void SetText(TextBlock block, string value)
    {
        if (!string.Equals(block.Text, value, StringComparison.Ordinal)) block.Text = value;
    }

    private static string EventKey(ActiveRunEvent item) => string.Join('|',
        item.Timestamp.UtcTicks,
        item.Phase,
        item.Detail,
        item.Progress.ToString("0.###", CultureInfo.InvariantCulture),
        item.LiveMbps?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty,
        item.LiveLatencyMs?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty,
        item.BytesTransferred.ToString(CultureInfo.InvariantCulture));

    private static string MethodName(TransferMethod method) => method switch
    {
        TransferMethod.Single => "Single",
        TransferMethod.Aggregate => "Aggregate",
        _ => "Compare"
    };

    private static string Format(double value) =>
        value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture);

    private static string StartedLabel(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture) ?? "now";

    private static string RunLabel(Guid runId) => runId == Guid.Empty ? "pending" : runId.ToString("N")[..8];

    private sealed record PhaseModel(string Title, string Detail, double Start, double End, bool DeepOnly);

    private sealed record PhaseView(PhaseModel Model, Border Marker, TextBlock StatusText);
}

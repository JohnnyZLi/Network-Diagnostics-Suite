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
    public RunningTestWorkspace()
    {
        InitializeComponent();
    }

    public event EventHandler? StopRequested;

    public void Render(ActiveRunSnapshot snapshot, IReadOnlyList<ActiveRunEvent> events)
    {
        var profile = DiagnosticReportPresenter.ProfileName(snapshot.Profile);
        TitleText.Text = $"{profile} test";
        SubtitleText.Text = $"{MethodName(snapshot.Method)} method · started {StartedLabel(snapshot.StartedAt)} · run {RunLabel(snapshot.RunId)}";
        PhaseText.Text = snapshot.Phase;
        DetailText.Text = snapshot.Detail;
        ProgressText.Text = $"{snapshot.Progress:0}%";
        ProgressBar.Value = snapshot.Progress;
        RateText.Text = snapshot.LiveMbps is { } rate ? $"{Format(rate)} Mbps" : "—";
        LatencyText.Text = snapshot.LiveLatencyMs is { } latency ? $"{Format(latency)} ms" : "—";
        BytesText.Text = snapshot.BytesTransferred > 0
            ? $"{snapshot.BytesTransferred / 1_000_000d:0.0} MB"
            : "—";
        StopButton.IsEnabled = snapshot.Status is ActiveRunStatus.Preparing or ActiveRunStatus.Running;
        StopButton.Content = snapshot.Status == ActiveRunStatus.Cancelling ? "Stopping…" : "Stop test";

        RenderPhases(snapshot);
        RenderEvents(events);
    }

    private void RenderPhases(ActiveRunSnapshot snapshot)
    {
        PhaseItems.Items.Clear();
        var phases = new[]
        {
            new PhaseModel("Network", "Reachability and path readiness", 4, 20, true),
            new PhaseModel("Latency", "Idle responsiveness and request loss", 20, 30, true),
            new PhaseModel("Download", "Capacity and loaded responsiveness", 30, 58, true),
            new PhaseModel("Upload", "Capacity and loaded responsiveness", 58, 76, true),
            new PhaseModel("Deep diagnostics", "Gateway, route, DNS, services, Wi-Fi, protocol, and LAN evidence", 76, 100,
                snapshot.Profile is TestProfileId.Standard or TestProfileId.Extended)
        };

        foreach (var phase in phases)
        {
            var status = PhaseStatus(snapshot, phase);
            var marker = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new Avalonia.CornerRadius(4),
                Background = Brush.Parse(status switch
                {
                    "Complete" => "#C77E68",
                    "In progress" => "#E7E2DA",
                    "Not included" => "#444A4B",
                    _ => "#697071"
                }),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };
            var title = new TextBlock { Text = phase.Title, FontWeight = FontWeight.SemiBold };
            var detail = new TextBlock
            {
                Text = phase.Detail,
                FontSize = 11,
                Foreground = Brush.Parse("#969C9D"),
                TextWrapping = TextWrapping.Wrap
            };
            var statusText = new TextBlock
            {
                Text = status,
                FontSize = 11,
                Foreground = Brush.Parse(status == "In progress" ? "#E9E6E0" : "#969C9D"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var text = new StackPanel { Spacing = 3 };
            text.Children.Add(title);
            text.Children.Add(detail);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            grid.Children.Add(marker);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);
            var border = new Border { Child = grid, Margin = new Avalonia.Thickness(0, 0, 0, 8) };
            border.Classes.Add("phase");
            PhaseItems.Items.Add(border);
        }
    }

    private void RenderEvents(IReadOnlyList<ActiveRunEvent> events)
    {
        EventPanel.Children.Clear();
        foreach (var item in events.TakeLast(20).Reverse())
        {
            var time = new TextBlock
            {
                Text = item.Timestamp.ToLocalTime().ToString("h:mm:ss tt", CultureInfo.InvariantCulture),
                FontSize = 10,
                Foreground = Brush.Parse("#777E7F")
            };
            var phase = new TextBlock
            {
                Text = item.Phase.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 1.2,
                Foreground = Brush.Parse("#C77E68")
            };
            var detail = new TextBlock
            {
                Text = item.Detail,
                FontSize = 12,
                Foreground = Brush.Parse("#D5D2CC"),
                TextWrapping = TextWrapping.Wrap
            };
            var values = new List<string>();
            if (item.LiveMbps is { } rate) values.Add($"{Format(rate)} Mbps");
            if (item.LiveLatencyMs is { } latency) values.Add($"{Format(latency)} ms");
            if (item.BytesTransferred > 0) values.Add($"{item.BytesTransferred / 1_000_000d:0.0} MB");
            var content = new StackPanel { Spacing = 3 };
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            header.Children.Add(phase);
            Grid.SetColumn(time, 1);
            header.Children.Add(time);
            content.Children.Add(header);
            content.Children.Add(detail);
            if (values.Count > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = string.Join(" · ", values),
                    FontSize = 11,
                    Foreground = Brush.Parse("#969C9D")
                });
            }
            EventPanel.Children.Add(new Border
            {
                BorderBrush = Brush.Parse("#303536"),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
                Padding = new Avalonia.Thickness(0, 0, 0, 10),
                Child = content
            });
        }

        if (events.Count == 0)
        {
            EventPanel.Children.Add(new TextBlock
            {
                Text = "Waiting for the first measurement update…",
                Foreground = Brush.Parse("#969C9D"),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) =>
        StopRequested?.Invoke(this, EventArgs.Empty);

    private static string PhaseStatus(ActiveRunSnapshot snapshot, PhaseModel phase)
    {
        if (!phase.Included) return "Not included";
        if (snapshot.Status == ActiveRunStatus.Completed || snapshot.Progress >= phase.End) return "Complete";
        if (snapshot.Progress >= phase.Start) return "In progress";
        return "Waiting";
    }

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

    private sealed record PhaseModel(string Title, string Detail, double Start, double End, bool Included);
}

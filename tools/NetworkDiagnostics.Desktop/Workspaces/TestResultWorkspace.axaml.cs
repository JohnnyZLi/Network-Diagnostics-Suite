using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestResultWorkspace : UserControl
{
    private string section = "Overview";

    public TestResultWorkspace()
    {
        InitializeComponent();
    }

    public event EventHandler? RunAgainRequested;
    public event EventHandler? QuickRequested;
    public event EventHandler? ExportRequested;
    public event EventHandler? ReportsRequested;
    public event EventHandler? CompareRequested;
    public event EventHandler<SectionRequestedEventArgs>? SectionRequested;

    public void Render(
        ConnectionCheckPresentation presentation,
        NetworkDiagnosticsReportV2? report,
        ActiveRunSnapshot session,
        string requestedSection = "Overview")
    {
        section = string.Equals(requestedSection, "Evidence", StringComparison.OrdinalIgnoreCase)
            ? "Evidence"
            : "Overview";
        LabelText.Text = presentation.Label.ToUpperInvariant();
        VerdictText.Text = presentation.Verdict;
        SummaryText.Text = presentation.Summary;
        NextActionText.Text = presentation.NextAction;
        ContextText.Text = ContextLabel(report, session);
        InterpretationText.Text = Interpretation(presentation, report, session);

        var profile = report?.Run.Profile ?? session.Profile;
        QuickButton.IsVisible = profile == TestProfileId.ConnectionCheck
            && presentation.Outcome == ConnectionCheckOutcome.Healthy;
        ExportButton.IsVisible = report is not null;
        CompareButton.IsVisible = report is not null;

        RenderMetrics(presentation.Metrics);
        RenderFindings(presentation.Findings);
        RenderEvidence(presentation.TechnicalEvidence);
        RenderRunDetails(report, session);
        ApplySection();
    }

    private void RenderMetrics(IReadOnlyList<MetricPresentation> metrics)
    {
        MetricGrid.Children.Clear();
        for (var index = 0; index < Math.Min(4, metrics.Count); index++)
        {
            var metric = metrics[index];
            var label = new TextBlock
            {
                Text = metric.Label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 1.2,
                Foreground = Brush.Parse("#C77E68")
            };
            var value = new TextBlock
            {
                Text = metric.Value,
                FontSize = 23,
                FontWeight = FontWeight.SemiBold,
                Opacity = metric.WasMeasured ? 1 : 0.65
            };
            var detail = new TextBlock
            {
                Text = metric.Detail,
                FontSize = 11,
                Foreground = Brush.Parse("#969C9D"),
                TextWrapping = TextWrapping.Wrap
            };
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(label);
            content.Children.Add(value);
            content.Children.Add(detail);
            var card = new Border { Child = content };
            card.Classes.Add("panel");
            card.Padding = new Avalonia.Thickness(14);
            Grid.SetColumn(card, index);
            MetricGrid.Children.Add(card);
        }
    }

    private void RenderFindings(IReadOnlyList<FindingPresentation> findings)
    {
        FindingsPanel.Children.Clear();
        foreach (var finding in findings)
        {
            var label = new TextBlock
            {
                Text = finding.Label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 1.2,
                Foreground = Brush.Parse("#C77E68")
            };
            var title = new TextBlock
            {
                Text = finding.Title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            var summary = new TextBlock
            {
                Text = finding.Summary,
                FontSize = 12,
                Foreground = Brush.Parse("#969C9D"),
                TextWrapping = TextWrapping.Wrap
            };
            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(label);
            content.Children.Add(title);
            content.Children.Add(summary);
            FindingsPanel.Children.Add(new Border
            {
                BorderBrush = Brush.Parse("#303536"),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
                Padding = new Avalonia.Thickness(0, 0, 0, 12),
                Child = content
            });
        }
    }

    private void RenderEvidence(IReadOnlyList<string> evidence)
    {
        EvidencePanel.Children.Clear();
        foreach (var item in evidence)
        {
            var marker = new TextBlock
            {
                Text = "•",
                Foreground = Brush.Parse("#C77E68"),
                VerticalAlignment = VerticalAlignment.Top
            };
            var text = new TextBlock
            {
                Text = item,
                FontSize = 12,
                Foreground = Brush.Parse("#D6D3CD"),
                TextWrapping = TextWrapping.Wrap
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
            row.Children.Add(marker);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            EvidencePanel.Children.Add(row);
        }
    }

    private void RenderRunDetails(NetworkDiagnosticsReportV2? report, ActiveRunSnapshot session)
    {
        RunDetailPanel.Children.Clear();
        var profile = report?.Run.Profile ?? session.Profile;
        var method = report?.Run.TransferMethod ?? session.Method;
        AddDetail("Profile", DiagnosticReportPresenter.ProfileName(profile));
        AddDetail("Method", MethodName(method));
        AddDetail("Status", StatusLabel(report, session));
        AddDetail("Generated", report?.GeneratedAt.ToLocalTime().ToString("MMM d, yyyy · h:mm tt") ?? "Not saved");
        AddDetail("Report ID", report?.Run.Id.ToString("N")[..8] ?? "—");
        AddDetail("Payload", session.BytesTransferred > 0 ? $"{session.BytesTransferred / 1_000_000d:0.0} MB" : "See report evidence");
    }

    private void AddDetail(string label, string value)
    {
        var labelText = new TextBlock { Text = label, FontSize = 11, Foreground = Brush.Parse("#969C9D") };
        var valueText = new TextBlock
        {
            Text = value,
            FontSize = 11,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap
        };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        row.Children.Add(labelText);
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);
        RunDetailPanel.Children.Add(row);
    }

    private void SectionClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string requested }) return;
        section = requested;
        ApplySection();
        SectionRequested?.Invoke(this, new SectionRequestedEventArgs(section));
    }

    private void ApplySection()
    {
        var evidence = string.Equals(section, "Evidence", StringComparison.OrdinalIgnoreCase);
        OverviewView.IsVisible = !evidence;
        EvidenceView.IsVisible = evidence;
        SetSelected(OverviewButton, !evidence);
        SetSelected(EvidenceButton, evidence);
    }

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

    private void RunAgainClicked(object? sender, RoutedEventArgs eventArgs) => RunAgainRequested?.Invoke(this, EventArgs.Empty);
    private void QuickClicked(object? sender, RoutedEventArgs eventArgs) => QuickRequested?.Invoke(this, EventArgs.Empty);
    private void ExportClicked(object? sender, RoutedEventArgs eventArgs) => ExportRequested?.Invoke(this, EventArgs.Empty);
    private void ReportsClicked(object? sender, RoutedEventArgs eventArgs) => ReportsRequested?.Invoke(this, EventArgs.Empty);
    private void CompareClicked(object? sender, RoutedEventArgs eventArgs) => CompareRequested?.Invoke(this, EventArgs.Empty);

    private static string ContextLabel(NetworkDiagnosticsReportV2? report, ActiveRunSnapshot session)
    {
        if (report is not null)
        {
            var context = ReportComparisonService.ContextLabel(report);
            return string.IsNullOrWhiteSpace(context)
                ? $"Saved locally · {report.GeneratedAt.ToLocalTime():MMM d, yyyy}"
                : $"{context}\nSaved locally · {report.GeneratedAt.ToLocalTime():MMM d, yyyy}"
                ;
        }

        return session.Status switch
        {
            ActiveRunStatus.Cancelled => "Cancelled result · completed evidence was retained in this view but no report was saved.",
            ActiveRunStatus.Failed => "Failed result · completed evidence was retained in this view but no report was saved.",
            _ => "Preview result · no local report file is attached."
        };
    }

    private static string Interpretation(
        ConnectionCheckPresentation presentation,
        NetworkDiagnosticsReportV2? report,
        ActiveRunSnapshot session)
    {
        if (session.Status == ActiveRunStatus.Cancelled)
        {
            return "Cancellation is a terminal run state, not a network verdict. Measurements that finished before cancellation remain useful; sections that never ran remain neutral.";
        }
        if (session.Status == ActiveRunStatus.Failed)
        {
            return "A run failure does not imply that every network layer failed. Use the evidence list to separate completed measurements from unavailable or unstarted sections.";
        }
        if (presentation.Metrics.Any(metric => !metric.WasMeasured))
        {
            return "Not measured values are neutral. They identify evidence the run could not collect and should not be interpreted as zero, failure, or poor performance.";
        }
        return report is null
            ? "This preview uses the same result hierarchy as a completed diagnostic, but it is not attached to a saved report."
            : "The overview prioritizes the verdict and actionable findings. Evidence retains the technical context needed to verify or challenge that interpretation.";
    }

    private static string StatusLabel(NetworkDiagnosticsReportV2? report, ActiveRunSnapshot session) => report is not null
        ? "Completed"
        : session.Status switch
        {
            ActiveRunStatus.Cancelled => "Cancelled",
            ActiveRunStatus.Failed => "Failed",
            _ => "Preview"
        };

    private static string MethodName(TransferMethod method) => method switch
    {
        TransferMethod.Single => "Single",
        TransferMethod.Aggregate => "Aggregate",
        _ => "Compare"
    };
}

public sealed class SectionRequestedEventArgs(string section) : EventArgs
{
    public string Section { get; } = section;
}

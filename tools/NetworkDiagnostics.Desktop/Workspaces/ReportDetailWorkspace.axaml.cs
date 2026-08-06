using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportDetailWorkspace : UserControl
{
    private StoredReport? report;

    public ReportDetailWorkspace()
    {
        InitializeComponent();
        SizeChanged += WorkspaceSizeChanged;
    }

    public event EventHandler? HomeRequested;

    public event EventHandler? BackRequested;

    public event EventHandler<StoredReportEventArgs>? CompareRequested;

    public event EventHandler<StoredReportEventArgs>? EditRequested;

    public event EventHandler<StoredReportEventArgs>? ExportRequested;

    public void Render(StoredReport stored, ConnectionCheckPresentation presentation)
    {
        report = stored;
        ToolbarTitleText.Text = stored.Label ?? presentation.Verdict;
        ToolbarMetaText.Text = $"{stored.ProfileName} · {stored.DisplayDate}";
        OutcomeLabelText.Text = presentation.Label.ToUpperInvariant();
        VerdictText.Text = presentation.Verdict;
        SummaryText.Text = presentation.Summary;
        NextActionText.Text = presentation.NextAction;
        GeneratedText.Text = stored.DisplayDate;
        ProfileText.Text = stored.ProfileName;
        MethodText.Text = stored.Report.Run.TransferMethod.ToString();
        ContextText.Text = ReportComparisonService.ContextLabel(stored.Report);
        HealthGroupCardFactory.ApplyOutcomeIndicator(OutcomeIndicator, presentation.Outcome);

        RenderHealthGroups(presentation);
        RenderFindings(presentation.Findings);
        RenderEvidence(presentation.TechnicalEvidence);
    }

    private void RenderHealthGroups(ConnectionCheckPresentation presentation)
    {
        var groups = HealthGroupPresenter.Build(presentation)
            .ToDictionary(group => group.Kind);
        ResponsivenessGroupHost.Content = HealthGroupCardFactory.Build(groups[HealthGroupKind.Responsiveness]);
        ReliabilityGroupHost.Content = HealthGroupCardFactory.Build(groups[HealthGroupKind.Reliability]);
        ThroughputGroupHost.Content = HealthGroupCardFactory.Build(groups[HealthGroupKind.Throughput]);
        ApplyHealthGroupLayout(Bounds.Width);
    }

    private void RenderFindings(IReadOnlyList<FindingPresentation> findings)
    {
        FindingsPanel.Children.Clear();
        if (findings.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No material findings were generated for this report.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            FindingsPanel.Children.Add(empty);
            return;
        }

        foreach (var finding in findings)
        {
            FindingsPanel.Children.Add(BuildFinding(finding));
        }
    }

    private void RenderEvidence(IReadOnlyList<string> evidence)
    {
        EvidencePanel.Children.Clear();
        foreach (var item in evidence)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 9
            };
            var marker = new TextBlock
            {
                Text = "•",
                FontSize = 12
            };
            marker.Classes.Add("eyebrow");
            row.Children.Add(marker);

            var text = new TextBlock
            {
                Text = item,
                FontSize = 11,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap
            };
            text.Classes.Add("secondary");
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            EvidencePanel.Children.Add(row);
        }
    }

    private void WorkspaceSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyHealthGroupLayout(eventArgs.NewSize.Width);

    private void ApplyHealthGroupLayout(double width) =>
        HealthGroupCardFactory.ApplyResponsiveLayout(
            HealthGroupGrid,
            [ResponsivenessGroupHost, ReliabilityGroupHost, ThroughputGroupHost],
            width);

    private void HomeClicked(object? sender, RoutedEventArgs eventArgs) =>
        HomeRequested?.Invoke(this, EventArgs.Empty);

    private void BackClicked(object? sender, RoutedEventArgs eventArgs) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void CompareClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (report is not null)
        {
            CompareRequested?.Invoke(this, new StoredReportEventArgs(report));
        }
    }

    private void EditClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (report is not null)
        {
            EditRequested?.Invoke(this, new StoredReportEventArgs(report));
        }
    }

    private void ExportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (report is not null)
        {
            ExportRequested?.Invoke(this, new StoredReportEventArgs(report));
        }
    }

    private static Border BuildFinding(FindingPresentation finding)
    {
        var label = new TextBlock
        {
            Text = finding.Label.ToUpperInvariant()
        };
        label.Classes.Add("eyebrow");

        var title = new TextBlock
        {
            Text = finding.Title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        var summary = new TextBlock
        {
            Text = finding.Summary,
            FontSize = 12,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap
        };
        summary.Classes.Add("secondary");

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(label);
        stack.Children.Add(title);
        stack.Children.Add(summary);

        var row = new Border
        {
            Padding = new Avalonia.Thickness(0, 5, 0, 14),
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
            Child = stack
        };
        row.Classes.Add("divider");
        return row;
    }
}

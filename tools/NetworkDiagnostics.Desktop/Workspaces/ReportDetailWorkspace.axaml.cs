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
    private bool evidenceExpanded;
    private Button? compareButton;
    private Button? runAgainButton;

    public ReportDetailWorkspace()
    {
        InitializeComponent();
        SizeChanged += WorkspaceSizeChanged;
        EnsureContextActions();
    }

    public event EventHandler? HomeRequested;

    public event EventHandler? BackRequested;

    public event EventHandler? RunAgainRequested;

    public event EventHandler<StoredReportEventArgs>? CompareRequested;

    public event EventHandler<StoredReportEventArgs>? EditRequested;

    public event EventHandler<StoredReportEventArgs>? ExportRequested;

    public void Render(StoredReport stored, ConnectionCheckPresentation presentation)
    {
        SetCurrentRunMode(false);
        report = stored;
        ToolbarTitleText.Text = stored.ProfileName;
        ToolbarMetaText.Text = $"{stored.DisplayDate} · {stored.Report.Run.TransferMethod}";
        GeneratedText.Text = stored.DisplayDate;
        ProfileText.Text = stored.ProfileName;
        MethodText.Text = stored.Report.Run.TransferMethod.ToString();
        ContextText.Text = ReportComparisonService.ContextLabel(stored.Report);
        RenderPresentation(presentation);
    }

    public void RenderCurrent(StoredReport stored, ConnectionCheckPresentation presentation)
    {
        Render(stored, presentation);
        SetCurrentRunMode(true);
        ToolbarMetaText.Text = $"Just completed · {stored.Report.Run.TransferMethod}";
    }

    public void RenderPreview(ConnectionCheckPresentation presentation)
    {
        SetCurrentRunMode(false);
        report = null;
        ToolbarTitleText.Text = "Quick";
        ToolbarMetaText.Text = "Preview data · Compare";
        GeneratedText.Text = "Today · preview";
        ProfileText.Text = "Quick";
        MethodText.Text = "Compare";
        ContextText.Text = "Automatic routing · first-party endpoint";
        RenderPresentation(presentation);
    }

    public void RenderCurrentPreview(ConnectionCheckPresentation presentation)
    {
        report = null;
        ToolbarTitleText.Text = "Connection Check";
        ToolbarMetaText.Text = "Just completed · Aggregate";
        GeneratedText.Text = "Just now";
        ProfileText.Text = "Connection Check";
        MethodText.Text = "Aggregate";
        ContextText.Text = "Automatic routing · first-party endpoint";
        RenderPresentation(presentation);
        SetCurrentRunMode(true);
    }

    private void RenderPresentation(ConnectionCheckPresentation presentation)
    {
        OutcomeLabelText.Text = presentation.Label.ToUpperInvariant();
        VerdictText.Text = presentation.Verdict;
        SummaryText.Text = presentation.Summary;
        NextActionText.Text = presentation.NextAction;
        HealthGroupCardFactory.ApplyOutcomeIndicator(OutcomeIndicator, presentation.Outcome);
        RenderHealthGroups(presentation);
        RenderFindings(presentation.Findings);
        RenderEvidence(presentation.TechnicalEvidence);
        SetEvidenceExpanded(false);
    }

    private void RenderHealthGroups(ConnectionCheckPresentation presentation)
    {
        var groups = HealthGroupPresenter.Build(presentation)
            .ToDictionary(group => group.Kind);
        ResponsivenessGroupHost.Content = HealthGroupCardFactory.Build(
            groups[HealthGroupKind.Responsiveness],
            compact: true);
        ReliabilityGroupHost.Content = HealthGroupCardFactory.Build(
            groups[HealthGroupKind.Reliability],
            compact: true);
        ThroughputGroupHost.Content = HealthGroupCardFactory.Build(
            groups[HealthGroupKind.Throughput],
            compact: true);
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
                FontSize = 11,
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
        EvidenceCountText.Text = evidence.Count == 0
            ? "No additional measurement notes"
            : $"{evidence.Count} measurement note{(evidence.Count == 1 ? string.Empty : "s")} and run details";

        if (evidence.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "This report does not contain additional technical evidence.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            EvidencePanel.Children.Add(empty);
            return;
        }

        foreach (var item in evidence)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 9
            };
            var marker = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new Avalonia.CornerRadius(3),
                Margin = new Avalonia.Thickness(0, 5, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            marker.Classes.Add("indicatorAccent");
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

    private void EnsureContextActions()
    {
        if (runAgainButton is not null) return;
        var actionPanel = ReportToolbarGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 2);
        if (actionPanel is null) return;

        compareButton = actionPanel.Children
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Compare", StringComparison.Ordinal));
        runAgainButton = new Button
        {
            Content = "Run again",
            MinHeight = 32,
            Padding = new Avalonia.Thickness(13, 5),
            IsVisible = false
        };
        runAgainButton.Classes.Add("primary");
        runAgainButton.Click += RunAgainClicked;
        actionPanel.Children.Add(runAgainButton);
    }

    private void SetCurrentRunMode(bool currentRun)
    {
        EnsureContextActions();
        if (runAgainButton is not null) runAgainButton.IsVisible = currentRun;
        if (compareButton is null) return;

        compareButton.Classes.Remove("primary");
        compareButton.Classes.Remove("secondary");
        compareButton.Classes.Add(currentRun ? "secondary" : "primary");
    }

    private void WorkspaceSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyHealthGroupLayout(eventArgs.NewSize.Width);

    private void ApplyHealthGroupLayout(double width) =>
        HealthGroupCardFactory.ApplyResponsiveLayout(
            HealthGroupGrid,
            [ResponsivenessGroupHost, ReliabilityGroupHost, ThroughputGroupHost],
            width);

    private void EvidenceToggleClicked(object? sender, RoutedEventArgs eventArgs) =>
        SetEvidenceExpanded(!evidenceExpanded);

    private void SetEvidenceExpanded(bool expanded)
    {
        evidenceExpanded = expanded;
        EvidenceBody.IsVisible = expanded;
        EvidenceToggleLabelText.Text = expanded ? "Hide details" : "Show details";
    }

    private void HomeClicked(object? sender, RoutedEventArgs eventArgs) =>
        HomeRequested?.Invoke(this, EventArgs.Empty);

    private void BackClicked(object? sender, RoutedEventArgs eventArgs) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void RunAgainClicked(object? sender, RoutedEventArgs eventArgs) =>
        RunAgainRequested?.Invoke(this, EventArgs.Empty);

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
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        var summary = new TextBlock
        {
            Text = finding.Summary,
            FontSize = 11,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap
        };
        summary.Classes.Add("secondary");

        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(label);
        stack.Children.Add(title);
        stack.Children.Add(summary);

        var row = new Border
        {
            Padding = new Avalonia.Thickness(0, 5, 0, 12),
            Margin = new Avalonia.Thickness(0, 0, 0, 10),
            Child = stack
        };
        row.Classes.Add("divider");
        return row;
    }
}

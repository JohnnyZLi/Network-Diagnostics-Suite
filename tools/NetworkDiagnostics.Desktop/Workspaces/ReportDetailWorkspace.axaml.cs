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

    public ReportDetailWorkspace()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    public event EventHandler? CloseRequested;

    public event EventHandler? RunAgainRequested;

    public event EventHandler<StoredReportEventArgs>? CompareRequested;

    public event EventHandler<StoredReportEventArgs>? EditRequested;

    public event EventHandler<StoredReportEventArgs>? ExportRequested;

    public void Render(StoredReport stored, ConnectionCheckPresentation presentation)
    {
        SetCurrentRunMode(false);
        report = stored;
        ToolbarTitleText.Text = stored.ProfileName;
        var context = ReportComparisonService.ContextLabel(stored.Report);
        ToolbarMetaText.Text = $"{stored.DisplayDate} · {stored.Report.Run.TransferMethod} · {context}";
        RenderPresentation(presentation);
    }

    public void RenderCurrent(StoredReport stored, ConnectionCheckPresentation presentation)
    {
        Render(stored, presentation);
        SetCurrentRunMode(true);
        var context = ReportComparisonService.ContextLabel(stored.Report);
        ToolbarMetaText.Text = $"Just completed · {stored.Report.Run.TransferMethod} · {context}";
    }

    public void RenderPreview(ConnectionCheckPresentation presentation)
    {
        SetCurrentRunMode(false);
        report = null;
        ToolbarTitleText.Text = "Quick";
        ToolbarMetaText.Text = "Preview data · Compare · Automatic routing · first-party endpoint";
        RenderPresentation(presentation);
    }

    public void RenderCurrentPreview(ConnectionCheckPresentation presentation)
    {
        report = null;
        ToolbarTitleText.Text = "Connection Check";
        ToolbarMetaText.Text = "Just completed · Aggregate · Automatic routing · first-party endpoint";
        RenderPresentation(presentation);
        SetCurrentRunMode(true);
    }

    private void RenderPresentation(ConnectionCheckPresentation presentation)
    {
        ResetReportDisclosureWidth();
        OutcomeLabelText.Text = presentation.Label.ToUpperInvariant();
        VerdictText.Text = presentation.Verdict;
        SummaryText.Text = presentation.Summary;
        NextActionText.Text = presentation.NextAction;
        HealthGroupCardFactory.ApplyOutcomeIndicator(OutcomeIndicator, presentation.Outcome);
        RenderSignals(presentation);
        RenderFindings(presentation);
        RenderEvidence(presentation.TechnicalEvidence);
        SetEvidenceExpanded(false);
        ApplyReportDetailPolish(Bounds.Width);
    }

    private void RenderSignals(ConnectionCheckPresentation presentation)
    {
        var groups = HealthGroupPresenter.Build(presentation)
            .ToDictionary(group => group.Kind);

        ApplySignal(
            groups[HealthGroupKind.Responsiveness],
            ResponsivenessSignalIndicator,
            ResponsivenessStateText,
            ResponsivenessSignalText,
            ResponsivenessMetricPanel);
        ApplySignal(
            groups[HealthGroupKind.Reliability],
            ReliabilitySignalIndicator,
            ReliabilityStateText,
            ReliabilitySignalText,
            ReliabilityMetricPanel);
        ApplySignal(
            groups[HealthGroupKind.Throughput],
            ThroughputSignalIndicator,
            ThroughputStateText,
            ThroughputSignalText,
            ThroughputMetricPanel);
    }

    private static void ApplySignal(
        HealthGroupPresentation group,
        Border indicator,
        TextBlock state,
        TextBlock summary,
        Grid metricPanel)
    {
        indicator.Classes.Remove("indicatorSuccess");
        indicator.Classes.Remove("indicatorAccent");
        indicator.Classes.Remove("indicatorNeutral");
        indicator.Classes.Add(group.Tone switch
        {
            HealthGroupTone.Positive => "indicatorSuccess",
            HealthGroupTone.Attention => "indicatorAccent",
            _ => "indicatorNeutral"
        });

        state.Text = group.State;
        summary.Text = group.Summary;
        RenderSignalMetrics(metricPanel, group.Metrics);
    }

    private static void RenderSignalMetrics(Grid panel, IReadOnlyList<MetricPresentation> metrics)
    {
        panel.Children.Clear();
        panel.ColumnDefinitions.Clear();
        panel.RowDefinitions.Clear();

        var visible = metrics.Where(metric => metric.WasMeasured).Take(2).ToArray();
        if (visible.Length == 0)
        {
            visible = metrics.Take(1).ToArray();
        }
        if (visible.Length == 0)
        {
            panel.IsVisible = false;
            return;
        }

        panel.IsVisible = true;
        panel.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var index = 0; index < visible.Length; index++)
        {
            panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var metric = BuildSignalMetric(visible[index]);
            Grid.SetColumn(metric, index);
            panel.Children.Add(metric);
        }
    }

    private static Control BuildSignalMetric(MetricPresentation metric)
    {
        var label = new TextBlock
        {
            Text = metric.Label.ToUpperInvariant(),
            FontSize = 8.5,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.7,
            LineHeight = 11
        };
        label.Classes.Add("muted");

        var value = new TextBlock
        {
            Text = metric.Value,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            LineHeight = 22,
            Opacity = metric.WasMeasured ? 1 : 0.58
        };

        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(label);
        stack.Children.Add(value);
        return stack;
    }

    private void RenderFindings(ConnectionCheckPresentation presentation)
    {
        FindingsPanel.Children.Clear();
        FindingsTitleText.Text = presentation.Outcome == ConnectionCheckOutcome.Healthy
            ? "Why this result looks normal"
            : "What needs attention";
        FindingsDetailText.Text = presentation.Outcome == ConnectionCheckOutcome.Healthy
            ? "Only evidence that materially supports the result is shown."
            : "Only findings that changed the interpretation are shown.";

        if (presentation.Findings.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No material findings were generated for this report.",
                FontSize = 11,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 10, 0, 6)
            };
            empty.Classes.Add("muted");
            FindingsPanel.Children.Add(empty);
            return;
        }

        foreach (var finding in presentation.Findings)
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
                FontSize = 10.5,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 10, 0, 10)
            };
            empty.Classes.Add("muted");
            EvidencePanel.Children.Add(empty);
            return;
        }

        foreach (var item in evidence)
        {
            EvidencePanel.Children.Add(BuildEvidenceRow(item));
        }
    }

    private static Control BuildEvidenceRow(string item)
    {
        var separator = item.IndexOf(':');
        var hasLabel = separator > 0 && separator <= 42;

        var row = new Grid
        {
            ColumnDefinitions = hasLabel
                ? new ColumnDefinitions("190,*")
                : new ColumnDefinitions("*"),
            ColumnSpacing = 18
        };

        if (hasLabel)
        {
            var label = new TextBlock
            {
                Text = item[..separator].Trim(),
                FontSize = 9.5,
                FontWeight = FontWeight.SemiBold,
                LineHeight = 15,
                TextWrapping = TextWrapping.Wrap
            };
            label.Classes.Add("muted");
            row.Children.Add(label);

            var value = new TextBlock
            {
                Text = item[(separator + 1)..].Trim(),
                FontSize = 10.5,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap
            };
            value.Classes.Add("secondary");
            Grid.SetColumn(value, 1);
            row.Children.Add(value);
        }
        else
        {
            var value = new TextBlock
            {
                Text = item,
                FontSize = 10.5,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap
            };
            value.Classes.Add("secondary");
            row.Children.Add(value);
        }

        var container = new Border
        {
            Padding = new Avalonia.Thickness(0, 10, 0, 12),
            Child = row
        };
        container.Classes.Add("divider");
        return container;
    }

    private void SetCurrentRunMode(bool currentRun)
    {
        RunAgainButton.IsVisible = currentRun;
        CompareButton.Classes.Remove("primary");
        CompareButton.Classes.Remove("secondary");
        CompareButton.Classes.Add(currentRun ? "secondary" : "primary");
    }

    private void EvidenceToggleClicked(object? sender, RoutedEventArgs eventArgs) =>
        SetEvidenceExpanded(!evidenceExpanded);

    private void SetEvidenceExpanded(bool expanded)
    {
        if (expanded)
        {
            LockReportWidthForEvidenceDisclosure();
        }

        evidenceExpanded = expanded;
        EvidenceBody.IsVisible = expanded;
        EvidenceToggleLabelText.Text = expanded ? "Hide details" : "Show details";
    }

    private void BackClicked(object? sender, RoutedEventArgs eventArgs) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void CloseClicked(object? sender, RoutedEventArgs eventArgs) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

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
            Text = finding.Label.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.7,
            LineHeight = 12
        };
        label.Classes.Add("eyebrow");

        var title = new TextBlock
        {
            Text = finding.Title,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap
        };

        var summary = new TextBlock
        {
            Text = finding.Summary,
            FontSize = 10.5,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap
        };
        summary.Classes.Add("secondary");

        var stack = new StackPanel { Spacing = 5 };
        stack.Children.Add(label);
        stack.Children.Add(title);
        stack.Children.Add(summary);

        var container = new Border
        {
            Padding = new Avalonia.Thickness(0, 12, 0, 14),
            Child = stack
        };
        container.Classes.Add("divider");
        return container;
    }
}

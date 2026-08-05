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
    }

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

        MetricsPanel.Children.Clear();
        foreach (var metric in presentation.Metrics)
        {
            MetricsPanel.Children.Add(BuildMetric(metric));
        }

        FindingsPanel.Children.Clear();
        if (presentation.Findings.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No diagnostic findings were generated for this report.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            FindingsPanel.Children.Add(empty);
        }
        else
        {
            foreach (var finding in presentation.Findings)
            {
                FindingsPanel.Children.Add(BuildFinding(finding));
            }
        }

        EvidencePanel.Children.Clear();
        foreach (var item in presentation.TechnicalEvidence)
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

    private static Border BuildMetric(MetricPresentation metric)
    {
        var label = new TextBlock
        {
            Text = metric.Label.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.2
        };
        label.Classes.Add("muted");

        var value = new TextBlock
        {
            Text = metric.Value,
            FontSize = 21,
            FontWeight = FontWeight.SemiBold,
            Opacity = metric.WasMeasured ? 1 : 0.62
        };

        var detail = new TextBlock
        {
            Text = metric.Detail,
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("muted");

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(label);
        stack.Children.Add(value);
        stack.Children.Add(detail);

        var card = new Border { Child = stack };
        card.Classes.Add("metricCard");
        if (!metric.WasMeasured) card.Classes.Add("unmeasured");
        return card;
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
            Padding = new Avalonia.Thickness(0, 3, 0, 12),
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
            Child = stack
        };
        row.Classes.Add("divider");
        return row;
    }
}

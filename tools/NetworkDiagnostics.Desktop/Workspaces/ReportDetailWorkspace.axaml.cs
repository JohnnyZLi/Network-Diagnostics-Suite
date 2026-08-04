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
            row.Children.Add(new TextBlock
            {
                Text = "•",
                Foreground = Brush.Parse("#C77E68"),
                FontSize = 12
            });
            var text = new TextBlock
            {
                Text = item,
                FontSize = 11,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap
            };
            text.Classes.Add("muted");
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
        var detail = new TextBlock
        {
            Text = metric.Detail,
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#969B9C")
        };
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = metric.Label.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.2,
            Foreground = Brush.Parse("#969B9C")
        });
        stack.Children.Add(new TextBlock
        {
            Text = metric.Value,
            FontSize = 21,
            FontWeight = FontWeight.SemiBold,
            Foreground = metric.WasMeasured ? Brush.Parse("#F0EDE7") : Brush.Parse("#969B9C")
        });
        stack.Children.Add(detail);

        return new Border
        {
            Width = 200,
            Height = 104,
            Margin = new Avalonia.Thickness(0, 0, 10, 10),
            Padding = new Avalonia.Thickness(14),
            Background = Brush.Parse("#171B1C"),
            BorderBrush = Brush.Parse(metric.WasMeasured ? "#303536" : "#3B3732"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(9),
            Child = stack
        };
    }

    private static Border BuildFinding(FindingPresentation finding)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = finding.Label.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.2,
            Foreground = Brush.Parse("#C77E68")
        });
        stack.Children.Add(new TextBlock
        {
            Text = finding.Title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = finding.Summary,
            FontSize = 12,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#969B9C")
        });

        return new Border
        {
            BorderBrush = Brush.Parse("#303536"),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Padding = new Avalonia.Thickness(0, 0, 0, 12),
            Child = stack
        };
    }
}

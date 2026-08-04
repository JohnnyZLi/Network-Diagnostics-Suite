using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ComparisonWorkspace : UserControl
{
    private IReadOnlyList<StoredReport> reports = [];
    private StoredReport? baseline;
    private StoredReport? candidate;

    public ComparisonWorkspace()
    {
        InitializeComponent();
        Render([], null, null, ReportComparisonService.AnalyzeTrend([]));
    }

    public event EventHandler? ClearRequested;

    public event EventHandler<StoredReportEventArgs>? BaselineRequested;

    public event EventHandler<StoredReportEventArgs>? CandidateRequested;

    public event EventHandler<StoredReportEventArgs>? OpenReportRequested;

    public event EventHandler<StoredReportEventArgs>? EditReportRequested;

    public void Render(
        IReadOnlyList<StoredReport> savedReports,
        Guid? baselineId,
        Guid? candidateId,
        ReportTrendResult trend)
    {
        reports = savedReports;
        baseline = baselineId is { } baselineValue
            ? reports.FirstOrDefault(item => item.Report.Run.Id == baselineValue)
            : null;
        candidate = candidateId is { } candidateValue
            ? reports.FirstOrDefault(item => item.Report.Run.Id == candidateValue)
            : null;

        ClearButton.IsEnabled = baseline is not null || candidate is not null;
        PickerInstructionText.Text = baseline is null
            ? "Select a baseline first."
            : candidate is null
                ? "Baseline selected. Choose a candidate."
                : "Both reports are selected. Change either side at any time.";

        RenderPicker();
        RenderSelectionCards();
        RenderComparison();
        TrendText.Text = trend.Summary;
    }

    private void ClearClicked(object? sender, RoutedEventArgs eventArgs) =>
        ClearRequested?.Invoke(this, EventArgs.Empty);

    private void OpenBaselineClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (baseline is not null)
        {
            OpenReportRequested?.Invoke(this, new StoredReportEventArgs(baseline));
        }
    }

    private void OpenCandidateClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (candidate is not null)
        {
            OpenReportRequested?.Invoke(this, new StoredReportEventArgs(candidate));
        }
    }

    private void OpenReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            OpenReportRequested?.Invoke(this, new StoredReportEventArgs(stored));
        }
    }

    private void SetBaselineClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            BaselineRequested?.Invoke(this, new StoredReportEventArgs(stored));
        }
    }

    private void SetCandidateClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            CandidateRequested?.Invoke(this, new StoredReportEventArgs(stored));
        }
    }

    private void EditReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            EditReportRequested?.Invoke(this, new StoredReportEventArgs(stored));
        }
    }

    private void RenderPicker()
    {
        ReportPickerPanel.Children.Clear();
        if (reports.Count == 0)
        {
            var empty = new TextBlock
            {
                Margin = new Avalonia.Thickness(16, 20),
                Text = "No saved reports are available. Complete or import a diagnostic first.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            ReportPickerPanel.Children.Add(empty);
            return;
        }

        foreach (var stored in reports)
        {
            ReportPickerPanel.Children.Add(BuildPickerRow(stored));
        }
    }

    private Border BuildPickerRow(StoredReport stored)
    {
        var isBaseline = baseline?.Report.Run.Id == stored.Report.Run.Id;
        var isCandidate = candidate?.Report.Run.Id == stored.Report.Run.Id;
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);

        var titlePanel = new StackPanel { Spacing = 3 };
        titlePanel.Children.Add(new TextBlock
        {
            Text = stored.Label ?? presentation.Verdict,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var meta = new TextBlock
        {
            Text = $"{stored.ProfileName} · {stored.Report.GeneratedAt.ToLocalTime():MMM d, h:mm tt}",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };
        meta.Classes.Add("muted");
        titlePanel.Children.Add(meta);

        var open = new Button
        {
            Content = titlePanel,
            Tag = stored,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        open.Classes.Add("pickerRow");
        if (isBaseline || isCandidate) open.Classes.Add("selected");
        open.Click += OpenReportClicked;

        var baselineButton = new Button
        {
            Content = isBaseline ? "Baseline" : "Set baseline",
            Tag = stored,
            IsEnabled = !isBaseline
        };
        baselineButton.Classes.Add("ghost");
        baselineButton.Click += SetBaselineClicked;

        var canSelectCandidate = baseline is not null && !isBaseline && !isCandidate;
        var candidateButton = new Button
        {
            Content = isCandidate ? "Candidate" : baseline is null ? "Choose baseline first" : "Set candidate",
            Tag = stored,
            IsEnabled = canSelectCandidate
        };
        candidateButton.Classes.Add(canSelectCandidate ? "action" : "ghost");
        candidateButton.Click += SetCandidateClicked;

        var editButton = new Button
        {
            Content = "Edit",
            Tag = stored
        };
        editButton.Classes.Add("ghost");
        editButton.Click += EditReportClicked;

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 7,
            Margin = new Avalonia.Thickness(12, 8, 12, 11)
        };
        actions.Children.Add(baselineButton);
        Grid.SetColumn(candidateButton, 1);
        actions.Children.Add(candidateButton);
        Grid.SetColumn(editButton, 2);
        actions.Children.Add(editButton);

        var stack = new StackPanel();
        stack.Children.Add(open);
        stack.Children.Add(actions);
        return new Border
        {
            BorderBrush = Brush.Parse("#2B3031"),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Child = stack
        };
    }

    private void RenderSelectionCards()
    {
        if (baseline is null)
        {
            BaselineTitleText.Text = "Not selected";
            BaselineMetaText.Text = "Choose the report that represents the reference condition.";
            OpenBaselineButton.IsEnabled = false;
        }
        else
        {
            var presentation = DiagnosticReportPresenter.FromReport(baseline.Report);
            BaselineTitleText.Text = baseline.Label ?? presentation.Verdict;
            BaselineMetaText.Text = $"{baseline.ProfileName} · {baseline.DisplayDate}\n{ReportComparisonService.ContextLabel(baseline.Report)}";
            OpenBaselineButton.IsEnabled = true;
        }

        if (candidate is null)
        {
            CandidateTitleText.Text = "Not selected";
            CandidateMetaText.Text = baseline is null
                ? "Choose a baseline before selecting the candidate."
                : "Choose a second report to measure change.";
            OpenCandidateButton.IsEnabled = false;
        }
        else
        {
            var presentation = DiagnosticReportPresenter.FromReport(candidate.Report);
            CandidateTitleText.Text = candidate.Label ?? presentation.Verdict;
            CandidateMetaText.Text = $"{candidate.ProfileName} · {candidate.DisplayDate}\n{ReportComparisonService.ContextLabel(candidate.Report)}";
            OpenCandidateButton.IsEnabled = true;
        }
    }

    private void RenderComparison()
    {
        MetricRowsPanel.Children.Clear();
        if (baseline is null || candidate is null)
        {
            CompatibilityIndicator.Background = Brush.Parse("#777D7E");
            CompatibilityTitleText.Text = "Waiting for two reports";
            CompatibilityDetailText.Text = "Compatibility checks compare profile, method, endpoint, interface, and transfer ceiling.";
            ComparisonSummaryText.Text = baseline is null
                ? "Select a baseline, then choose a candidate report."
                : "Baseline selected. Choose the candidate report from the left pane.";
            AddEmptyMetricRow("Metrics appear after both reports are selected.");
            return;
        }

        var comparison = ReportComparisonService.Compare(baseline.Report, candidate.Report);
        CompatibilityIndicator.Background = Brush.Parse(comparison.Comparable ? "#72A17B" : "#C77E68");
        CompatibilityTitleText.Text = comparison.Comparable
            ? "Equivalent test context"
            : "Comparison includes cautions";
        CompatibilityDetailText.Text = comparison.Warnings.Count == 0
            ? "Profile, transfer method, endpoint, interface, and transfer ceiling align."
            : string.Join(" ", comparison.Warnings);
        ComparisonSummaryText.Text = comparison.Summary;

        if (comparison.Metrics.Count == 0)
        {
            AddEmptyMetricRow("The selected reports do not share comparable measurements.");
            return;
        }

        foreach (var metric in comparison.Metrics)
        {
            MetricRowsPanel.Children.Add(BuildMetricRow(metric));
        }
    }

    private static Border BuildMetricRow(ReportMetricDelta metric)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,130,130,190"),
            ColumnSpacing = 12
        };
        grid.Children.Add(new TextBlock
        {
            Text = metric.Label,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var baselineText = MetricText(metric.Baseline);
        Grid.SetColumn(baselineText, 1);
        grid.Children.Add(baselineText);
        var candidateText = MetricText(metric.Candidate);
        Grid.SetColumn(candidateText, 2);
        grid.Children.Add(candidateText);
        var change = MetricText(metric.Change);
        change.Foreground = Brush.Parse(metric.Change.Contains("improved", StringComparison.OrdinalIgnoreCase)
            ? "#79A982"
            : metric.Change.Contains("worsened", StringComparison.OrdinalIgnoreCase)
                ? "#D1846D"
                : "#B7BCBC");
        Grid.SetColumn(change, 3);
        grid.Children.Add(change);

        return new Border
        {
            BorderBrush = Brush.Parse("#2B3031"),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            Padding = new Avalonia.Thickness(14, 11),
            Child = grid
        };
    }

    private void AddEmptyMetricRow(string text)
    {
        var label = new TextBlock
        {
            Margin = new Avalonia.Thickness(14, 18),
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        label.Classes.Add("muted");
        MetricRowsPanel.Children.Add(label);
    }

    private static TextBlock MetricText(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = Brush.Parse("#B7BCBC"),
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap
    };
}

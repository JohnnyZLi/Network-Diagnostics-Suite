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
        SelectionGrid.ColumnDefinitions = new ColumnDefinitions("*,180,*");
        ComparisonBody.ColumnDefinitions = new ColumnDefinitions("400,*");
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

        var libraryEmpty = reports.Count == 0;
        var hasSelection = baseline is not null || candidate is not null;
        EmptyLibraryPanel.IsVisible = libraryEmpty;
        SelectionGrid.IsVisible = !libraryEmpty;
        ComparisonBody.IsVisible = !libraryEmpty;
        ClearButton.IsVisible = !libraryEmpty && hasSelection;
        ClearButton.IsEnabled = hasSelection;
        PickerInstructionText.Text = baseline is null
            ? "Choose the reference condition."
            : candidate is null
                ? "Reference selected. Choose the changed condition."
                : "Both conditions are selected. You can replace either one.";

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
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var meta = new TextBlock
        {
            Text = $"{stored.ProfileName} · {stored.Report.GeneratedAt.ToLocalTime():MMM d, h:mm tt}",
            FontSize = 9.5,
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
        open.Classes.Add("dataRow");
        if (isBaseline || isCandidate) open.Classes.Add("selected");
        open.Click += OpenReportClicked;

        var baselineButton = new Button
        {
            Content = isBaseline ? "Baseline" : "Use as baseline",
            Tag = stored,
            IsEnabled = !isBaseline
        };
        baselineButton.Classes.Add("secondary");
        baselineButton.Classes.Add("pickerAction");
        baselineButton.Click += SetBaselineClicked;

        var candidateButton = new Button
        {
            Content = isCandidate ? "Candidate" : "Use as candidate",
            Tag = stored,
            IsVisible = baseline is not null && !isBaseline,
            IsEnabled = !isCandidate
        };
        candidateButton.Classes.Add(isCandidate ? "secondary" : "primary");
        candidateButton.Classes.Add("pickerAction");
        candidateButton.Click += SetCandidateClicked;

        var editButton = new Button
        {
            Content = "Edit",
            Tag = stored,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        editButton.Classes.Add("linkAction");
        editButton.Classes.Add("pickerAction");
        editButton.Click += EditReportClicked;

        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            ColumnSpacing = 7,
            Margin = new Avalonia.Thickness(14, 6, 14, 10)
        };
        actions.Children.Add(baselineButton);
        Grid.SetColumn(candidateButton, 1);
        actions.Children.Add(candidateButton);
        Grid.SetColumn(editButton, 2);
        actions.Children.Add(editButton);

        var stack = new StackPanel();
        stack.Children.Add(open);
        stack.Children.Add(actions);
        var row = new Border { Child = stack };
        row.Classes.Add("divider");
        return row;
    }

    private void RenderSelectionCards()
    {
        if (baseline is null)
        {
            BaselineTitleText.Text = "Not selected";
            BaselineMetaText.Text = "Choose the report that represents the reference condition.";
            OpenBaselineButton.IsVisible = false;
            OpenBaselineButton.IsEnabled = false;
        }
        else
        {
            var presentation = DiagnosticReportPresenter.FromReport(baseline.Report);
            BaselineTitleText.Text = baseline.Label ?? presentation.Verdict;
            BaselineMetaText.Text = $"{baseline.ProfileName} · {baseline.DisplayDate}\n{ReportComparisonService.ContextLabel(baseline.Report)}";
            OpenBaselineButton.IsVisible = true;
            OpenBaselineButton.IsEnabled = true;
        }

        if (candidate is null)
        {
            CandidateTitleText.Text = "Not selected";
            CandidateMetaText.Text = baseline is null
                ? "Choose a baseline before selecting the candidate."
                : "Choose a second report to measure change.";
            OpenCandidateButton.IsVisible = false;
            OpenCandidateButton.IsEnabled = false;
        }
        else
        {
            var presentation = DiagnosticReportPresenter.FromReport(candidate.Report);
            CandidateTitleText.Text = candidate.Label ?? presentation.Verdict;
            CandidateMetaText.Text = $"{candidate.ProfileName} · {candidate.DisplayDate}\n{ReportComparisonService.ContextLabel(candidate.Report)}";
            OpenCandidateButton.IsVisible = true;
            OpenCandidateButton.IsEnabled = true;
        }
    }

    private void RenderComparison()
    {
        MetricRowsPanel.Children.Clear();
        if (baseline is null || candidate is null)
        {
            SetIndicatorClass("indicatorNeutral");
            CompatibilityTitleText.Text = "Waiting for two reports";
            CompatibilityDetailText.Text = "Profile, method, endpoint, interface, and transfer ceiling are checked before comparison.";
            ComparisonSummaryText.Text = baseline is null
                ? "Choose a baseline, then choose a candidate report."
                : "Baseline selected. Choose the candidate from Saved diagnostics.";
            AddEmptyMetricRow("Metrics appear after both reports are selected.");
            return;
        }

        var comparison = ReportComparisonService.Compare(baseline.Report, candidate.Report);
        SetIndicatorClass(comparison.Comparable ? "indicatorSuccess" : "indicatorAccent");
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

    private void SetIndicatorClass(string className)
    {
        CompatibilityIndicator.Classes.Remove("indicatorNeutral");
        CompatibilityIndicator.Classes.Remove("indicatorAccent");
        CompatibilityIndicator.Classes.Remove("indicatorSuccess");
        CompatibilityIndicator.Classes.Add(className);
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
        change.Classes.Remove("secondary");
        change.Classes.Add(metric.Change.Contains("improved", StringComparison.OrdinalIgnoreCase)
            ? "deltaPositive"
            : metric.Change.Contains("worsened", StringComparison.OrdinalIgnoreCase)
                ? "deltaNegative"
                : "deltaNeutral");
        Grid.SetColumn(change, 3);
        grid.Children.Add(change);

        var row = new Border
        {
            Padding = new Avalonia.Thickness(14, 11),
            Child = grid
        };
        row.Classes.Add("divider");
        return row;
    }

    private void AddEmptyMetricRow(string text)
    {
        var label = new TextBlock
        {
            Margin = new Avalonia.Thickness(14, 18),
            Text = text,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        label.Classes.Add("muted");
        MetricRowsPanel.Children.Add(label);
    }

    private static TextBlock MetricText(string text)
    {
        var value = new TextBlock
        {
            Text = text,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        value.Classes.Add("secondary");
        return value;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed record ControlCenterSectionModel(
    IReadOnlyList<StoredReport> Reports,
    Guid? BaselineId,
    Guid? CandidateId,
    ReportTrendResult Trend);

public sealed partial class TestSetupWorkspace
{
    private Border? recentDiagnosticsSection;
    private Border? inlineComparisonSection;
    private StackPanel? recentRowsPanel;
    private StackPanel? comparisonMetricRows;
    private TextBlock? recentCountText;
    private TextBlock? comparisonInstructionText;
    private TextBlock? comparisonSummaryText;
    private TextBlock? comparisonTrendText;
    private TextBlock? baselineTitleText;
    private TextBlock? baselineMetaText;
    private TextBlock? candidateTitleText;
    private TextBlock? candidateMetaText;
    private Button? compareModeButton;
    private bool compareModeExpanded;
    private ControlCenterSectionModel controlCenterModel = new([], null, null, ReportComparisonService.AnalyzeTrend([]));

    public event EventHandler<StoredReportEventArgs>? RecentReportRequested;
    public event EventHandler<StoredReportEventArgs>? RecentReportEditRequested;
    public event EventHandler<StoredReportEventArgs>? InlineBaselineRequested;
    public event EventHandler<StoredReportEventArgs>? InlineCandidateRequested;
    public event EventHandler? InlineComparisonClearRequested;
    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? ImportHistoryRequested;
    public event EventHandler? OpenHistoryFolderRequested;

    public void RenderControlCenter(ControlCenterSectionModel model)
    {
        controlCenterModel = model;
        EnsureControlCenterSections();
        RenderRecentDiagnostics();
        RenderInlineComparison();
    }

    private void EnsureControlCenterSections()
    {
        if (recentDiagnosticsSection is not null) return;

        while (OverviewGrid.RowDefinitions.Count < 4)
        {
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        recentDiagnosticsSection = BuildRecentDiagnosticsSection();
        Grid.SetRow(recentDiagnosticsSection, 2);
        Grid.SetColumnSpan(recentDiagnosticsSection, 2);
        OverviewGrid.Children.Add(recentDiagnosticsSection);

        inlineComparisonSection = BuildInlineComparisonSection();
        Grid.SetRow(inlineComparisonSection, 3);
        Grid.SetColumnSpan(inlineComparisonSection, 2);
        OverviewGrid.Children.Add(inlineComparisonSection);
    }

    private Border BuildRecentDiagnosticsSection()
    {
        recentRowsPanel = new StackPanel { Spacing = 0 };
        recentCountText = new TextBlock
        {
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        recentCountText.Classes.Add("muted");

        compareModeButton = ActionButton("Compare reports", "secondary", CompareModeClicked);
        var libraryButton = ActionButton("Open library", "ghost", (_, _) => OpenHistoryRequested?.Invoke(this, EventArgs.Empty));
        var folderButton = ActionButton("Open folder", "ghost", (_, _) => OpenHistoryFolderRequested?.Invoke(this, EventArgs.Empty));
        var importButton = ActionButton("Import JSON", "primary", (_, _) => ImportHistoryRequested?.Invoke(this, EventArgs.Empty));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(recentCountText);
        actions.Children.Add(folderButton);
        actions.Children.Add(libraryButton);
        actions.Children.Add(compareModeButton);
        actions.Children.Add(importButton);

        var title = new StackPanel { Spacing = 3 };
        var eyebrow = new TextBlock { Text = "RECENT DIAGNOSTICS" };
        eyebrow.Classes.Add("eyebrow");
        title.Children.Add(eyebrow);
        title.Children.Add(new TextBlock
        {
            Text = "Saved network conditions",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        });
        var detail = new TextBlock
        {
            Text = "Open evidence, add context, or select two runs without leaving the live dashboard.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("secondary");
        title.Children.Add(detail);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        header.Children.Add(title);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(header);
        var divider = new Border();
        divider.Classes.Add("divider");
        stack.Children.Add(divider);
        stack.Children.Add(recentRowsPanel);

        var border = new Border
        {
            Padding = new Thickness(18),
            Margin = new Thickness(0, 10, 0, 0),
            Child = stack
        };
        border.Classes.Add("dashboardPanel");
        return border;
    }

    private Border BuildInlineComparisonSection()
    {
        baselineTitleText = HeadingText("Not selected", 15);
        baselineMetaText = BodyText("Choose a recent diagnostic as the reference condition.", muted: true);
        candidateTitleText = HeadingText("Not selected", 15);
        candidateMetaText = BodyText("Choose a baseline first, then select the changed condition.", muted: true);
        comparisonInstructionText = BodyText("Select a baseline and candidate from Recent diagnostics.", muted: true);
        comparisonSummaryText = HeadingText("Choose two reports to compare network conditions.", 16);
        comparisonTrendText = BodyText(string.Empty, muted: true);
        comparisonMetricRows = new StackPanel();

        var baselineCard = SelectionCard("BASELINE", baselineTitleText, baselineMetaText);
        var candidateCard = SelectionCard("CANDIDATE", candidateTitleText, candidateMetaText);
        var selectionGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        selectionGrid.Children.Add(baselineCard);
        Grid.SetColumn(candidateCard, 1);
        selectionGrid.Children.Add(candidateCard);

        var headerTitle = new StackPanel { Spacing = 3 };
        var eyebrow = new TextBlock { Text = "COMPARE" };
        eyebrow.Classes.Add("eyebrow");
        headerTitle.Children.Add(eyebrow);
        headerTitle.Children.Add(new TextBlock
        {
            Text = "Compare network conditions",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        });
        headerTitle.Children.Add(comparisonInstructionText);

        var clear = ActionButton("Clear", "ghost", (_, _) => InlineComparisonClearRequested?.Invoke(this, EventArgs.Empty));
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        header.Children.Add(headerTitle);
        Grid.SetColumn(clear, 1);
        header.Children.Add(clear);

        var summary = new Border
        {
            Background = this.FindResource("AppSelectedBrush") as IBrush,
            BorderBrush = this.FindResource("AppAccentBorderBrush") as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(16),
            Child = comparisonSummaryText
        };

        var metricHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,130,130,190"),
            ColumnSpacing = 12,
            Margin = new Thickness(14, 10)
        };
        AddMetricHeader(metricHeader, "METRIC", 0);
        AddMetricHeader(metricHeader, "BASELINE", 1);
        AddMetricHeader(metricHeader, "CANDIDATE", 2);
        AddMetricHeader(metricHeader, "CHANGE", 3);

        var metricTable = new Border
        {
            Background = this.FindResource("AppInsetBrush") as IBrush,
            BorderBrush = this.FindResource("AppBorderBrush") as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            ClipToBounds = true,
            Child = new StackPanel
            {
                Children =
                {
                    metricHeader,
                    comparisonMetricRows
                }
            }
        };

        var trend = new Border
        {
            Background = this.FindResource("AppInsetBrush") as IBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 11),
            Child = comparisonTrendText
        };

        var stack = new StackPanel { Spacing = 13 };
        stack.Children.Add(header);
        stack.Children.Add(selectionGrid);
        stack.Children.Add(summary);
        stack.Children.Add(metricTable);
        stack.Children.Add(trend);

        var border = new Border
        {
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 18),
            Child = stack,
            IsVisible = false
        };
        border.Classes.Add("dashboardPanel");
        return border;
    }

    private void RenderRecentDiagnostics()
    {
        if (recentRowsPanel is null || recentCountText is null || compareModeButton is null) return;
        recentRowsPanel.Children.Clear();
        var reports = controlCenterModel.Reports
            .OrderByDescending(item => item.Report.GeneratedAt)
            .Take(5)
            .ToArray();
        recentCountText.Text = controlCenterModel.Reports.Count == 0
            ? "No saved reports"
            : $"Latest {reports.Length} of {controlCenterModel.Reports.Count}";
        compareModeButton.Content = compareModeExpanded ? "Close compare" : "Compare reports";

        if (reports.Length == 0)
        {
            var empty = new Border
            {
                Background = this.FindResource("AppInsetBrush") as IBrush,
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(22),
                Child = BodyText("Completed diagnostics will appear here automatically. Run Connection Check or import an existing schema 2.0 report.", muted: true)
            };
            recentRowsPanel.Children.Add(empty);
            return;
        }

        foreach (var stored in reports)
        {
            recentRowsPanel.Children.Add(BuildRecentRow(stored));
        }
    }

    private Control BuildRecentRow(StoredReport stored)
    {
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);
        var selectedAsBaseline = controlCenterModel.BaselineId == stored.Report.Run.Id;
        var selectedAsCandidate = controlCenterModel.CandidateId == stored.Report.Run.Id;

        var date = BodyText(stored.Report.GeneratedAt.ToLocalTime().ToString("MMM d, h:mm tt"), muted: true);
        var profile = BodyText(stored.ProfileName, muted: true);
        profile.FontWeight = FontWeight.SemiBold;
        var resultStack = new StackPanel { Spacing = 2 };
        resultStack.Children.Add(HeadingText(stored.Label ?? presentation.Verdict, 11.5));
        if (!string.IsNullOrWhiteSpace(stored.Label)) resultStack.Children.Add(BodyText(presentation.Verdict, muted: true));
        var context = BodyText(ReportComparisonService.ContextLabel(stored.Report), muted: true);
        context.TextTrimming = TextTrimming.CharacterEllipsis;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(TaggedButton("Open", "ghost", stored, OpenRecentClicked));
        actions.Children.Add(TaggedButton("Label", "linkAction", stored, EditRecentClicked));
        if (compareModeExpanded)
        {
            var baseline = TaggedButton(selectedAsBaseline ? "Baseline" : "Set baseline", "secondary", stored, SetInlineBaselineClicked);
            baseline.IsEnabled = !selectedAsBaseline;
            actions.Children.Add(baseline);
            if (controlCenterModel.BaselineId is not null && !selectedAsBaseline)
            {
                var candidate = TaggedButton(selectedAsCandidate ? "Candidate" : "Set candidate", selectedAsCandidate ? "secondary" : "primary", stored, SetInlineCandidateClicked);
                candidate.IsEnabled = !selectedAsCandidate;
                actions.Children.Add(candidate);
            }
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("126,100,1.2*,0.9*,Auto"),
            ColumnSpacing = 13,
            Margin = new Thickness(14, 10)
        };
        grid.Children.Add(date);
        Grid.SetColumn(profile, 1);
        grid.Children.Add(profile);
        Grid.SetColumn(resultStack, 2);
        grid.Children.Add(resultStack);
        Grid.SetColumn(context, 3);
        grid.Children.Add(context);
        Grid.SetColumn(actions, 4);
        grid.Children.Add(actions);

        var row = new Border { Child = grid };
        row.Classes.Add("divider");
        if (selectedAsBaseline || selectedAsCandidate)
        {
            row.Background = this.FindResource("AppSelectedBrush") as IBrush;
        }
        return row;
    }

    private void RenderInlineComparison()
    {
        if (inlineComparisonSection is null
            || baselineTitleText is null
            || baselineMetaText is null
            || candidateTitleText is null
            || candidateMetaText is null
            || comparisonInstructionText is null
            || comparisonSummaryText is null
            || comparisonTrendText is null
            || comparisonMetricRows is null)
        {
            return;
        }

        var baseline = controlCenterModel.BaselineId is { } baselineId
            ? controlCenterModel.Reports.FirstOrDefault(item => item.Report.Run.Id == baselineId)
            : null;
        var candidate = controlCenterModel.CandidateId is { } candidateId
            ? controlCenterModel.Reports.FirstOrDefault(item => item.Report.Run.Id == candidateId)
            : null;
        inlineComparisonSection.IsVisible = compareModeExpanded || baseline is not null || candidate is not null;

        baselineTitleText.Text = baseline is null ? "Not selected" : baseline.Label ?? DiagnosticReportPresenter.FromReport(baseline.Report).Verdict;
        baselineMetaText.Text = baseline is null
            ? "Choose a recent diagnostic as the reference condition."
            : $"{baseline.ProfileName} · {baseline.DisplayDate}\n{ReportComparisonService.ContextLabel(baseline.Report)}";
        candidateTitleText.Text = candidate is null ? "Not selected" : candidate.Label ?? DiagnosticReportPresenter.FromReport(candidate.Report).Verdict;
        candidateMetaText.Text = candidate is null
            ? baseline is null ? "Choose a baseline first." : "Choose a second diagnostic as the changed condition."
            : $"{candidate.ProfileName} · {candidate.DisplayDate}\n{ReportComparisonService.ContextLabel(candidate.Report)}";
        comparisonInstructionText.Text = baseline is null
            ? "Choose the reference condition from Recent diagnostics."
            : candidate is null
                ? "Reference selected. Choose the changed condition."
                : "Both conditions are selected. Replace either one from the report list.";
        comparisonTrendText.Text = $"Local trend · {controlCenterModel.Trend.Summary}";
        comparisonMetricRows.Children.Clear();

        if (baseline is null || candidate is null)
        {
            comparisonSummaryText.Text = baseline is null
                ? "Select a baseline, then choose a candidate report."
                : "Baseline selected. Choose the candidate from Recent diagnostics.";
            AddEmptyComparisonRow("Metrics appear after both reports are selected.");
            return;
        }

        var comparison = ReportComparisonService.Compare(baseline.Report, candidate.Report);
        comparisonSummaryText.Text = comparison.Summary;
        if (comparison.Metrics.Count == 0)
        {
            AddEmptyComparisonRow("The selected reports do not share comparable measurements.");
            return;
        }

        foreach (var metric in comparison.Metrics)
        {
            comparisonMetricRows.Children.Add(BuildComparisonMetricRow(metric));
        }
    }

    private void CompareModeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        compareModeExpanded = !compareModeExpanded;
        RenderRecentDiagnostics();
        RenderInlineComparison();
        if (compareModeExpanded)
        {
            Dispatcher.UIThread.Post(() => inlineComparisonSection?.BringIntoView());
        }
    }

    private void OpenRecentClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored }) RecentReportRequested?.Invoke(this, new StoredReportEventArgs(stored));
    }

    private void EditRecentClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored }) RecentReportEditRequested?.Invoke(this, new StoredReportEventArgs(stored));
    }

    private void SetInlineBaselineClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored }) InlineBaselineRequested?.Invoke(this, new StoredReportEventArgs(stored));
    }

    private void SetInlineCandidateClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored }) InlineCandidateRequested?.Invoke(this, new StoredReportEventArgs(stored));
    }

    private static Border SelectionCard(string eyebrowText, TextBlock title, TextBlock meta)
    {
        var eyebrow = new TextBlock { Text = eyebrowText };
        eyebrow.Classes.Add("eyebrow");
        var stack = new StackPanel { Spacing = 5 };
        stack.Children.Add(eyebrow);
        stack.Children.Add(title);
        stack.Children.Add(meta);
        var card = new Border
        {
            Background = title.FindResource("AppInsetBrush") as IBrush,
            BorderBrush = title.FindResource("AppBorderBrush") as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(15),
            Child = stack
        };
        return card;
    }

    private static Button ActionButton(string text, string className, EventHandler<RoutedEventArgs> handler)
    {
        var button = new Button { Content = text };
        button.Classes.Add(className);
        button.Click += handler;
        return button;
    }

    private static Button TaggedButton(string text, string className, StoredReport stored, EventHandler<RoutedEventArgs> handler)
    {
        var button = ActionButton(text, className, handler);
        button.Tag = stored;
        return button;
    }

    private static TextBlock HeadingText(string text, double size) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock BodyText(string text, bool muted)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (muted) block.Classes.Add("muted");
        return block;
    }

    private static void AddMetricHeader(Grid grid, string text, int column)
    {
        var header = new TextBlock
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1
        };
        header.Classes.Add("muted");
        Grid.SetColumn(header, column);
        grid.Children.Add(header);
    }

    private Border BuildComparisonMetricRow(ReportMetricDelta metric)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,130,130,190"),
            ColumnSpacing = 12
        };
        grid.Children.Add(HeadingText(metric.Label, 10.5));
        var baseline = BodyText(metric.Baseline, muted: false);
        Grid.SetColumn(baseline, 1);
        grid.Children.Add(baseline);
        var candidate = BodyText(metric.Candidate, muted: false);
        Grid.SetColumn(candidate, 2);
        grid.Children.Add(candidate);
        var change = BodyText(metric.Change, muted: false);
        change.Classes.Add(metric.Change.Contains("improved", StringComparison.OrdinalIgnoreCase)
            ? "deltaPositive"
            : metric.Change.Contains("worsened", StringComparison.OrdinalIgnoreCase)
                ? "deltaNegative"
                : "deltaNeutral");
        Grid.SetColumn(change, 3);
        grid.Children.Add(change);
        var row = new Border { Padding = new Thickness(14, 10), Child = grid };
        row.Classes.Add("divider");
        return row;
    }

    private void AddEmptyComparisonRow(string text)
    {
        if (comparisonMetricRows is null) return;
        comparisonMetricRows.Children.Add(new Border
        {
            Padding = new Thickness(14, 18),
            Child = BodyText(text, muted: true)
        });
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
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
    private StackPanel? controlCenterPage;
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
        if (!EnsureControlCenterSections())
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (EnsureControlCenterSections())
                {
                    RenderRecentDiagnostics();
                    RenderInlineComparison();
                }
            });
            return;
        }

        RenderRecentDiagnostics();
        RenderInlineComparison();
    }

    private bool EnsureControlCenterSections()
    {
        if (recentDiagnosticsSection is not null) return true;
        if (OverviewGrid.GetLogicalParent() is not ScrollViewer scrollViewer) return false;

        scrollViewer.Content = null;
        controlCenterPage = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        controlCenterPage.Children.Add(OverviewGrid);

        recentDiagnosticsSection = BuildRecentDiagnosticsSection();
        inlineComparisonSection = BuildInlineComparisonSection();
        controlCenterPage.Children.Add(recentDiagnosticsSection);
        controlCenterPage.Children.Add(inlineComparisonSection);
        scrollViewer.Content = controlCenterPage;
        return true;
    }

    private Border BuildRecentDiagnosticsSection()
    {
        recentRowsPanel = new StackPanel();
        recentCountText = MutedText(string.Empty);
        compareModeButton = ActionButton("Compare reports", "secondary", CompareModeClicked);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(recentCountText);
        actions.Children.Add(ActionButton("Open folder", "ghost", (_, _) => OpenHistoryFolderRequested?.Invoke(this, EventArgs.Empty)));
        actions.Children.Add(ActionButton("Open library", "ghost", (_, _) => OpenHistoryRequested?.Invoke(this, EventArgs.Empty)));
        actions.Children.Add(compareModeButton);
        actions.Children.Add(ActionButton("Import JSON", "primary", (_, _) => ImportHistoryRequested?.Invoke(this, EventArgs.Empty)));
        foreach (var child in actions.Children.OfType<Control>()) child.Margin = new Thickness(0, 0, 7, 6);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        header.Children.Add(SectionTitle(
            "RECENT DIAGNOSTICS",
            "Saved network conditions",
            "Open evidence, add context, or compare two runs without leaving the live dashboard."));
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);

        var stack = new StackPanel { Spacing = 13 };
        stack.Children.Add(header);
        stack.Children.Add(Divider());
        stack.Children.Add(recentRowsPanel);

        var border = Panel(stack, new Thickness(18));
        border.MaxWidth = 1360;
        border.HorizontalAlignment = HorizontalAlignment.Stretch;
        border.Margin = new Thickness(24, 0, 24, 0);
        return border;
    }

    private Border BuildInlineComparisonSection()
    {
        baselineTitleText = Heading("Not selected", 15);
        baselineMetaText = MutedText("Choose a recent diagnostic as the reference condition.");
        candidateTitleText = Heading("Not selected", 15);
        candidateMetaText = MutedText("Choose a baseline first, then select the changed condition.");
        comparisonInstructionText = MutedText("Select a baseline and candidate from Recent diagnostics.");
        comparisonSummaryText = Heading("Choose two reports to compare network conditions.", 16);
        comparisonTrendText = MutedText(string.Empty);
        comparisonMetricRows = new StackPanel();

        var selectionGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        selectionGrid.Children.Add(SelectionCard("BASELINE", baselineTitleText, baselineMetaText));
        var candidateCard = SelectionCard("CANDIDATE", candidateTitleText, candidateMetaText);
        Grid.SetColumn(candidateCard, 1);
        selectionGrid.Children.Add(candidateCard);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        header.Children.Add(SectionTitle("COMPARE", "Compare network conditions", comparisonInstructionText.Text ?? string.Empty, comparisonInstructionText));
        var clear = ActionButton("Clear", "ghost", (_, _) => InlineComparisonClearRequested?.Invoke(this, EventArgs.Empty));
        Grid.SetColumn(clear, 1);
        header.Children.Add(clear);

        var summary = new Border
        {
            Padding = new Thickness(16),
            Child = comparisonSummaryText
        };
        summary.Classes.Add("accentSurface");

        var metricHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,*,*,1.15*"),
            ColumnSpacing = 12,
            Margin = new Thickness(14, 10)
        };
        AddMetricHeader(metricHeader, "METRIC", 0);
        AddMetricHeader(metricHeader, "BASELINE", 1);
        AddMetricHeader(metricHeader, "CANDIDATE", 2);
        AddMetricHeader(metricHeader, "CHANGE", 3);

        var metricStack = new StackPanel();
        metricStack.Children.Add(metricHeader);
        metricStack.Children.Add(comparisonMetricRows);
        var metricTable = Panel(metricStack, new Thickness(0));
        metricTable.ClipToBounds = true;

        var trend = Panel(comparisonTrendText, new Thickness(14, 11));

        var content = new StackPanel { Spacing = 13 };
        content.Children.Add(header);
        content.Children.Add(selectionGrid);
        content.Children.Add(summary);
        content.Children.Add(metricTable);
        content.Children.Add(trend);

        var border = Panel(content, new Thickness(18));
        border.MaxWidth = 1360;
        border.HorizontalAlignment = HorizontalAlignment.Stretch;
        border.Margin = new Thickness(24, 0, 24, 36);
        border.IsVisible = false;
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
            recentRowsPanel.Children.Add(Panel(
                MutedText("Completed diagnostics will appear here automatically. Run Connection Check or import an existing schema 2.0 report."),
                new Thickness(18)));
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
        var isBaseline = controlCenterModel.BaselineId == stored.Report.Run.Id;
        var isCandidate = controlCenterModel.CandidateId == stored.Report.Run.Id;

        var title = Heading(stored.Label ?? presentation.Verdict, 11.5);
        var metadata = MutedText($"{stored.ProfileName} · {stored.Report.GeneratedAt.ToLocalTime():MMM d, h:mm tt}");
        var context = MutedText(ReportComparisonService.ContextLabel(stored.Report));
        context.TextTrimming = TextTrimming.CharacterEllipsis;

        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(title);
        text.Children.Add(metadata);
        text.Children.Add(context);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddAction(actions, TaggedButton("Open", "ghost", stored, OpenRecentClicked));
        AddAction(actions, TaggedButton("Label", "linkAction", stored, EditRecentClicked));
        if (compareModeExpanded)
        {
            var baseline = TaggedButton(isBaseline ? "Baseline" : "Set baseline", "secondary", stored, SetInlineBaselineClicked);
            baseline.IsEnabled = !isBaseline;
            AddAction(actions, baseline);
            if (controlCenterModel.BaselineId is not null && !isBaseline)
            {
                var candidate = TaggedButton(isCandidate ? "Candidate" : "Set candidate", isCandidate ? "secondary" : "primary", stored, SetInlineCandidateClicked);
                candidate.IsEnabled = !isCandidate;
                AddAction(actions, candidate);
            }
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Margin = new Thickness(14, 10)
        };
        grid.Children.Add(text);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        var row = new Border { Child = grid };
        row.Classes.Add(isBaseline || isCandidate ? "accentSurface" : "divider");
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

        var baseline = FindReport(controlCenterModel.BaselineId);
        var candidate = FindReport(controlCenterModel.CandidateId);
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

    private StoredReport? FindReport(Guid? id) => id is { } value
        ? controlCenterModel.Reports.FirstOrDefault(item => item.Report.Run.Id == value)
        : null;

    private void CompareModeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        compareModeExpanded = !compareModeExpanded;
        RenderRecentDiagnostics();
        RenderInlineComparison();
        if (compareModeExpanded) Dispatcher.UIThread.Post(() => inlineComparisonSection?.BringIntoView());
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

    private static StackPanel SectionTitle(string eyebrowText, string titleText, string detailText, TextBlock? existingDetail = null)
    {
        var eyebrow = new TextBlock { Text = eyebrowText };
        eyebrow.Classes.Add("eyebrow");
        var title = Heading(titleText, 20);
        var detail = existingDetail ?? MutedText(detailText);
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(eyebrow);
        stack.Children.Add(title);
        stack.Children.Add(detail);
        return stack;
    }

    private static Border SelectionCard(string eyebrowText, TextBlock title, TextBlock meta)
    {
        var eyebrow = new TextBlock { Text = eyebrowText };
        eyebrow.Classes.Add("eyebrow");
        var stack = new StackPanel { Spacing = 5 };
        stack.Children.Add(eyebrow);
        stack.Children.Add(title);
        stack.Children.Add(meta);
        return Panel(stack, new Thickness(15));
    }

    private static Border Panel(Control content, Thickness padding)
    {
        var panel = new Border { Padding = padding, Child = content };
        panel.Classes.Add("dashboardPanel");
        return panel;
    }

    private static Border Divider()
    {
        var divider = new Border();
        divider.Classes.Add("divider");
        return divider;
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

    private static void AddAction(Panel panel, Button button)
    {
        button.Margin = new Thickness(0, 0, 6, 4);
        panel.Children.Add(button);
    }

    private static TextBlock Heading(string text, double size) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock MutedText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            LineHeight = 15,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        block.Classes.Add("muted");
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

    private static Border BuildComparisonMetricRow(ReportMetricDelta metric)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,*,*,1.15*"),
            ColumnSpacing = 12
        };
        grid.Children.Add(Heading(metric.Label, 10.5));
        AddMetricValue(grid, metric.Baseline, 1, "secondary");
        AddMetricValue(grid, metric.Candidate, 2, "secondary");
        AddMetricValue(
            grid,
            metric.Change,
            3,
            metric.Change.Contains("improved", StringComparison.OrdinalIgnoreCase)
                ? "deltaPositive"
                : metric.Change.Contains("worsened", StringComparison.OrdinalIgnoreCase)
                    ? "deltaNegative"
                    : "deltaNeutral");
        var row = new Border { Padding = new Thickness(14, 10), Child = grid };
        row.Classes.Add("divider");
        return row;
    }

    private static void AddMetricValue(Grid grid, string text, int column, string className)
    {
        var value = new TextBlock
        {
            Text = text,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        value.Classes.Add(className);
        Grid.SetColumn(value, column);
        grid.Children.Add(value);
    }

    private void AddEmptyComparisonRow(string text)
    {
        if (comparisonMetricRows is null) return;
        comparisonMetricRows.Children.Add(new Border
        {
            Padding = new Thickness(14, 18),
            Child = MutedText(text)
        });
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private bool recentDiagnosticsProductFlowInstalled;

    public void ApplyRecentDiagnosticsProductFlow()
    {
        if (!EnsureControlCenterSections())
        {
            // RenderControlCenter may already have queued the legacy section render for
            // this attachment. Run after that callback so the product flow owns the
            // final visible tree rather than being overwritten one frame later.
            Dispatcher.UIThread.Post(
                ApplyRecentDiagnosticsProductFlow,
                DispatcherPriority.Background);
            return;
        }

        if (!recentDiagnosticsProductFlowInstalled)
        {
            InstallRecentDiagnosticsProductFlow();
        }

        RenderProductRecentDiagnostics();
        RenderInlineComparison();
        ApplyProductComparisonState();
    }

    public void OpenProductComparison()
    {
        compareModeExpanded = true;
        ApplyRecentDiagnosticsProductFlow();
        Dispatcher.UIThread.Post(
            () => inlineComparisonSection?.BringIntoView(),
            DispatcherPriority.Loaded);
    }

    private void InstallRecentDiagnosticsProductFlow()
    {
        if (recentDiagnosticsSection is null || inlineComparisonSection is null) return;

        recentRowsPanel = new StackPanel();
        recentCountText = MutedText(string.Empty);
        compareModeButton = ActionButton("Compare", "secondary", ProductCompareModeClicked);

        var recentHeading = SectionTitle(
            "RECENT DIAGNOSTICS",
            "Saved network conditions",
            "Open a recent result or compare two runs without leaving the live dashboard.");
        recentHeading.Children.Add(recentCountText);

        var recentActions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddHeaderAction(recentActions, ActionButton(
            "Library",
            "ghost",
            (_, _) => OpenHistoryRequested?.Invoke(this, EventArgs.Empty)));
        AddHeaderAction(recentActions, ActionButton(
            "Import",
            "ghost",
            (_, _) => ImportHistoryRequested?.Invoke(this, EventArgs.Empty)));
        AddHeaderAction(recentActions, compareModeButton);

        var recentHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        recentHeader.Children.Add(recentHeading);
        Grid.SetColumn(recentActions, 1);
        recentHeader.Children.Add(recentActions);

        var recentContent = new StackPanel { Spacing = 13 };
        recentContent.Children.Add(recentHeader);
        recentContent.Children.Add(Divider());
        recentContent.Children.Add(recentRowsPanel);
        recentDiagnosticsSection.Child = recentContent;

        baselineTitleText = Heading("Not selected", 14);
        baselineMetaText = MutedText("Pick the run that represents the reference condition.");
        candidateTitleText = Heading("Not selected", 14);
        candidateMetaText = MutedText("Pick the run you want to compare with the reference.");
        comparisonInstructionText = MutedText("Pick a reference run above, then pick a second run.");
        comparisonSummaryText = Heading("Choose two diagnostics to compare.", 16);
        comparisonTrendText = MutedText(string.Empty);
        comparisonMetricRows = new StackPanel();

        var selectionGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        selectionGrid.Children.Add(SelectionCard("REFERENCE", baselineTitleText, baselineMetaText));
        var candidateCard = SelectionCard("COMPARE TO", candidateTitleText, candidateMetaText);
        Grid.SetColumn(candidateCard, 1);
        selectionGrid.Children.Add(candidateCard);

        var comparisonHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        comparisonHeader.Children.Add(SectionTitle(
            "COMPARISON",
            "Compare two diagnostics",
            comparisonInstructionText.Text ?? string.Empty,
            comparisonInstructionText));
        var reset = ActionButton("Start over", "ghost", ProductClearComparisonClicked);
        Grid.SetColumn(reset, 1);
        comparisonHeader.Children.Add(reset);

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
        AddMetricHeader(metricHeader, "REFERENCE", 1);
        AddMetricHeader(metricHeader, "COMPARE TO", 2);
        AddMetricHeader(metricHeader, "CHANGE", 3);

        var metricStack = new StackPanel();
        metricStack.Children.Add(metricHeader);
        metricStack.Children.Add(comparisonMetricRows);
        var metricTable = Panel(metricStack, new Thickness(0));
        metricTable.ClipToBounds = true;

        var trend = Panel(comparisonTrendText, new Thickness(14, 11));
        var comparisonContent = new StackPanel { Spacing = 13 };
        comparisonContent.Children.Add(comparisonHeader);
        comparisonContent.Children.Add(selectionGrid);
        comparisonContent.Children.Add(summary);
        comparisonContent.Children.Add(metricTable);
        comparisonContent.Children.Add(trend);
        inlineComparisonSection.Child = comparisonContent;
        inlineComparisonSection.IsVisible = false;

        recentDiagnosticsProductFlowInstalled = true;
    }

    private void RenderProductRecentDiagnostics()
    {
        if (recentRowsPanel is null || recentCountText is null || compareModeButton is null) return;

        var reports = controlCenterModel.Reports
            .OrderByDescending(item => item.Report.GeneratedAt)
            .Take(5)
            .ToArray();

        recentCountText.Text = controlCenterModel.Reports.Count switch
        {
            0 => "No saved diagnostics yet",
            <= 5 => $"{controlCenterModel.Reports.Count} saved locally",
            _ => $"Showing latest {reports.Length} of {controlCenterModel.Reports.Count}"
        };
        compareModeButton.Content = compareModeExpanded ? "Exit compare" : "Compare";
        compareModeButton.IsEnabled = reports.Length >= 2 || compareModeExpanded;

        recentRowsPanel.Children.Clear();
        if (reports.Length == 0)
        {
            recentRowsPanel.Children.Add(Panel(
                MutedText("Completed diagnostics appear here automatically. Run a diagnostic or import an existing schema 2.0 report."),
                new Thickness(16)));
            return;
        }

        foreach (var stored in reports)
        {
            recentRowsPanel.Children.Add(BuildProductRecentRow(stored));
        }
    }

    private Control BuildProductRecentRow(StoredReport stored)
    {
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);
        var reportId = stored.Report.Run.Id;
        var isReference = controlCenterModel.BaselineId == reportId;
        var isCandidate = controlCenterModel.CandidateId == reportId;

        var title = Heading(stored.Label ?? presentation.Verdict, 11.5);
        var metadata = MutedText($"{stored.ProfileName} · {stored.Report.GeneratedAt.ToLocalTime():MMM d, h:mm tt}");
        var context = MutedText(ReportComparisonService.ContextLabel(stored.Report));
        context.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;

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

        if (!compareModeExpanded)
        {
            AddAction(actions, TaggedButton("Open", "ghost", stored, OpenRecentClicked));
            AddAction(actions, TaggedButton("Label", "linkAction", stored, EditRecentClicked));
        }
        else
        {
            var selection = TaggedButton(
                ComparisonSelectionLabel(isReference, isCandidate),
                ComparisonSelectionClass(isReference, isCandidate),
                stored,
                ProductComparisonSelectionClicked);
            selection.IsEnabled = !isReference && !isCandidate;
            AddAction(actions, selection);
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
        row.Classes.Add(isReference || isCandidate ? "accentSurface" : "divider");
        return row;
    }

    private string ComparisonSelectionLabel(bool isReference, bool isCandidate)
    {
        if (isReference) return "Reference";
        if (isCandidate) return "Compare to";
        if (controlCenterModel.BaselineId is null) return "Use as reference";
        return controlCenterModel.CandidateId is null ? "Compare with this" : "Use instead";
    }

    private string ComparisonSelectionClass(bool isReference, bool isCandidate)
    {
        if (isReference || isCandidate) return "secondary";
        return controlCenterModel.BaselineId is null ? "secondary" : "primary";
    }

    private void ProductCompareModeClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (!compareModeExpanded)
        {
            compareModeExpanded = true;
            ApplyRecentDiagnosticsProductFlow();
            return;
        }

        compareModeExpanded = false;
        controlCenterModel = controlCenterModel with { BaselineId = null, CandidateId = null };
        ApplyRecentDiagnosticsProductFlow();
        InlineComparisonClearRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProductComparisonSelectionClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        var reportId = stored.Report.Run.Id;

        if (controlCenterModel.BaselineId is null)
        {
            controlCenterModel = controlCenterModel with
            {
                BaselineId = reportId,
                CandidateId = null
            };
            ApplyRecentDiagnosticsProductFlow();
            InlineBaselineRequested?.Invoke(this, new StoredReportEventArgs(stored));
            return;
        }

        if (controlCenterModel.BaselineId == reportId) return;
        controlCenterModel = controlCenterModel with { CandidateId = reportId };
        ApplyRecentDiagnosticsProductFlow();
        InlineCandidateRequested?.Invoke(this, new StoredReportEventArgs(stored));
    }

    private void ProductClearComparisonClicked(object? sender, RoutedEventArgs eventArgs)
    {
        controlCenterModel = controlCenterModel with { BaselineId = null, CandidateId = null };
        ApplyRecentDiagnosticsProductFlow();
        InlineComparisonClearRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyProductComparisonState()
    {
        if (inlineComparisonSection is null
            || comparisonInstructionText is null
            || comparisonTrendText is null)
        {
            return;
        }

        inlineComparisonSection.IsVisible = compareModeExpanded;
        var reference = FindReport(controlCenterModel.BaselineId);
        var candidate = FindReport(controlCenterModel.CandidateId);

        comparisonInstructionText.Text = reference is null
            ? "Pick a reference run from Recent diagnostics."
            : candidate is null
                ? "Reference selected. Pick the run you want to compare against it."
                : "Comparison ready. Choose another run above to replace the second selection.";
        comparisonTrendText.IsVisible = reference is not null && candidate is not null;
    }

    private static void AddHeaderAction(Panel panel, Button button)
    {
        button.Margin = new Thickness(0, 0, 6, 4);
        panel.Children.Add(button);
    }
}

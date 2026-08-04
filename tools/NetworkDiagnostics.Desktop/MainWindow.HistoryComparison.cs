using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDeepProbe.Models;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private Guid? comparisonBaselineId;
    private Guid? comparisonCandidateId;
    private NetworkDiagnosticsReportV2? comparisonCandidateReport;

    private async Task RefreshComparisonHistoryAsync()
    {
        var reports = await reportStore.ListAsync();
        var baseline = FindStoredReport(reports, comparisonBaselineId);
        var candidate = FindStoredReport(reports, comparisonCandidateId);

        if (comparisonBaselineId is not null && baseline is null) comparisonBaselineId = null;
        if (comparisonCandidateId is not null && candidate is null) comparisonCandidateId = null;
        if (baseline is null && candidate is not null)
        {
            comparisonCandidateId = null;
            candidate = null;
        }

        comparisonBaselineReport = baseline?.Report;
        comparisonCandidateReport = candidate?.Report;

        HistoryListPanel.Children.Clear();
        HistoryCountText.Text = reports.Count == 1 ? "1 SAVED REPORT" : $"{reports.Count} SAVED REPORTS";
        ReportsFolderText.Text = reportStore.ReportsDirectory;
        SetHistoryPanelEyebrow("COMPARE REPORTS");

        if (reports.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No saved reports yet. Complete or import a diagnostic to begin a comparison.",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            HistoryListPanel.Children.Add(empty);
            HistoryFixtureTitle.Text = "No reports to compare";
            HistoryFixtureDetail.Text = "Completed diagnostics and imported schema 2.0 reports will appear here.";
            return;
        }

        if (baseline is not null)
        {
            var clearButton = CreateHistoryActionButton(
                "Clear comparison",
                baseline,
                primary: false,
                selected: false,
                enabled: true);
            clearButton.Margin = new Thickness(0, 0, 0, 8);
            clearButton.Click += ClearComparisonClicked;
            HistoryListPanel.Children.Add(clearButton);
        }

        foreach (var stored in reports.Take(12))
        {
            HistoryListPanel.Children.Add(BuildComparisonHistoryCard(stored));
        }

        var trend = ReportComparisonService.AnalyzeTrend(reports);
        RenderComparisonSummary(baseline, candidate, trend);
    }

    private Border BuildComparisonHistoryCard(StoredReport stored)
    {
        var isBaseline = comparisonBaselineId == stored.Report.Run.Id;
        var isCandidate = comparisonCandidateId == stored.Report.Run.Id;
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);

        var marker = isBaseline ? " · BASELINE" : isCandidate ? " · COMPARED" : string.Empty;
        var profile = new TextBlock
        {
            Text = $"{stored.ProfileName.ToUpperInvariant()}{marker}",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.5
        };
        profile.Classes.Add("eyebrow");

        var title = new TextBlock
        {
            Text = stored.Label ?? presentation.Verdict,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        var verdict = new TextBlock
        {
            Text = stored.Label is null
                ? stored.DisplayDate
                : $"{presentation.Verdict} · {stored.DisplayDate}",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        verdict.Classes.Add("muted");

        var contextParts = new List<string>();
        var contextLabel = ReportComparisonService.ContextLabel(stored.Report);
        if (!string.IsNullOrWhiteSpace(contextLabel)) contextParts.Add(contextLabel);
        if (stored.Tags.Count > 0) contextParts.Add($"Tags: {string.Join(", ", stored.Tags)}");
        var context = new TextBlock
        {
            Text = string.Join(Environment.NewLine, contextParts),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        context.Classes.Add("muted");

        var reportContent = new StackPanel { Spacing = 4 };
        reportContent.Children.Add(profile);
        reportContent.Children.Add(title);
        reportContent.Children.Add(verdict);
        if (contextParts.Count > 0) reportContent.Children.Add(context);

        var openButton = new Button
        {
            Content = reportContent,
            Tag = stored,
            Background = Brushes.Transparent,
            Foreground = Brush.Parse("#E9E6E0"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        openButton.Click += ComparisonHistoryReportClicked;

        var baselineButton = CreateHistoryActionButton(
            isBaseline ? "Baseline selected" : "Set baseline",
            stored,
            primary: false,
            selected: isBaseline,
            enabled: !isBaseline);
        baselineButton.Click += SetComparisonBaselineClicked;

        var compareEnabled = comparisonBaselineId is not null && !isBaseline;
        var compareButton = CreateHistoryActionButton(
            isCandidate ? "Compared report" : compareEnabled ? "Compare to baseline" : "Select baseline first",
            stored,
            primary: compareEnabled && !isCandidate,
            selected: isCandidate,
            enabled: compareEnabled && !isCandidate);
        compareButton.Click += CompareToBaselineClicked;

        var annotationButton = CreateHistoryActionButton(
            stored.Label is null && stored.Tags.Count == 0 ? "Add label" : "Edit label",
            stored,
            primary: false,
            selected: false,
            enabled: true);
        annotationButton.Click += ComparisonEditReportAnnotationsClicked;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        actions.Children.Add(baselineButton);
        actions.Children.Add(compareButton);
        actions.Children.Add(annotationButton);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(openButton);
        content.Children.Add(actions);

        return new Border
        {
            Background = Brush.Parse(isBaseline || isCandidate ? "#1E1D19" : "#171B1C"),
            BorderBrush = Brush.Parse(isBaseline || isCandidate ? "#C96346" : "#34383A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = content
        };
    }

    private static Button CreateHistoryActionButton(
        string content,
        StoredReport stored,
        bool primary,
        bool selected,
        bool enabled)
    {
        var button = new Button
        {
            Content = content,
            Tag = stored,
            FontSize = 12,
            Padding = new Thickness(11, 7),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = enabled,
            Opacity = enabled || selected ? 1 : 0.52
        };
        button.Classes.Add(primary ? "primary" : "secondary");
        if (selected)
        {
            button.Background = Brush.Parse("#2A2118");
            button.Foreground = Brush.Parse("#F2EFE9");
            button.BorderBrush = Brush.Parse("#C96346");
            button.BorderThickness = new Thickness(1);
        }
        return button;
    }

    private void SetComparisonBaselineClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        NavigateToDestination(new ComparisonDestination(stored.Report.Run.Id));
    }

    private void CompareToBaselineClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }
            || comparisonBaselineId is null
            || comparisonBaselineId == stored.Report.Run.Id)
        {
            return;
        }

        NavigateToDestination(new ComparisonDestination(comparisonBaselineId, stored.Report.Run.Id));
    }

    private void ClearComparisonClicked(object? sender, RoutedEventArgs eventArgs) =>
        NavigateToDestination(new ComparisonDestination());

    private void ComparisonHistoryReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        selectedHistoryReport = stored;
        currentReport = stored.Report;
        activeProfile = stored.Report.Run.Profile;
        currentPresentation = DiagnosticReportPresenter.FromReport(stored.Report);
        RenderPresentation(currentPresentation);
        NavigateToDestination(new ReportDetailDestination(stored.Report.Run.Id));
    }

    private async void ComparisonEditReportAnnotationsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        var input = await new ReportAnnotationDialog(stored).ShowDialog<ReportAnnotationInput?>(this);
        if (input is null) return;

        try
        {
            var updated = await reportStore.UpdateAnnotationsAsync(stored, input.Label, input.Tags);
            selectedHistoryReport = updated;
            if (currentReport?.Run.Id == updated.Report.Run.Id) currentReport = updated.Report;
            if (comparisonBaselineId == updated.Report.Run.Id) comparisonBaselineReport = updated.Report;
            if (comparisonCandidateId == updated.Report.Run.Id) comparisonCandidateReport = updated.Report;
            await RefreshComparisonHistoryAsync();
            RefreshWorkbenchChrome();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            HistoryFixtureTitle.Text = "Report annotations were not saved";
            HistoryFixtureDetail.Text = error.Message;
        }
    }

    private void RenderComparisonSummary(
        StoredReport? baseline,
        StoredReport? candidate,
        ReportTrendResult trend)
    {
        if (baseline is null)
        {
            HistoryFixtureTitle.Text = "Choose a comparison baseline";
            HistoryFixtureDetail.Text =
                "Use Set baseline under one saved report. Then choose Compare to baseline under a second report.\n\n"
                + $"LOCAL TREND\n{trend.Summary}";
            return;
        }

        if (candidate is null)
        {
            HistoryFixtureTitle.Text = "Choose the second report";
            HistoryFixtureDetail.Text =
                $"BASELINE\n{HistorySelectionName(baseline)}\n\n"
                + "Choose Compare to baseline under another report.\n\n"
                + $"LOCAL TREND\n{trend.Summary}";
            return;
        }

        var comparison = ReportComparisonService.Compare(baseline.Report, candidate.Report);
        var warningText = comparison.Warnings.Count == 0
            ? "Equivalent test conditions"
            : $"Comparison cautions: {string.Join(" ", comparison.Warnings)}";
        var metricText = string.Join(
            Environment.NewLine,
            comparison.Metrics.Take(10).Select(item =>
                $"{item.Label}: {item.Baseline} → {item.Candidate} · {item.Change}"));

        HistoryFixtureTitle.Text = comparison.Comparable
            ? "Report comparison"
            : "Report comparison with cautions";
        HistoryFixtureDetail.Text =
            $"BASELINE\n{HistorySelectionName(baseline)}\n\n"
            + $"COMPARED REPORT\n{HistorySelectionName(candidate)}\n\n"
            + $"{warningText}\n{comparison.Summary}\n\n{metricText}\n\n"
            + $"LOCAL TREND\n{trend.Summary}";
    }

    private void SetHistoryPanelEyebrow(string text)
    {
        if (HistoryFixtureTitle.Parent is StackPanel titlePanel
            && titlePanel.Parent is StackPanel summaryPanel
            && summaryPanel.Children.FirstOrDefault() is TextBlock eyebrow)
        {
            eyebrow.Text = text;
        }
    }

    private static StoredReport? FindStoredReport(
        IReadOnlyList<StoredReport> reports,
        Guid? reportId) => reportId is null
            ? null
            : reports.FirstOrDefault(item => item.Report.Run.Id == reportId.Value);

    private static string HistorySelectionName(StoredReport stored) =>
        $"{stored.Label ?? stored.ProfileName} · {stored.DisplayDate}\n{ReportComparisonService.ContextLabel(stored.Report)}";
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void PreviewFixtureClicked(object? sender, RoutedEventArgs eventArgs)
    {
        currentReport = null;
        comparisonBaselineReport = null;
        selectedHistoryReport = null;
        activeProfile = TestProfileId.ConnectionCheck;
        currentPresentation = ConnectionCheckFixtures.Get(FixtureSelector.SelectedIndex);
        RenderPresentation(currentPresentation);
        ShowArea(Models.DesktopArea.Test);
        ShowTestState(Models.TestViewState.Results);
    }

    private async void ImportReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Network Diagnostics report",
            AllowMultiple = false,
            FileTypeFilter = [JsonReportFileType]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var stored = await reportStore.ImportAsync(path);
            selectedHistoryReport = stored;
            currentReport = stored.Report;
            comparisonBaselineReport = stored.Report;
            activeProfile = currentReport.Run.Profile;
            currentPresentation = DiagnosticReportPresenter.FromReport(currentReport);
            RenderPresentation(currentPresentation);
            await RefreshHistoryAsync();
            ShowArea(Models.DesktopArea.Test);
            ShowTestState(Models.TestViewState.Results);
        }
        catch (Exception error)
        {
            currentPresentation = DiagnosticReportPresenter.FromFailure(activeProfile, error);
            RenderPresentation(currentPresentation);
            ShowArea(Models.DesktopArea.Test);
            ShowTestState(Models.TestViewState.Results);
        }
    }

    private async void ExportReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (currentReport is null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Network Diagnostics report",
            SuggestedFileName = SuggestedExportName(currentReport),
            DefaultExtension = "json",
            FileTypeChoices = [JsonReportFileType]
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        await reportStore.ExportAsync(currentReport, path);
    }

    private void OpenReportsFolderClicked(object? sender, RoutedEventArgs eventArgs) => reportStore.OpenReportsFolder();

    private async void IncludeIdentifiersChanged(object? sender, RoutedEventArgs eventArgs)
    {
        if (!initialized) return;
        settings = settings with { IncludeLocalIdentifiers = IncludeIdentifiersCheckBox.IsChecked == true };
        await PersistSettingsAsync();
        await RefreshPreflightAsync();
    }

    private async void SaveAdvancedSettingsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        var originValues = DesktopSettings.ParseOriginLines(TestOriginTextBox.Text);
        if (originValues.Count > 8)
        {
            SettingsStatusText.Text = "Configure no more than eight measurement endpoint candidates.";
            return;
        }
        foreach (var value in originValues)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                SettingsStatusText.Text = $"Endpoint '{value}' must be an absolute HTTP or HTTPS URL.";
                return;
            }
        }
        if (!TryParseLanSettings(out var lanPort, out var lanDuration, out var lanConnections)) return;

        settings = settings with
        {
            TestOrigin = null,
            TestOrigins = originValues.Count == 0 ? null : originValues,
            LanTarget = string.IsNullOrWhiteSpace(LanTargetTextBox.Text) ? null : LanTargetTextBox.Text.Trim(),
            LanPort = lanPort,
            LanDurationSeconds = lanDuration,
            LanConnections = lanConnections
        };
        await PersistSettingsAsync();
        SettingsStatusText.Text = originValues.Count == 0
            ? "Using the default first-party endpoint. LAN settings saved."
            : $"Saved {originValues.Count} endpoint candidate{(originValues.Count == 1 ? string.Empty : "s")} and LAN settings.";
        await RefreshPreflightAsync();
    }

    private bool TryParseLanSettings(out int port, out int duration, out int connections)
    {
        if (!int.TryParse(LanPortTextBox.Text, out port) || port is < 1024 or > 65535)
        {
            duration = 0;
            connections = 0;
            SettingsStatusText.Text = "LAN port must be between 1024 and 65535.";
            return false;
        }
        if (!int.TryParse(LanDurationTextBox.Text, out duration) || duration is < 3 or > 30)
        {
            connections = 0;
            SettingsStatusText.Text = "LAN duration must be between 3 and 30 seconds.";
            return false;
        }
        if (!int.TryParse(LanConnectionsTextBox.Text, out connections) || connections is < 1 or > 16)
        {
            SettingsStatusText.Text = "LAN connections must be between 1 and 16.";
            return false;
        }
        return true;
    }

    private async void ResetApprovalsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        settings = settings.ResetDataApprovals();
        await PersistSettingsAsync();
        SettingsStatusText.Text = "Full and Stress data-use approvals were reset.";
    }

    private async Task RefreshHistoryAsync()
    {
        var reports = await reportStore.ListAsync();
        comparisonBaselineReport ??= currentReport;
        HistoryListPanel.Children.Clear();
        HistoryCountText.Text = reports.Count == 1 ? "1 saved report" : $"{reports.Count} saved reports";
        ReportsFolderText.Text = reportStore.ReportsDirectory;

        if (reports.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No saved reports yet. Completed diagnostics and imported schema 2.0 reports will appear here.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            empty.Classes.Add("muted");
            HistoryListPanel.Children.Add(empty);
            HistoryFixtureTitle.Text = "No local trend yet";
            HistoryFixtureDetail.Text = "Save two equivalent reports to compare changes over time.";
            return;
        }

        foreach (var stored in reports.Take(12))
        {
            var presentation = DiagnosticReportPresenter.FromReport(stored.Report);
            var profile = new TextBlock
            {
                Text = stored.ProfileName.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                LetterSpacing = 1.5
            };
            profile.Classes.Add("eyebrow");
            var title = new TextBlock
            {
                Text = stored.Label ?? presentation.Verdict,
                FontSize = 15,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var verdict = new TextBlock
            {
                Text = stored.Label is null ? stored.DisplayDate : $"{presentation.Verdict} · {stored.DisplayDate}",
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            verdict.Classes.Add("muted");
            var contextParts = new List<string> { ReportComparisonService.ContextLabel(stored.Report) };
            if (stored.Tags.Count > 0) contextParts.Add($"Tags: {string.Join(", ", stored.Tags)}");
            var context = new TextBlock
            {
                Text = string.Join(Environment.NewLine, contextParts),
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            context.Classes.Add("muted");
            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(profile);
            content.Children.Add(title);
            content.Children.Add(verdict);
            content.Children.Add(context);
            var openButton = new Button
            {
                Content = content,
                Tag = stored,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };
            openButton.Classes.Add("historyItem");
            openButton.Click += SavedReportClicked;
            var compareButton = new Button
            {
                Content = comparisonBaselineReport is null ? "Use as comparison baseline" : "Compare with baseline",
                Tag = stored,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            compareButton.Classes.Add("compact");
            compareButton.Click += CompareReportClicked;
            var annotationButton = new Button
            {
                Content = stored.Label is null && stored.Tags.Count == 0 ? "Add label" : "Edit label",
                Tag = stored,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            annotationButton.Classes.Add("compact");
            annotationButton.Click += EditReportAnnotationsClicked;
            var actions = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };
            actions.Children.Add(compareButton);
            actions.Children.Add(annotationButton);
            var item = new StackPanel { Spacing = 6 };
            item.Children.Add(openButton);
            item.Children.Add(actions);
            HistoryListPanel.Children.Add(item);
        }

        var trend = ReportComparisonService.AnalyzeTrend(reports);
        HistoryFixtureTitle.Text = trend.CompatibleRuns >= 2 ? "Compatible-run trend" : "Trend needs another equivalent run";
        HistoryFixtureDetail.Text = trend.Summary;
    }

    private void SavedReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        selectedHistoryReport = stored;
        currentReport = stored.Report;
        comparisonBaselineReport = stored.Report;
        activeProfile = stored.Report.Run.Profile;
        currentPresentation = DiagnosticReportPresenter.FromReport(stored.Report);
        RenderPresentation(currentPresentation);
        ShowArea(Models.DesktopArea.Test);
        ShowTestState(Models.TestViewState.Results);
    }

    private void CompareReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        selectedHistoryReport = stored;
        if (comparisonBaselineReport is null || comparisonBaselineReport.Run.Id == stored.Report.Run.Id)
        {
            comparisonBaselineReport = stored.Report;
            HistoryFixtureTitle.Text = "Comparison baseline selected";
            HistoryFixtureDetail.Text = $"{stored.Label ?? stored.ProfileName} · {stored.DisplayDate}\n{ReportComparisonService.ContextLabel(stored.Report)}";
            return;
        }

        var comparison = ReportComparisonService.Compare(comparisonBaselineReport, stored.Report);
        var warningText = comparison.Warnings.Count == 0
            ? "Equivalent test conditions"
            : $"Comparison cautions: {string.Join(" ", comparison.Warnings)}";
        var metricText = string.Join(
            Environment.NewLine,
            comparison.Metrics.Take(8).Select(item => $"{item.Label}: {item.Baseline} → {item.Candidate} · {item.Change}"));
        HistoryFixtureTitle.Text = comparison.Comparable ? "Report comparison" : "Report comparison with cautions";
        HistoryFixtureDetail.Text = $"{warningText}\n{comparison.Summary}\n{metricText}";
    }

    private async void EditReportAnnotationsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        var input = await new ReportAnnotationDialog(stored).ShowDialog<ReportAnnotationInput?>(this);
        if (input is null) return;

        try
        {
            var updated = await reportStore.UpdateAnnotationsAsync(stored, input.Label, input.Tags);
            selectedHistoryReport = updated;
            if (currentReport?.Run.Id == updated.Report.Run.Id) currentReport = updated.Report;
            if (comparisonBaselineReport?.Run.Id == updated.Report.Run.Id) comparisonBaselineReport = updated.Report;
            await RefreshHistoryAsync();
            HistoryFixtureTitle.Text = string.IsNullOrWhiteSpace(updated.Label) ? "Report annotations updated" : updated.Label;
            HistoryFixtureDetail.Text = updated.Tags.Count == 0
                ? "The label was saved inside the local report."
                : $"Tags: {string.Join(", ", updated.Tags)}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            HistoryFixtureTitle.Text = "Report annotations were not saved";
            HistoryFixtureDetail.Text = error.Message;
        }
    }

    private async Task PersistSettingsAsync()
    {
        try
        {
            await settingsStore.SaveAsync(settings);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SettingsStatusText.Text = $"Settings could not be saved: {error.Message}";
        }
    }

    private static string SuggestedExportName(NetworkDiagnosticsReportV2 report)
    {
        var profile = DesktopSettings.ContractId(report.Run.Profile).Replace("standard", "full").Replace("extended", "stress");
        return $"network-diagnostics-{report.GeneratedAt:yyyyMMdd-HHmmss}-{profile}.json";
    }
}

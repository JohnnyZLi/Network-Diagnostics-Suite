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
            currentReport = stored.Report;
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
    }

    private async void SaveAdvancedSettingsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        var value = TestOriginTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(value)
            && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
        {
            SettingsStatusText.Text = "Endpoint override must be an absolute HTTP or HTTPS URL.";
            return;
        }
        settings = settings with { TestOrigin = string.IsNullOrWhiteSpace(value) ? null : value };
        await PersistSettingsAsync();
        SettingsStatusText.Text = string.IsNullOrWhiteSpace(value)
            ? "Using the default first-party endpoint."
            : "Endpoint override saved.";
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
            return;
        }

        foreach (var stored in reports.Take(12))
        {
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
                Text = DiagnosticReportPresenter.FromReport(stored.Report).Verdict,
                FontSize = 15,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var date = new TextBlock { Text = stored.DisplayDate, FontSize = 12 };
            date.Classes.Add("muted");
            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(profile);
            content.Children.Add(title);
            content.Children.Add(date);
            var button = new Button
            {
                Content = content,
                Tag = stored,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };
            button.Classes.Add("historyItem");
            button.Click += SavedReportClicked;
            HistoryListPanel.Children.Add(button);
        }

        var latest = reports[0];
        var presentation = DiagnosticReportPresenter.FromReport(latest.Report);
        HistoryFixtureTitle.Text = presentation.Verdict;
        HistoryFixtureDetail.Text = $"{latest.ProfileName} · {latest.DisplayDate}. {presentation.Summary}";
    }

    private void SavedReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        currentReport = stored.Report;
        activeProfile = stored.Report.Run.Profile;
        currentPresentation = DiagnosticReportPresenter.FromReport(stored.Report);
        RenderPresentation(currentPresentation);
        ShowArea(Models.DesktopArea.Test);
        ShowTestState(Models.TestViewState.Results);
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

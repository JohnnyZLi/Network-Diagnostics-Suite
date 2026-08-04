using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;
using NetworkDiagnostics.Desktop.Workspaces;

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
        NavigateToDestination(new TestResultDestination(Guid.Empty));
    }

    private async void ImportReportClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ImportReportAsync();

    private async Task ImportReportAsync()
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
            NavigateToDestination(new ReportDetailDestination(stored.Report.Run.Id));
        }
        catch (Exception error)
        {
            currentPresentation = DiagnosticReportPresenter.FromFailure(activeProfile, error);
            RenderPresentation(currentPresentation);
            NavigateToDestination(new TestResultDestination(Guid.Empty));
        }
    }

    private async void ExportReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (currentReport is not null)
        {
            await ExportReportAsync(currentReport);
        }
    }

    private async Task ExportReportAsync(NetworkDiagnosticsReportV2 report)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Network Diagnostics report",
            SuggestedFileName = SuggestedExportName(report),
            DefaultExtension = "json",
            FileTypeChoices = [JsonReportFileType]
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        await reportStore.ExportAsync(report, path);
    }

    private void OpenReportsFolderClicked(object? sender, RoutedEventArgs eventArgs) => reportStore.OpenReportsFolder();

    private async void IncludeIdentifiersChanged(object? sender, RoutedEventArgs eventArgs) =>
        await SaveIdentifiersSettingAsync(IncludeIdentifiersCheckBox.IsChecked == true);

    private async Task SaveIdentifiersSettingAsync(bool includeIdentifiers)
    {
        if (!initialized) return;
        settings = settings with { IncludeLocalIdentifiers = includeIdentifiers };
        IncludeIdentifiersCheckBox.IsChecked = includeIdentifiers;
        await PersistSettingsAsync();
        await RefreshPreflightAsync();
        RefreshWorkbenchChrome();
    }

    private async void SaveAdvancedSettingsClicked(object? sender, RoutedEventArgs eventArgs) =>
        await SaveAdvancedSettingsAsync();

    private async Task SaveAdvancedSettingsAsync()
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
        RefreshWorkbenchChrome();
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

    private async void ResetApprovalsClicked(object? sender, RoutedEventArgs eventArgs) =>
        await ResetApprovalsAsync();

    private async Task ResetApprovalsAsync()
    {
        settings = settings.ResetDataApprovals();
        await PersistSettingsAsync();
        SettingsStatusText.Text = "Full and Stress data-use approvals were reset.";
    }

    private async Task RefreshHistoryAsync(NavigationViewState? viewState = null)
    {
        var reports = await reportStore.ListAsync();
        comparisonBaselineReport ??= currentReport;
        HistoryCountText.Text = reports.Count == 1 ? "1 saved report" : $"{reports.Count} saved reports";

        ReportBrowserState? browserState = viewState is null
            ? null
            : new ReportBrowserState(
                viewState.SearchQuery ?? string.Empty,
                viewState.SortKey ?? "date-desc",
                viewState.SortDescending,
                viewState.SelectedReportId);
        reportBrowserWorkspace?.Render(reports, browserState);
    }

    private void SavedReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            OpenStoredReport(stored);
        }
    }

    private void OpenStoredReport(StoredReport stored)
    {
        selectedHistoryReport = stored;
        currentReport = stored.Report;
        comparisonBaselineReport = stored.Report;
        activeProfile = stored.Report.Run.Profile;
        currentPresentation = DiagnosticReportPresenter.FromReport(stored.Report);
        RenderPresentation(currentPresentation);
        NavigateToDestination(new ReportDetailDestination(stored.Report.Run.Id));
    }

    private void CompareReportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            CompareStoredReport(stored);
        }
    }

    private void CompareStoredReport(StoredReport stored)
    {
        selectedHistoryReport = stored;
        comparisonBaselineReport = stored.Report;
        NavigateToDestination(new ComparisonDestination(stored.Report.Run.Id));
    }

    private async void EditReportAnnotationsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: StoredReport stored })
        {
            await EditReportAnnotationsAsync(stored);
        }
    }

    private async Task EditReportAnnotationsAsync(StoredReport stored)
    {
        var input = await new ReportAnnotationDialog(stored).ShowDialog<ReportAnnotationInput?>(this);
        if (input is null) return;

        try
        {
            var updated = await reportStore.UpdateAnnotationsAsync(stored, input.Label, input.Tags);
            selectedHistoryReport = updated;
            if (currentReport?.Run.Id == updated.Report.Run.Id) currentReport = updated.Report;
            if (comparisonBaselineReport?.Run.Id == updated.Report.Run.Id) comparisonBaselineReport = updated.Report;
            if (comparisonCandidateReport?.Run.Id == updated.Report.Run.Id) comparisonCandidateReport = updated.Report;
            currentPresentation = currentReport?.Run.Id == updated.Report.Run.Id
                ? DiagnosticReportPresenter.FromReport(updated.Report)
                : currentPresentation;

            switch (navigationService.Current?.Destination)
            {
                case ReportListDestination:
                    await RefreshHistoryAsync(navigationService.Current.ViewState with
                    {
                        SelectedReportId = updated.Report.Run.Id
                    });
                    break;
                case ReportDetailDestination:
                    reportDetailWorkspace?.Render(updated, DiagnosticReportPresenter.FromReport(updated.Report));
                    break;
                case ComparisonDestination:
                    await RefreshComparisonHistoryAsync();
                    break;
            }
            RefreshWorkbenchChrome();
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

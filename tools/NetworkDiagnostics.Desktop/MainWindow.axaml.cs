using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType JsonReportFileType = new("Network Diagnostics report")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    private readonly DiagnosticRunService diagnosticRunService = new();
    private readonly DesktopSettingsStore settingsStore = new();
    private readonly ReportStore reportStore;
    private CancellationTokenSource? runCancellation;
    private bool initialized;
    private TestViewState currentTestState = TestViewState.Setup;
    private ConnectionCheckPresentation currentPresentation = ConnectionCheckFixtures.All[0];
    private DesktopSettings settings = new();
    private NetworkDiagnosticsReportV2? currentReport;
    private StoredReport? currentStoredReport;
    private TestProfileId activeProfile = TestProfileId.ConnectionCheck;
    private double displayedRunProgress;

    public MainWindow()
    {
        reportStore = new ReportStore(settingsStore.RootDirectory);
        InitializeComponent();
        ProfileSelector.SelectedIndex = 0;
        FixtureSelector.SelectedIndex = 0;
        initialized = true;
        RenderProfileSelection();
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
        Opened += WindowOpened;
    }

    private async void WindowOpened(object? sender, EventArgs eventArgs)
    {
        initialized = false;
        settings = await settingsStore.LoadAsync();
        reportStore.Configure(settings.ReportDirectory);
        ProfileSelector.SelectedIndex = ProfileIndex(settings.SelectedProfile);
        IncludeIdentifiersCheckBox.IsChecked = settings.IncludeLocalIdentifiers;
        TestOriginTextBox.Text = settings.TestOrigin ?? string.Empty;
        ReportsFolderText.Text = reportStore.ReportsDirectory;
        initialized = true;
        RenderProfileSelection();
        await RefreshHistoryAsync();
    }

    private void TestNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Test);

    private async void HistoryNavClicked(object? sender, RoutedEventArgs eventArgs)
    {
        await RefreshHistoryAsync();
        ShowArea(DesktopArea.History);
    }

    private void SettingsNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Settings);

    private async void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!initialized) return;
        RenderProfileSelection();
        settings = settings with { DefaultProfile = DesktopSettings.ContractId(SelectedProfile()) };
        await PersistSettingsAsync();
    }

    private void ConnectionProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(0);

    private void QuickProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(1);

    private void FullProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(2);

    private void StressProfileClicked(object? sender, RoutedEventArgs eventArgs) => SelectProfile(3);

    private void SelectProfile(int index)
    {
        if (ProfileSelector.SelectedIndex == index)
        {
            RenderProfileSelection();
            return;
        }
        ProfileSelector.SelectedIndex = index;
    }

    private async void RunClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (runCancellation is not null) return;
        activeProfile = SelectedProfile();
        if (!await ConfirmDataUseAsync(activeProfile)) return;

        var cancellation = new CancellationTokenSource();
        runCancellation = cancellation;
        currentReport = null;
        currentStoredReport = null;
        ResetRunningState();
        RunningProfileText.Text = DiagnosticReportPresenter.ProfileName(activeProfile).ToUpperInvariant();
        ShowTestState(TestViewState.Running);
        var progress = new Progress<NativeRunProgress>(RenderRunProgress);

        try
        {
            var report = await diagnosticRunService.RunAsync(
                activeProfile,
                settings.IncludeLocalIdentifiers,
                settings.ParsedTestOrigin,
                progress,
                cancellation.Token);
            currentReport = report;
            currentStoredReport = await reportStore.SaveAsync(report, cancellation.Token);
            currentPresentation = DiagnosticReportPresenter.FromReport(report);
            CompleteRunningState();
            RenderPresentation(currentPresentation);
            await RefreshHistoryAsync();
            ShowTestState(TestViewState.Results);
        }
        catch (OperationCanceledException)
        {
            currentPresentation = DiagnosticReportPresenter.FromCancellation(activeProfile);
            RenderPresentation(currentPresentation);
            ShowTestState(TestViewState.Results);
        }
        catch (Exception error)
        {
            currentPresentation = DiagnosticReportPresenter.FromFailure(activeProfile, error);
            RenderPresentation(currentPresentation);
            ShowTestState(TestViewState.Results);
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(runCancellation, cancellation)) runCancellation = null;
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) => runCancellation?.Cancel();

    private void RunAgainClicked(object? sender, RoutedEventArgs eventArgs)
    {
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
    }

    private void ChooseQuickClicked(object? sender, RoutedEventArgs eventArgs)
    {
        ProfileSelector.SelectedIndex = 1;
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
    }

    private void PreviewFixtureClicked(object? sender, RoutedEventArgs eventArgs)
    {
        currentReport = null;
        currentStoredReport = null;
        activeProfile = TestProfileId.ConnectionCheck;
        currentPresentation = ConnectionCheckFixtures.Get(FixtureSelector.SelectedIndex);
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Results);
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
            currentStoredReport = await reportStore.ImportAsync(path);
            currentReport = currentStoredReport.Report;
            activeProfile = currentReport.Run.Profile;
            currentPresentation = DiagnosticReportPresenter.FromReport(currentReport);
            RenderPresentation(currentPresentation);
            await RefreshHistoryAsync();
            ShowArea(DesktopArea.Test);
            ShowTestState(TestViewState.Results);
        }
        catch (Exception error)
        {
            currentPresentation = DiagnosticReportPresenter.FromFailure(activeProfile, error);
            RenderPresentation(currentPresentation);
            ShowArea(DesktopArea.Test);
            ShowTestState(TestViewState.Results);
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

    private void ShowArea(DesktopArea area)
    {
        TestArea.IsVisible = area == DesktopArea.Test;
        HistoryArea.IsVisible = area == DesktopArea.History;
        SettingsArea.IsVisible = area == DesktopArea.Settings;

        SetActiveState(TestNavButton, area == DesktopArea.Test);
        SetActiveState(HistoryNavButton, area == DesktopArea.History);
        SetActiveState(SettingsNavButton, area == DesktopArea.Settings);

        if (area == DesktopArea.Test) ShowTestState(currentTestState);
    }

    private void ShowTestState(TestViewState state)
    {
        currentTestState = state;
        SetupView.IsVisible = state == TestViewState.Setup;
        RunningView.IsVisible = state == TestViewState.Running;
        ResultsView.IsVisible = state == TestViewState.Results;
    }

    private void RenderProfileSelection()
    {
        var selectedIndex = ProfileSelector.SelectedIndex;
        var profile = selectedIndex switch
        {
            1 => new ProfileCopy(
                "What performance am I getting now?",
                "A broader speed and responsiveness snapshot using single and aggregate transfer evidence.",
                "About 20 seconds",
                "Up to 728 MB",
                "Not required",
                "Quick runs real throughput and loaded-responsiveness measurements without the deeper route and service probes.",
                "Run Quick"),
            2 => new ProfileCopy(
                "Where is the likely problem?",
                "Adds local-network, route, resolver, service, Wi-Fi, and responsiveness evidence.",
                "About 35 seconds",
                "Up to 1.156 GB",
                "Required",
                "Full runs the native deep-probe stack after its Internet transfer measurements and saves the complete schema 2.0 report.",
                "Run Full"),
            3 => new ProfileCopy(
                "How does the connection behave under sustained load?",
                "Runs sustained capacity, connection scaling, loaded responsiveness, and the native deep probe.",
                "About 60 seconds",
                "Up to 3.512 GB",
                "Required",
                "Stress uses the largest transfer ceiling. Cancellation remains available throughout the run.",
                "Run Stress"),
            _ => new ProfileCopy(
                "Is the connection working normally?",
                "A lightweight first-party reachability, latency, request-loss, download, and upload check with a clear verdict.",
                "About 15 seconds",
                "Up to 28 MB",
                "Not required",
                "Connection Check now runs the real native engine and saves its report locally when complete.",
                "Run Connection Check")
        };

        SetActiveState(ConnectionProfileButton, selectedIndex == 0);
        SetActiveState(QuickProfileButton, selectedIndex == 1);
        SetActiveState(FullProfileButton, selectedIndex == 2);
        SetActiveState(StressProfileButton, selectedIndex == 3);

        ProfileQuestionText.Text = profile.Question;
        ProfilePurposeText.Text = profile.Purpose;
        EstimatedTimeText.Text = profile.EstimatedTime;
        TransferCapText.Text = profile.TransferCap;
        ConfirmationText.Text = profile.Confirmation;
        ProfileAvailabilityText.Text = profile.Availability;
        RunButton.Content = profile.ButtonText;
        RunButton.IsEnabled = runCancellation is null;
    }

    private void RenderPresentation(ConnectionCheckPresentation presentation)
    {
        var profileName = currentReport is null
            ? DiagnosticReportPresenter.ProfileName(activeProfile)
            : DiagnosticReportPresenter.ProfileName(currentReport.Run.Profile);
        ResultProfileText.Text = $"{profileName.ToUpperInvariant()} / RESULT";
        VerdictLabelText.Text = presentation.Label.ToUpperInvariant();
        VerdictText.Text = presentation.Verdict;
        VerdictSummaryText.Text = presentation.Summary;
        NextActionText.Text = presentation.NextAction;
        ChooseQuickButton.IsVisible = activeProfile == TestProfileId.ConnectionCheck
            && presentation.Outcome == ConnectionCheckOutcome.Healthy;
        ExportReportButton.IsVisible = currentReport is not null;

        RenderMetric(Metric1Label, Metric1Value, Metric1Detail, presentation.Metrics[0]);
        RenderMetric(Metric2Label, Metric2Value, Metric2Detail, presentation.Metrics[1]);
        RenderMetric(Metric3Label, Metric3Value, Metric3Detail, presentation.Metrics[2]);
        RenderMetric(Metric4Label, Metric4Value, Metric4Detail, presentation.Metrics[3]);

        FindingsPanel.Children.Clear();
        foreach (var finding in presentation.Findings)
        {
            var label = new TextBlock { Text = finding.Label.ToUpperInvariant() };
            label.Classes.Add("eyebrow");
            var title = new TextBlock
            {
                Text = finding.Title,
                FontSize = 17,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var summary = new TextBlock
            {
                Text = finding.Summary,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            summary.Classes.Add("muted");

            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(label);
            content.Children.Add(title);
            content.Children.Add(summary);

            var section = new Border { Child = content };
            section.Classes.Add("finding");
            FindingsPanel.Children.Add(section);
        }

        TechnicalEvidencePanel.Children.Clear();
        foreach (var evidence in presentation.TechnicalEvidence)
        {
            var line = new TextBlock
            {
                Text = $"• {evidence}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            line.Classes.Add("muted");
            TechnicalEvidencePanel.Children.Add(line);
        }

        HistoryFixtureTitle.Text = presentation.Verdict;
        HistoryFixtureDetail.Text = $"{profileName} · {presentation.Label}. {presentation.Summary}";
    }

    private void RenderRunProgress(NativeRunProgress progress)
    {
        CurrentPhaseText.Text = progress.Message;
        LiveMeasurementText.Text = LiveProgressText(progress);
        displayedRunProgress = Math.Max(displayedRunProgress, OverallProgress(progress));
        RunProgress.Value = displayedRunProgress;

        var deepProfile = activeProfile is TestProfileId.Standard or TestProfileId.Extended;
        if (progress.Phase == "diagnostics")
        {
            if (displayedRunProgress < 20)
            {
                NetworkPhaseStatus.Text = "In progress";
            }
            else if (deepProfile)
            {
                DeepPhaseStatus.Text = "In progress";
                displayedRunProgress = Math.Min(96, displayedRunProgress + 2.5);
                RunProgress.Value = displayedRunProgress;
            }
            return;
        }

        switch (progress.Phase)
        {
            case "idle":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "In progress";
                break;
            case "download":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "Complete";
                DownloadPhaseStatus.Text = "In progress";
                break;
            case "upload":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "Complete";
                DownloadPhaseStatus.Text = "Complete";
                UploadPhaseStatus.Text = "In progress";
                break;
            case "complete":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "Complete";
                DownloadPhaseStatus.Text = "Complete";
                UploadPhaseStatus.Text = "Complete";
                DeepPhaseStatus.Text = deepProfile ? "Waiting" : "Not included";
                break;
        }
    }

    private static void RenderMetric(
        TextBlock label,
        TextBlock value,
        TextBlock detail,
        MetricPresentation metric)
    {
        label.Text = metric.Label.ToUpperInvariant();
        value.Text = metric.Value;
        detail.Text = metric.Detail;
        value.Opacity = metric.WasMeasured ? 1 : 0.72;
    }

    private void ResetRunningState()
    {
        displayedRunProgress = 0;
        RunProgress.Value = 0;
        CurrentPhaseText.Text = "Preparing the test…";
        LiveMeasurementText.Text = "Starting…";
        NetworkPhaseStatus.Text = "Waiting";
        LatencyPhaseStatus.Text = "Waiting";
        DownloadPhaseStatus.Text = "Waiting";
        UploadPhaseStatus.Text = "Waiting";
        DeepPhaseStatus.Text = activeProfile is TestProfileId.Standard or TestProfileId.Extended
            ? "Waiting"
            : "Not included";
    }

    private void CompleteRunningState()
    {
        displayedRunProgress = 100;
        RunProgress.Value = 100;
        NetworkPhaseStatus.Text = "Complete";
        LatencyPhaseStatus.Text = "Complete";
        DownloadPhaseStatus.Text = "Complete";
        UploadPhaseStatus.Text = "Complete";
        DeepPhaseStatus.Text = activeProfile is TestProfileId.Standard or TestProfileId.Extended
            ? "Complete"
            : "Not included";
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
        currentStoredReport = stored;
        currentReport = stored.Report;
        activeProfile = stored.Report.Run.Profile;
        currentPresentation = DiagnosticReportPresenter.FromReport(stored.Report);
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Results);
    }

    private async Task<bool> ConfirmDataUseAsync(TestProfileId profile)
    {
        var plan = NetworkDiagnosticsRunner.DescribePlan(profile, TransferMethod.Compare);
        if (profile is not (TestProfileId.Standard or TestProfileId.Extended)
            || settings.HasDataApproval(profile, plan.TransferCapBytes))
        {
            return true;
        }

        var confirmation = await new DataUseDialog(plan).ShowDialog<DataUseConfirmation>(this);
        if (!confirmation.Confirmed) return false;
        if (confirmation.Remember)
        {
            settings = settings.WithDataApproval(profile, plan.TransferCapBytes);
            await PersistSettingsAsync();
        }
        return true;
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

    private TestProfileId SelectedProfile() => ProfileSelector.SelectedIndex switch
    {
        1 => TestProfileId.Quick,
        2 => TestProfileId.Standard,
        3 => TestProfileId.Extended,
        _ => TestProfileId.ConnectionCheck
    };

    private static int ProfileIndex(TestProfileId profile) => profile switch
    {
        TestProfileId.Quick => 1,
        TestProfileId.Standard => 2,
        TestProfileId.Extended => 3,
        _ => 0
    };

    private double OverallProgress(NativeRunProgress progress)
    {
        var deepProfile = activeProfile is TestProfileId.Standard or TestProfileId.Extended;
        return progress.Phase switch
        {
            "idle" => 8 + progress.Fraction * 17,
            "download" => 25 + progress.Fraction * 27,
            "upload" => 52 + progress.Fraction * 20,
            "complete" => deepProfile ? 74 : 100,
            "diagnostics" => displayedRunProgress < 8 ? 5 : displayedRunProgress,
            _ => displayedRunProgress
        };
    }

    private static string LiveProgressText(NativeRunProgress progress)
    {
        var values = new List<string>();
        if (progress.LiveMbps is { } mbps)
        {
            values.Add($"{mbps.ToString(mbps >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} Mbps");
        }
        if (progress.LiveLatencyMs is { } latency)
        {
            values.Add($"{latency.ToString(latency >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms live latency");
        }
        if (progress.BytesTransferred > 0)
        {
            values.Add($"{(progress.BytesTransferred / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture)} MB measured");
        }
        return values.Count == 0 ? progress.Message : string.Join(" · ", values);
    }

    private static string SuggestedExportName(NetworkDiagnosticsReportV2 report)
    {
        var profile = DesktopSettings.ContractId(report.Run.Profile).Replace("standard", "full").Replace("extended", "stress");
        return $"network-diagnostics-{report.GeneratedAt:yyyyMMdd-HHmmss}-{profile}.json";
    }

    private static void SetActiveState(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("active")) button.Classes.Add("active");
        }
        else
        {
            button.Classes.Remove("active");
        }
    }

    private sealed record ProfileCopy(
        string Question,
        string Purpose,
        string EstimatedTime,
        string TransferCap,
        string Confirmation,
        string Availability,
        string ButtonText);
}

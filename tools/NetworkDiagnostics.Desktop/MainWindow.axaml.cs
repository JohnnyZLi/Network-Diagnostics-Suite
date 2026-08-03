using Avalonia.Controls;
using Avalonia.Interactivity;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow : Window
{
    private CancellationTokenSource? previewCancellation;
    private bool initialized;
    private TestViewState currentTestState = TestViewState.Setup;
    private ConnectionCheckPresentation currentPresentation = ConnectionCheckFixtures.All[0];

    public MainWindow()
    {
        InitializeComponent();
        ProfileSelector.SelectedIndex = 0;
        FixtureSelector.SelectedIndex = 0;
        initialized = true;
        RenderProfileSelection();
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
    }

    private void TestNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Test);

    private void HistoryNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.History);

    private void SettingsNavClicked(object? sender, RoutedEventArgs eventArgs) => ShowArea(DesktopArea.Settings);

    private void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (initialized) RenderProfileSelection();
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
        if (ProfileSelector.SelectedIndex != 0 || previewCancellation is not null) return;

        var cancellation = new CancellationTokenSource();
        previewCancellation = cancellation;
        ResetRunningState();
        ShowTestState(TestViewState.Running);

        var phases = new[]
        {
            new PreviewPhase("Checking network access", "Contacting the first-party endpoint…", 18),
            new PreviewPhase("Measuring latency and packet loss", "8 ICMP samples in progress…", 42),
            new PreviewPhase("Sampling download", "Aggregate transfer sample…", 70),
            new PreviewPhase("Sampling upload", "Aggregate transfer sample…", 92)
        };

        try
        {
            for (var index = 0; index < phases.Length; index++)
            {
                var phase = phases[index];
                CurrentPhaseText.Text = phase.Title;
                LiveMeasurementText.Text = phase.Detail;
                RunProgress.Value = phase.Progress;
                UpdatePhaseStatuses(index);
                await Task.Delay(650, cancellation.Token);
            }

            UpdatePhaseStatuses(phases.Length);
            RunProgress.Value = 100;
            currentPresentation = ConnectionCheckFixtures.Get(FixtureSelector.SelectedIndex);
            RenderPresentation(currentPresentation);
            await Task.Delay(250, cancellation.Token);
            ShowTestState(TestViewState.Results);
        }
        catch (OperationCanceledException)
        {
            ShowTestState(TestViewState.Setup);
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(previewCancellation, cancellation)) previewCancellation = null;
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) => previewCancellation?.Cancel();

    private void RunAgainClicked(object? sender, RoutedEventArgs eventArgs)
    {
        ProfileSelector.SelectedIndex = 0;
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
        currentPresentation = ConnectionCheckFixtures.Get(FixtureSelector.SelectedIndex);
        RenderPresentation(currentPresentation);
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Results);
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
                "A broader speed and responsiveness snapshot using the original Quick measurement plan.",
                "About 20 seconds",
                "Up to 728 MB",
                "Not required",
                "Quick is the next vertical slice. Its approved measurement plan is already preserved in the native contract.",
                "Quick is not connected yet",
                false),
            2 => new ProfileCopy(
                "Where is the likely problem?",
                "Adds deeper local-network, path, resolver, service, and responsiveness evidence.",
                "About 35 seconds",
                "Up to 1.156 GB",
                "Required",
                "Full will be connected after Connection Check and Quick have complete screen workflows.",
                "Full is not connected yet",
                false),
            3 => new ProfileCopy(
                "How does the connection behave under sustained load?",
                "Runs the approved stress and scaling sequence for capacity and loaded-responsiveness analysis.",
                "About 60 seconds",
                "Up to 3.512 GB",
                "Required",
                "Stress remains available in the native contract but will be the final profile workflow implemented.",
                "Stress is not connected yet",
                false),
            _ => new ProfileCopy(
                "Is the connection working normally?",
                "A lightweight reachability, latency, packet-loss, download, and upload check with a clear verdict.",
                "About 15 seconds",
                "Up to 28 MB",
                "Not required",
                "This first slice uses approved static fixtures so every screen state can be reviewed before the real engine is connected.",
                "Run Connection Check",
                true)
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
        RunButton.IsEnabled = profile.Enabled;
    }

    private void RenderPresentation(ConnectionCheckPresentation presentation)
    {
        VerdictLabelText.Text = presentation.Label.ToUpperInvariant();
        VerdictText.Text = presentation.Verdict;
        VerdictSummaryText.Text = presentation.Summary;
        NextActionText.Text = presentation.NextAction;
        ChooseQuickButton.IsVisible = presentation.Outcome is ConnectionCheckOutcome.Healthy
            or ConnectionCheckOutcome.Problematic
            or ConnectionCheckOutcome.Inconclusive;

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

            var card = new Border { Child = content };
            card.Classes.Add("finding");
            FindingsPanel.Children.Add(card);
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
        HistoryFixtureDetail.Text = $"Connection Check · {presentation.Label}. {presentation.Summary}";
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
        RunProgress.Value = 0;
        CurrentPhaseText.Text = "Preparing the test…";
        LiveMeasurementText.Text = "Starting…";
        UpdatePhaseStatuses(-1);
    }

    private void UpdatePhaseStatuses(int activeIndex)
    {
        var labels = new[]
        {
            NetworkPhaseStatus,
            LatencyPhaseStatus,
            DownloadPhaseStatus,
            UploadPhaseStatus
        };

        for (var index = 0; index < labels.Length; index++)
        {
            labels[index].Text = index < activeIndex
                ? "Complete"
                : index == activeIndex
                    ? "In progress"
                    : "Waiting";
        }
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

    private enum DesktopArea
    {
        Test,
        History,
        Settings
    }

    private enum TestViewState
    {
        Setup,
        Running,
        Results
    }

    private sealed record ProfileCopy(
        string Question,
        string Purpose,
        string EstimatedTime,
        string TransferCap,
        string Confirmation,
        string Availability,
        string ButtonText,
        bool Enabled);

    private sealed record PreviewPhase(string Title, string Detail, double Progress);
}
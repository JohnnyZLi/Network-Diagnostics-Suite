using Avalonia.Controls;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
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

    private sealed record ProfileCopy(
        string Question,
        string Purpose,
        string EstimatedTime,
        string TransferCap,
        string Confirmation,
        string Availability,
        string ButtonText);
}

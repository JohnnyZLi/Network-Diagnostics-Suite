using System.Globalization;
using Avalonia.Controls;
using NetworkDeepProbe.Models;
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
        if (currentReport is not null) RenderDetailedEvidence(currentReport);

        HistoryFixtureTitle.Text = presentation.Verdict;
        HistoryFixtureDetail.Text = $"{profileName} · {presentation.Label}. {presentation.Summary}";
    }

    private void RenderDetailedEvidence(NetworkDiagnosticsReportV2 report)
    {
        if (report.InternetTransfer is { } internet && report.Run.Profile != TestProfileId.ConnectionCheck)
        {
            var flowRows = internet.FlowMeasurements.Select(measurement => new EvidenceRow(
                measurement.Strategy == TransferStrategy.Single
                    ? "Single connection"
                    : $"Aggregate · {measurement.Connections} connections",
                measurement.Download is null ? "Download not measured" : $"{Rate(measurement.Download.SteadyMbps)} down",
                measurement.Upload is null
                    ? (measurement.DownloadLatency is null ? null : $"Loaded delay +{Milliseconds(measurement.DownloadLatency.IncreaseMs)}")
                    : $"{Rate(measurement.Upload.SteadyMbps)} up · loaded delay +{Milliseconds(measurement.UploadLatency?.IncreaseMs)}"));
            AddEvidenceSection("FLOW COMPARISON", flowRows);
        }

        if (report.InternetTransfer is { DownloadScaling.Count: > 0 } stress)
        {
            AddEvidenceSection(
                "CONNECTION SCALING",
                stress.DownloadScaling.Select(point => new EvidenceRow(
                    $"{point.Connections} connection{(point.Connections == 1 ? string.Empty : "s")}",
                    Rate(point.Download.SteadyMbps),
                    $"Whole phase {Rate(point.Download.Mbps)} · loaded delay +{Milliseconds(point.DownloadLatency.IncreaseMs)} · {point.Download.Qualification}")));
        }

        if (report.DeepDiagnostics is { } deep)
        {
            var fastestDns = deep.DnsResolvers
                .Where(item => item.MedianMs is not null)
                .OrderBy(item => item.MedianMs)
                .FirstOrDefault();
            var reachableServices = deep.ServiceEndpoints.Count(item => item.Reachable);
            var localRows = new List<EvidenceRow>
            {
                new("Gateway latency", Milliseconds(deep.GatewayPing?.Statistics.MedianMs), deep.GatewayPing is null ? "Not measured" : $"{deep.GatewayPing.Statistics.LossPercent:0.0}% loss"),
                new("Public target latency", Milliseconds(deep.InternetPing.Statistics.MedianMs), $"{deep.InternetPing.Statistics.JitterMs:0.0} ms jitter · {deep.InternetPing.Statistics.LossPercent:0.0}% loss"),
                new("Traceroute", $"{deep.TraceRoute.Hops.Count} hops", deep.TraceRoute.ReachedDestination ? "Destination reached" : "Partial path"),
                new("Path MTU", deep.PathMtu.EstimatedIpv4Mtu?.ToString(CultureInfo.InvariantCulture) ?? "Not measured", deep.PathMtu.Status),
                new("Fastest DNS", fastestDns is null ? "Not measured" : Milliseconds(fastestDns.MedianMs), fastestDns?.Name),
                new("Service endpoints", $"{reachableServices} of {deep.ServiceEndpoints.Count} reachable", deep.ServiceEndpoints.Count == 0 ? "Not measured" : null),
                new("Wi-Fi", deep.Wifi?.SignalPercent is int signal ? $"{signal}% signal" : "Not measured", deep.Wifi?.Status)
            };
            AddEvidenceSection("LOCAL NETWORK + PATH", localRows);
        }
        else if (report.Run.Profile is TestProfileId.Standard or TestProfileId.Extended)
        {
            AddEvidenceSection(
                "LOCAL NETWORK + PATH",
                [new EvidenceRow("Deep diagnostics", "Not measured", "The section was unavailable or failed; completed Internet evidence remains valid.")]);
        }

        if (report.BrowserEvidence is { } browser)
        {
            var browserRows = new List<EvidenceRow>();
            if (browser.Edge is { } edge)
            {
                browserRows.Add(new EvidenceRow(
                    "Website edge",
                    edge.Edge ?? "Unknown",
                    string.Join(" · ", new[]
                    {
                        edge.Network,
                        edge.Asn is null ? null : $"AS{edge.Asn}",
                        edge.Protocol,
                        edge.TlsVersion,
                        edge.IpVersion
                    }.Where(value => !string.IsNullOrWhiteSpace(value)))));
            }
            browserRows.AddRange(browser.ServiceChecks.Select(service => new EvidenceRow(
                service.Name,
                service.Reachable ? "Reachable" : "Not reachable",
                service.DurationMs is null
                    ? service.Note
                    : $"{service.DurationMs.Value:0.0} ms{(string.IsNullOrWhiteSpace(service.Note) ? string.Empty : $" · {service.Note}")}")));
            if (browserRows.Count > 0) AddEvidenceSection("BROWSER EDGE + SERVICES", browserRows);
        }
    }

    private void AddEvidenceSection(string title, IEnumerable<EvidenceRow> rows)
    {
        var content = new StackPanel { Spacing = 8 };
        var heading = new TextBlock { Text = title };
        heading.Classes.Add("eyebrow");
        content.Children.Add(heading);

        foreach (var row in rows)
        {
            var label = new TextBlock
            {
                Text = row.Label,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            label.Classes.Add("muted");
            var value = new TextBlock
            {
                Text = row.Value,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                TextAlignment = Avalonia.Media.TextAlignment.Right,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(label);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            content.Children.Add(grid);
            if (!string.IsNullOrWhiteSpace(row.Detail))
            {
                var detail = new TextBlock
                {
                    Text = row.Detail,
                    FontSize = 12,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };
                detail.Classes.Add("muted");
                content.Children.Add(detail);
            }
        }

        var section = new Border { Margin = new Avalonia.Thickness(0, 10, 0, 0), Child = content };
        section.Classes.Add("panelInner");
        TechnicalEvidencePanel.Children.Add(section);
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

    private static string Rate(double value) => $"{value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} Mbps";

    private static string Milliseconds(double? value) => value is null
        ? "Not measured"
        : $"{value.Value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms";

    private sealed record EvidenceRow(string Label, string Value, string? Detail = null);

    private sealed record ProfileCopy(
        string Question,
        string Purpose,
        string EstimatedTime,
        string TransferCap,
        string Confirmation,
        string Availability,
        string ButtonText);
}

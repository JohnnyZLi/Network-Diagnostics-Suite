using System.Globalization;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop.Presentation;

public static class DiagnosticReportPresenter
{
    public static ConnectionCheckPresentation FromReport(NetworkDiagnosticsReportV2 report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var findings = report.Findings ?? DiagnosticClassifier.Classify(report);
        var actionable = findings
            .Where(item => item.Severity is "warning" or "critical")
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ToArray();
        var hasMeasurements = report.InternetTransfer is not null || report.DeepDiagnostics is not null;
        var hasSectionFailure = findings.Any(item => item.Id == "deep-diagnostics-failed");
        var outcome = !hasMeasurements
            ? ConnectionCheckOutcome.Unavailable
            : hasSectionFailure
                ? ConnectionCheckOutcome.Inconclusive
                : actionable.Length == 0
                    ? ConnectionCheckOutcome.Healthy
                    : actionable.All(item => item.Confidence == "low")
                        ? ConnectionCheckOutcome.Inconclusive
                        : ConnectionCheckOutcome.Problematic;
        var profileName = ProfileName(report.Run.Profile);
        var primary = hasSectionFailure
            ? findings.First(item => item.Id == "deep-diagnostics-failed")
            : actionable.FirstOrDefault() ?? findings.FirstOrDefault();

        var label = outcome switch
        {
            ConnectionCheckOutcome.Healthy => "Connection looks normal",
            ConnectionCheckOutcome.Problematic => actionable.Any(item => item.Severity == "critical")
                ? "Significant problem detected"
                : "Problem detected",
            ConnectionCheckOutcome.Inconclusive => hasSectionFailure ? "Completed with partial data" : "Result is inconclusive",
            ConnectionCheckOutcome.Unavailable => "Partially measured",
            _ => "Test did not complete"
        };
        var verdict = outcome switch
        {
            ConnectionCheckOutcome.Healthy => $"{profileName} completed without an obvious problem.",
            ConnectionCheckOutcome.Problematic => $"{profileName} found a problem worth investigating.",
            ConnectionCheckOutcome.Inconclusive when hasSectionFailure => $"{profileName} preserved its completed Internet measurements.",
            ConnectionCheckOutcome.Inconclusive => $"{profileName} found a weak or inconsistent signal.",
            ConnectionCheckOutcome.Unavailable => $"{profileName} did not contain supported performance measurements.",
            _ => $"{profileName} could not finish."
        };
        var summary = primary?.Summary ?? "The report completed, but no supported finding summary was available.";
        var nextAction = primary?.NextTest
            ?? primary?.Recommendations.FirstOrDefault()
            ?? (outcome == ConnectionCheckOutcome.Healthy
                ? NextHealthyAction(report.Run.Profile)
                : "Repeat the same profile once and compare the saved reports.");

        var presentedFindings = (actionable.Length > 0 && !hasSectionFailure ? actionable : findings.Take(3).ToArray())
            .Select(item => new FindingPresentation(
                item.Category.Replace('-', ' '),
                item.Title,
                item.Summary))
            .ToArray();
        if (presentedFindings.Length == 0)
        {
            presentedFindings =
            [
                new FindingPresentation(
                    "Measurement",
                    "No supported finding was available",
                    "The report remains available in Technical data for manual inspection.")
            ];
        }

        return new ConnectionCheckPresentation(
            outcome,
            label,
            verdict,
            summary,
            nextAction,
            Metrics(report),
            presentedFindings,
            TechnicalEvidence(report, findings));
    }

    public static ConnectionCheckPresentation FromFailure(TestProfileId profile, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ConnectionCheckPresentation(
            ConnectionCheckOutcome.Failed,
            "Test did not complete",
            $"{ProfileName(profile)} could not finish.",
            "The diagnostic engine stopped before it could produce a complete report.",
            "Check the connection, VPN, captive portal, or endpoint setting, then run the same profile again.",
            [
                new("Status", "Failed", "No complete report was produced"),
                new("Latency", "Not measured", "The test stopped before completion", false),
                new("Download", "Not measured", "The test stopped before completion", false),
                new("Upload", "Not measured", "The test stopped before completion", false)
            ],
            [
                new("Failure", "The diagnostic engine returned an error", SafeMessage(error))
            ],
            [
                $"Profile: {DesktopSettingsId(profile)}",
                $"Error type: {error.GetType().Name}",
                $"Message: {SafeMessage(error)}",
                "No failure is reported for measurements that did not start"
            ]);
    }

    public static ConnectionCheckPresentation FromCancellation(TestProfileId profile) => new(
        ConnectionCheckOutcome.Inconclusive,
        "Test cancelled",
        $"{ProfileName(profile)} was stopped.",
        "The run ended before a final verdict was available. Completed progress is not presented as a finished report.",
        "Run the same profile again when the connection can remain active for the full measurement window.",
        [
            new("Status", "Cancelled", "Stopped by the user"),
            new("Latency", "Not final", "Partial live values were not saved", false),
            new("Download", "Not final", "Partial live values were not saved", false),
            new("Upload", "Not final", "Partial live values were not saved", false)
        ],
        [
            new("Run state", "No final report was saved", "Cancellation is distinct from a failed or unhealthy connection.")
        ],
        [
            $"Profile: {DesktopSettingsId(profile)}",
            "State: cancelled",
            "Final classifier did not run"
        ]);

    public static string ProfileName(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "Connection Check",
        TestProfileId.Quick => "Quick",
        TestProfileId.Standard => "Full",
        TestProfileId.Extended => "Stress",
        _ => "Diagnostic"
    };

    private static IReadOnlyList<MetricPresentation> Metrics(NetworkDiagnosticsReportV2 report)
    {
        var internet = report.InternetTransfer;
        var deep = report.DeepDiagnostics;
        var measurement = report.Measurement;
        var single = internet?.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Single);
        var aggregate = internet?.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Aggregate);
        var fastestDns = deep?.DnsResolvers
            .Where(item => item.MedianMs is not null)
            .OrderBy(item => item.MedianMs)
            .FirstOrDefault();
        var reachableServices = deep?.ServiceEndpoints.Count(item => item.Reachable);
        var totalServices = deep?.ServiceEndpoints.Count;
        var traceHops = deep?.TraceRoute.Hops.Count;
        var preflight = measurement?.SelectedEndpoint.PreflightLatencyMs;
        var retransmission = report.HostResources?.TcpRetransmissionPercent;

        return
        [
            new("Latency", Milliseconds(internet?.IdleLatency.MedianMs), "Median first-party HTTP latency", internet?.IdleLatency.MedianMs is not null),
            new("Jitter", Milliseconds(internet?.IdleLatency.JitterMs), "Idle HTTP latency variation", internet?.IdleLatency.JitterMs is not null),
            new("Request loss", internet is null ? "Not measured" : Percent(internet.IdleLatency.LossPercent), internet is null ? "No Internet transfer section" : $"{internet.IdleLatency.Received} of {internet.IdleLatency.Sent} responses", internet is not null),
            new("Download", internet is null ? "Not measured" : Megabits(internet.Download.SteadyMbps), internet?.Download.Qualification ?? "No transfer measurement", internet is not null),
            new("Download peak", internet is null ? "Not measured" : Megabits(internet.Download.PeakMbps), "Highest observed download interval", internet is not null),
            new("Download stability", internet is null ? "Not measured" : Percent(internet.Download.StabilityPercent), "Steady-rate consistency", internet is not null),
            new("Loaded download", Milliseconds(internet?.DownloadLatency.IncreaseMs), "Latency increase while downloading", internet?.DownloadLatency.IncreaseMs is not null),
            new("Upload", internet is null ? "Not measured" : Megabits(internet.Upload.SteadyMbps), internet?.Upload.Qualification ?? "No transfer measurement", internet is not null),
            new("Loaded upload", Milliseconds(internet?.UploadLatency.IncreaseMs), "Latency increase while uploading", internet?.UploadLatency.IncreaseMs is not null),
            new("Data used", internet is null ? "Not measured" : FormatBytes(internet.DataUsedBytes), "Measured transfer payload", internet is not null),
            new("Single-flow download", single?.Download is null ? "Not measured" : Megabits(single.Download.SteadyMbps), single?.Download is null ? "Selected topology did not measure a single flow" : "One connection", single?.Download is not null),
            new("Aggregate download", aggregate?.Download is null ? "Not measured" : Megabits(aggregate.Download.SteadyMbps), aggregate?.Download is null ? "Selected topology did not measure parallel flows" : $"{aggregate.Connections} parallel connections", aggregate?.Download is not null),
            new("Endpoint preflight", Milliseconds(preflight), measurement is null ? "No endpoint preflight data" : $"{measurement.SelectedEndpoint.Name} · {measurement.SelectedEndpoint.Provider}", preflight is not null),
            new("HTTP/3", measurement?.Http3 is null ? "Not measured" : measurement.Http3.Supported ? "Available" : "Unavailable", "Exact-version protocol probe", measurement?.Http3?.Attempted == true),
            new("Gateway", Milliseconds(deep?.GatewayPing?.Statistics.MedianMs), "Default gateway median", deep?.GatewayPing?.Statistics.MedianMs is not null),
            new("Internet ICMP", Milliseconds(deep?.InternetPing.Statistics.MedianMs), "Public target median", deep?.InternetPing.Statistics.MedianMs is not null),
            new("DNS", Milliseconds(fastestDns?.MedianMs), fastestDns is null ? "Not measured" : $"Fastest · {fastestDns.Name}", fastestDns is not null),
            new("Path MTU", deep?.PathMtu.EstimatedIpv4Mtu is int mtu ? $"{mtu} bytes" : "Not measured", deep?.PathMtu.Status ?? "No path MTU measurement", deep?.PathMtu.EstimatedIpv4Mtu is not null),
            new("Service reachability", reachableServices is null || totalServices is null ? "Not measured" : $"{reachableServices}/{totalServices}", "Common TLS endpoints reachable", reachableServices is not null),
            new("Traceroute", traceHops is null ? "Not measured" : $"{traceHops} hops", deep?.TraceRoute.ReachedDestination == true ? "Destination reached" : deep is null ? "No traceroute measurement" : "Destination not reached", traceHops is not null),
            new("Wi-Fi signal", deep?.Wifi?.SignalPercent is int signal ? $"{signal}%" : "Not measured", "Operating-system signal estimate", deep?.Wifi?.SignalPercent is not null),
            new("LAN download", deep?.LocalLink is null ? "Not measured" : Megabits(deep.LocalLink.DownloadMbps), deep?.LocalLink is null ? "No LAN peer configured" : $"{deep.LocalLink.Concurrency} parallel streams", deep?.LocalLink is not null),
            new("LAN upload", deep?.LocalLink is null ? "Not measured" : Megabits(deep.LocalLink.UploadMbps), deep?.LocalLink is null ? "No LAN peer configured" : $"{deep.LocalLink.Concurrency} parallel streams", deep?.LocalLink is not null),
            new("Process CPU", report.HostResources is null ? "Not measured" : Percent(report.HostResources.ProcessCpuPercent), "Diagnostic process CPU share", report.HostResources is not null),
            new("TCP retransmission", retransmission is null ? "Not measured" : Percent(retransmission.Value), retransmission is null ? "Operating-system counter unavailable" : $"{report.HostResources!.TcpSegmentsRetransmitted} of {report.HostResources.TcpSegmentsSent} sent segments", retransmission is not null)
        ];
    }

    private static IReadOnlyList<string> TechnicalEvidence(
        NetworkDiagnosticsReportV2 report,
        IReadOnlyList<DiagnosticFinding> findings)
    {
        var data = new List<string>
        {
            $"Report ID: {report.Run.Id}",
            $"Profile: {DesktopSettingsId(report.Run.Profile)}",
            $"Generated: {report.GeneratedAt:O}",
            $"Producer: {report.Producer?.Application ?? "unknown"} {report.Producer?.Version ?? string.Empty}".TrimEnd(),
            $"Platform: {report.Run.Platform} · {report.Run.Architecture}",
            $"Transfer ceiling: {FormatBytes(report.TransferPlan.TransferCapBytes)}"
        };
        if (report.Annotations is { } annotations)
        {
            if (!string.IsNullOrWhiteSpace(annotations.Label)) data.Add($"Report label: {annotations.Label}");
            if (annotations.Tags.Count > 0) data.Add($"Report tags: {string.Join(", ", annotations.Tags)}");
        }
        if (report.InternetTransfer is { } internet)
        {
            data.Add($"Measured payload: {FormatBytes(internet.DataUsedBytes)}");
            data.Add($"Endpoint origin: {internet.Origin}");
            if (internet.DownloadDelivery is { } delivery)
            {
                data.Add($"Download path: requested {UserDownloadPath(delivery.RequestedPath)} · actual {UserDownloadPath(delivery.SelectedPath)}");
                data.Add($"R2 status: {UserStatus(delivery.R2ProbeStatus)} · R2 requests {delivery.R2Requests} · Worker requests {delivery.WorkerRequests}");
                data.Add($"Download requests: {delivery.RequestsCompleted} of {delivery.RequestsStarted} completed");
                if (!string.IsNullOrWhiteSpace(delivery.FallbackReason)) data.Add($"Download fallback: {delivery.FallbackReason}");
            }
        }
        if (report.Measurement is { } measurement)
        {
            data.Add($"Selected endpoint: {measurement.SelectedEndpoint.Name} · {measurement.SelectedEndpoint.Provider}");
            data.Add($"Endpoint selection: {measurement.SelectedEndpoint.SelectionReason}");
            if (measurement.SelectedEndpoint.PreflightLatencyMs is { } preflight)
            {
                data.Add($"Endpoint preflight median: {preflight.ToString("0.0", CultureInfo.InvariantCulture)} ms");
            }
            data.Add($"Capabilities: {string.Join(", ", measurement.Capabilities.Select(UserCapabilityLabel))}");
        }
        if (report.DualStack is { } dualStack)
        {
            data.Add($"Address-family probe: {dualStack.Status} · preferred {dualStack.PreferredFamily}");
            data.Add($"Dual-stack DNS: {Milliseconds(dualStack.DnsResolutionMs)} · {dualStack.Ipv4AddressCount} IPv4 · {dualStack.Ipv6AddressCount} IPv6 addresses");
            if (dualStack.ParallelConnectWinner is { } winner)
            {
                data.Add($"Parallel family winner: {winner} · difference {Milliseconds(dualStack.ParallelConnectDifferenceMs)}");
            }
            data.Add($"IPv4: {FamilyEvidence(dualStack.Ipv4)}");
            data.Add($"IPv6: {FamilyEvidence(dualStack.Ipv6)}");
            if (dualStack.Nat64Suspected) data.Add("NAT64/DNS64: suspected from the resolved IPv6 prefix");
        }
        if (report.LoadLocalization is { } localization)
        {
            data.Add($"Loaded path localization: {localization.Status} · {localization.LikelyBoundary ?? "no clear boundary"}");
            data.Add($"Loaded path summary: {localization.Summary}");
            foreach (var target in localization.Targets)
            {
                data.Add($"{target.Label}: idle {Milliseconds(target.Idle.MedianMs)} · download {Milliseconds(target.Download.MedianMs)} · upload {Milliseconds(target.Upload.MedianMs)}");
            }
        }
        if (report.NetworkChange is { } networkChange)
        {
            data.Add($"Network state: {(networkChange.Changed ? "changed during run" : "stable during run")}");
            data.Add($"Captive portal: {(networkChange.CaptivePortalSuspected ? "suspected" : "not detected")}");
            if (networkChange.Before.Proxy is { } proxy) data.Add($"System proxy: {proxy}");
            if (networkChange.Before.TunnelInterfaces.Count > 0)
            {
                data.Add($"Tunnel interfaces: {string.Join(", ", networkChange.Before.TunnelInterfaces)}");
            }
            if (networkChange.PublicNetworkBefore is { } before)
            {
                data.Add($"Public path before: {PublicNetworkEvidence(before)}");
            }
            if (networkChange.PublicNetworkAfter is { } after)
            {
                data.Add($"Public path after: {PublicNetworkEvidence(after)}");
            }
            foreach (var change in networkChange.Changes) data.Add($"Network change: {change}");
        }
        if (report.HostResources is { } resources)
        {
            data.Add($"Diagnostic process CPU: {Percent(resources.ProcessCpuPercent)}");
            data.Add($"Peak working set: {FormatBytes(resources.PeakWorkingSetBytes)}");
            if (resources.TcpRetransmissionPercent is { } retransmission)
            {
                data.Add($"TCP retransmissions: {resources.TcpSegmentsRetransmitted} of {resources.TcpSegmentsSent} sent segments · {Percent(retransmission)}");
            }
            var memoryPressure = MemoryPressurePercent(resources);
            if (memoryPressure is not null) data.Add($"Runtime-reported memory pressure: {Percent(memoryPressure.Value)} of high-load threshold");
            foreach (var item in resources.Interfaces.Where(HasCounterIssue))
            {
                data.Add($"{item.Name} counters: errors {item.IncomingErrors + item.OutgoingErrors} · discards {item.IncomingDiscards + item.OutgoingDiscards}");
            }
        }
        data.Add($"Findings: {findings.Count}");
        return data;
    }

    private static bool HasCounterIssue(InterfaceCounterDelta item) =>
        item.IncomingErrors > 0
        || item.OutgoingErrors > 0
        || item.IncomingDiscards > 0
        || item.OutgoingDiscards > 0;

    private static string FamilyEvidence(AddressFamilyProbeReport family)
    {
        if (!family.AddressAvailable) return "address unavailable";
        var ping = family.PingAvailable ? $"ICMP {Milliseconds(family.PingMedianMs)}" : "ICMP unavailable";
        var tcp = family.TcpReachable ? $"TCP {Milliseconds(family.TcpConnectMs)}" : "TCP unreachable";
        var tls = family.TlsReachable
            ? $"TLS {Milliseconds(family.TlsHandshakeMs)} · {family.TlsProtocol ?? "protocol unknown"}"
            : "TLS unavailable";
        var http = family.HttpReachable
            ? $"HTTP {family.HttpStatusCode?.ToString(CultureInfo.InvariantCulture) ?? "response"} · {Milliseconds(family.HttpResponseMs)}"
            : "HTTP unavailable";
        return $"{ping} · {tcp} · {tls} · {http}";
    }

    private static string PublicNetworkEvidence(NetworkMetadataReport metadata)
    {
        var parts = new[]
        {
            metadata.Network,
            metadata.Asn is null ? null : $"AS{metadata.Asn.Value}",
            metadata.Edge,
            metadata.IpVersion,
            metadata.Protocol
        }.Where(item => !string.IsNullOrWhiteSpace(item));
        return string.Join(" · ", parts);
    }

    private static double? MemoryPressurePercent(HostResourceReport resources)
    {
        if (resources.SystemMemoryLoadBytes is not { } load
            || resources.HighMemoryLoadThresholdBytes is not { } threshold
            || threshold <= 0)
        {
            return null;
        }
        return load / (double)threshold * 100;
    }

    private static string NextHealthyAction(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "Run Quick when you want a broader performance snapshot.",
        TestProfileId.Quick => "Save this report as a baseline, or run Full when you need local-path data.",
        TestProfileId.Standard => "Save this report and compare it with another Full run if the issue returns.",
        _ => "Save this Stress report as a baseline before changing network conditions."
    };

    private static string UserCapabilityLabel(string value) =>
        value.Replace("-evidence", "-data", StringComparison.OrdinalIgnoreCase);

    private static string UserDownloadPath(string value) => value.Trim().ToLowerInvariant() switch
    {
        "direct-r2" => "Direct R2",
        "worker" => "Worker",
        "mixed" => "R2 to Worker",
        "automatic" => "Automatic",
        _ => value
    };

    private static string UserStatus(string value) =>
        string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 2,
        "warning" => 1,
        _ => 0
    };

    private static string SafeMessage(Exception error)
    {
        var message = string.IsNullOrWhiteSpace(error.Message) ? "No additional message was provided." : error.Message;
        return message.Length <= 240 ? message : $"{message[..237]}...";
    }

    private static string DesktopSettingsId(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "connection-check",
        TestProfileId.Quick => "quick",
        TestProfileId.Standard => "standard",
        TestProfileId.Extended => "extended",
        _ => "unknown"
    };

    private static string Percent(double value) => $"{value.ToString("0.0", CultureInfo.InvariantCulture)}%";
    private static string Megabits(double value) => $"{value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} Mbps";
    private static string Milliseconds(double? value) => value is null ? "Not measured" : $"{value.Value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms";
    private static string FormatBytes(long value) => value >= 1_000_000_000
        ? $"{(value / 1_000_000_000d).ToString("0.###", CultureInfo.InvariantCulture)} GB"
        : $"{(value / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture)} MB";
}

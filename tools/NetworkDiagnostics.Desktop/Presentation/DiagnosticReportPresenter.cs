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
            ConnectionCheckOutcome.Inconclusive => hasSectionFailure ? "Completed with partial evidence" : "Result is inconclusive",
            ConnectionCheckOutcome.Unavailable => "Partially measured",
            _ => "Test did not complete"
        };
        var verdict = outcome switch
        {
            ConnectionCheckOutcome.Healthy => $"{profileName} completed without an obvious problem.",
            ConnectionCheckOutcome.Problematic => $"{profileName} found evidence worth investigating.",
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
                    "The report remains available in Technical evidence for manual inspection.")
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
        if (report.InternetTransfer is { } internet)
        {
            return
            [
                new("Latency", Milliseconds(internet.IdleLatency.MedianMs), "Median first-party HTTP latency", internet.IdleLatency.MedianMs is not null),
                new("Request loss", Percent(internet.IdleLatency.LossPercent), $"{internet.IdleLatency.Received} of {internet.IdleLatency.Sent} responses"),
                new("Download", Megabits(internet.Download.SteadyMbps), internet.Download.Qualification),
                new("Upload", Megabits(internet.Upload.SteadyMbps), internet.Upload.Qualification)
            ];
        }

        if (report.DeepDiagnostics is { } deep)
        {
            var fastestDns = deep.DnsResolvers
                .Where(item => item.MedianMs is not null)
                .OrderBy(item => item.MedianMs)
                .FirstOrDefault();
            return
            [
                new("Gateway", Milliseconds(deep.GatewayPing?.Statistics.MedianMs), "Local gateway median", deep.GatewayPing?.Statistics.MedianMs is not null),
                new("Internet", Milliseconds(deep.InternetPing.Statistics.MedianMs), "Public target median", deep.InternetPing.Statistics.MedianMs is not null),
                new("DNS", Milliseconds(fastestDns?.MedianMs), fastestDns is null ? "Not measured" : $"Fastest · {fastestDns.Name}", fastestDns is not null),
                new("Wi-Fi", deep.Wifi?.SignalPercent is int signal ? $"{signal}%" : "Not measured", "Operating-system signal estimate", deep.Wifi?.SignalPercent is not null)
            ];
        }

        return
        [
            new("Latency", "Not measured", "No supported latency section", false),
            new("Request loss", "Not measured", "No supported reliability section", false),
            new("Download", "Not measured", "No supported transfer section", false),
            new("Upload", "Not measured", "No supported transfer section", false)
        ];
    }

    private static IReadOnlyList<string> TechnicalEvidence(
        NetworkDiagnosticsReportV2 report,
        IReadOnlyList<DiagnosticFinding> findings)
    {
        var evidence = new List<string>
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
            if (!string.IsNullOrWhiteSpace(annotations.Label)) evidence.Add($"Report label: {annotations.Label}");
            if (annotations.Tags.Count > 0) evidence.Add($"Report tags: {string.Join(", ", annotations.Tags)}");
        }
        if (report.InternetTransfer is { } internet)
        {
            evidence.Add($"Measured payload: {FormatBytes(internet.DataUsedBytes)}");
            evidence.Add($"Endpoint origin: {internet.Origin}");
        }
        if (report.Measurement is { } measurement)
        {
            evidence.Add($"Selected endpoint: {measurement.SelectedEndpoint.Name} · {measurement.SelectedEndpoint.Provider}");
            evidence.Add($"Endpoint selection: {measurement.SelectedEndpoint.SelectionReason}");
            if (measurement.SelectedEndpoint.PreflightLatencyMs is { } preflight)
            {
                evidence.Add($"Endpoint preflight median: {preflight.ToString("0.0", CultureInfo.InvariantCulture)} ms");
            }
            evidence.Add($"Capabilities: {string.Join(", ", measurement.Capabilities)}");
        }
        if (report.DualStack is { } dualStack)
        {
            evidence.Add($"Address-family probe: {dualStack.Status} · preferred {dualStack.PreferredFamily}");
            evidence.Add($"Dual-stack DNS: {Milliseconds(dualStack.DnsResolutionMs)} · {dualStack.Ipv4AddressCount} IPv4 · {dualStack.Ipv6AddressCount} IPv6 addresses");
            if (dualStack.ParallelConnectWinner is { } winner)
            {
                evidence.Add($"Parallel family winner: {winner} · difference {Milliseconds(dualStack.ParallelConnectDifferenceMs)}");
            }
            evidence.Add($"IPv4: {FamilyEvidence(dualStack.Ipv4)}");
            evidence.Add($"IPv6: {FamilyEvidence(dualStack.Ipv6)}");
            if (dualStack.Nat64Suspected) evidence.Add("NAT64/DNS64: suspected from the resolved IPv6 prefix");
        }
        if (report.LoadLocalization is { } localization)
        {
            evidence.Add($"Loaded path localization: {localization.Status} · {localization.LikelyBoundary ?? "no clear boundary"}");
            evidence.Add($"Loaded path summary: {localization.Summary}");
            foreach (var target in localization.Targets)
            {
                evidence.Add($"{target.Label}: idle {Milliseconds(target.Idle.MedianMs)} · download {Milliseconds(target.Download.MedianMs)} · upload {Milliseconds(target.Upload.MedianMs)}");
            }
        }
        if (report.NetworkChange is { } networkChange)
        {
            evidence.Add($"Network state: {(networkChange.Changed ? "changed during run" : "stable during run")}");
            evidence.Add($"Captive portal: {(networkChange.CaptivePortalSuspected ? "suspected" : "not detected")}");
            if (networkChange.Before.Proxy is { } proxy) evidence.Add($"System proxy: {proxy}");
            if (networkChange.Before.TunnelInterfaces.Count > 0)
            {
                evidence.Add($"Tunnel interfaces: {string.Join(", ", networkChange.Before.TunnelInterfaces)}");
            }
            if (networkChange.PublicNetworkBefore is { } before)
            {
                evidence.Add($"Public path before: {PublicNetworkEvidence(before)}");
            }
            if (networkChange.PublicNetworkAfter is { } after)
            {
                evidence.Add($"Public path after: {PublicNetworkEvidence(after)}");
            }
            foreach (var change in networkChange.Changes) evidence.Add($"Network change: {change}");
        }
        if (report.HostResources is { } resources)
        {
            evidence.Add($"Diagnostic process CPU: {Percent(resources.ProcessCpuPercent)}");
            evidence.Add($"Peak working set: {FormatBytes(resources.PeakWorkingSetBytes)}");
            if (resources.TcpRetransmissionPercent is { } retransmission)
            {
                evidence.Add($"TCP retransmissions: {resources.TcpSegmentsRetransmitted} of {resources.TcpSegmentsSent} sent segments · {Percent(retransmission)}");
            }
            var memoryPressure = MemoryPressurePercent(resources);
            if (memoryPressure is not null) evidence.Add($"Runtime-reported memory pressure: {Percent(memoryPressure.Value)} of high-load threshold");
            foreach (var item in resources.Interfaces.Where(HasCounterIssue))
            {
                evidence.Add($"{item.Name} counters: errors {item.IncomingErrors + item.OutgoingErrors} · discards {item.IncomingDiscards + item.OutgoingDiscards}");
            }
        }
        evidence.Add($"Findings: {findings.Count}");
        return evidence;
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
        TestProfileId.Quick => "Save this report as a baseline, or run Full when you need local-path evidence.",
        TestProfileId.Standard => "Save this report and compare it with another Full run if the issue returns.",
        _ => "Save this Stress report as a baseline before changing network conditions."
    };

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

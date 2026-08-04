using System.Globalization;
using System.Runtime.InteropServices;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

internal static class FullDiagnosticRunner
{
    public static Task<NetworkDiagnosticsReportV2> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        CancellationToken cancellationToken) =>
        RunAsync(options, progress, null, cancellationToken);

    public static async Task<NetworkDiagnosticsReportV2> RunAsync(
        ProbeOptions options,
        IProgress<string>? progress,
        IProgress<NativeTransferProgress>? detailedTransferProgress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var plan = NativeTransferPlanBuilder.Build(options.Profile, options.TransferMethod);

        progress?.Report("Selecting the measurement endpoint and reading network metadata");
        var preflight = await MeasurementPreflight.RunAsync(
            options.CandidateOrigins,
            options.InterfaceId,
            options.IncludeAddresses,
            "network-diagnostics-native",
            Capabilities(options),
            cancellationToken);

        await using var advancedSession = await AdvancedEvidenceSession.StartAsync(
            preflight.EndpointSelection.Selected.Origin,
            options.InterfaceId,
            options.IncludeAddresses,
            options.Profile is TestProfileId.Standard or TestProfileId.Extended,
            cancellationToken);

        var lastStage = string.Empty;
        var transferProgress = new Progress<NativeTransferProgress>(current =>
        {
            advancedSession.SetPhase(current.Phase);
            detailedTransferProgress?.Report(current);
            if (current.Stage == lastStage) return;
            lastStage = current.Stage;
            progress?.Report(current.Phase switch
            {
                "idle" => "Measuring first-party HTTP latency",
                "download" => $"Measuring {current.Stage} download",
                "upload" => $"Measuring {current.Stage} upload",
                "complete" => "Internet transfer measurements complete",
                _ => $"Running {current.Stage}"
            });
        });

        var internetTransfer = await InternetTransferProbe.RunAsync(
            plan,
            preflight.EndpointSelection.Selected.Origin,
            transferProgress,
            cancellationToken,
            preflight.Binding?.SourceAddress);

        DeepProbeReport? deepDiagnostics = null;
        string? deepFailure = null;
        if (options.Profile is TestProfileId.Standard or TestProfileId.Extended)
        {
            try
            {
                deepDiagnostics = await ProbeRunner.RunAsync(options, progress, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                deepFailure = SafeError(error);
                progress?.Report("Local and path diagnostics did not complete; preserving Internet measurements");
            }
        }

        progress?.Report("Finalizing dual-stack, network-change, and host evidence");
        var advancedEvidence = await advancedSession.CompleteAsync(cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;
        var report = new NetworkDiagnosticsReportV2(
            "2.0",
            completedAt,
            new DiagnosticRunMetadata(
                Guid.NewGuid(),
                RuntimeInformation.OSDescription,
                RuntimeInformation.OSArchitecture.ToString(),
                plan.Profile,
                plan.Method,
                startedAt,
                completedAt,
                options.IncludeAddresses),
            NativeTransferPlanReport.FromPlan(plan),
            internetTransfer,
            deepDiagnostics,
            deepDiagnostics?.LocalLink,
            null,
            preflight.Measurement,
            null,
            null,
            advancedEvidence.LoadLocalization,
            advancedEvidence.DualStack,
            advancedEvidence.NetworkChange,
            advancedEvidence.HostResources);

        var findings = DiagnosticClassifier.Classify(report).ToList();
        AddAdvancedFindings(report, findings);
        if (preflight.Measurement.Http3 is { Attempted: true, Supported: false } http3)
        {
            findings.Add(new DiagnosticFinding(
                "http3-unavailable",
                "protocol",
                "info",
                "high",
                "HTTP/3 was not available on the selected path",
                "The exact-version HTTP/3 request did not complete. The normal transfer phases still used supported HTTP versions and remain valid.",
                [new DiagnosticEvidence("measurement.http3.supported", "HTTP/3", "Unavailable", http3.Error)],
                ["No change is required unless you are diagnosing QUIC or HTTP/3 specifically. Compare another network or endpoint to isolate path filtering."],
                "Repeat the protocol probe on another network or endpoint."));
        }
        if (deepFailure is not null)
        {
            findings.Insert(0, new DiagnosticFinding(
                "deep-diagnostics-failed",
                "measurement-quality",
                "info",
                "high",
                "Local and path diagnostics did not complete",
                "The Internet transfer phases completed and remain available, but the operating-system deep-probe section failed before it could produce evidence.",
                [new DiagnosticEvidence("deepDiagnostics.status", "Deep diagnostics", "Failed", deepFailure)],
                ["Repeat the same profile. If the section fails again, inspect permissions and the technical error before changing network settings."],
                "Repeat Full or Stress after resolving the local diagnostic error."));
        }

        return report with { Findings = findings };
    }

    internal static IEnumerable<string> Capabilities(ProbeOptions options)
    {
        yield return "endpoint-preflight";
        yield return "network-metadata";
        yield return "http3-probe";
        yield return "dual-stack-probe";
        yield return "network-change-detection";
        yield return "captive-portal-detection";
        yield return "host-resource-evidence";
        yield return "http-latency";
        yield return "download-throughput";
        yield return "upload-throughput";
        yield return "loaded-latency";
        if (!string.IsNullOrWhiteSpace(options.InterfaceId)) yield return "source-interface-binding";

        if (options.Profile is not (TestProfileId.Standard or TestProfileId.Extended)) yield break;

        yield return "loaded-path-localization";
        yield return "network-interfaces";
        yield return "gateway-latency";
        yield return "icmp-latency";
        yield return "traceroute";
        yield return "dns-resolvers";
        yield return "path-mtu";
        yield return "service-reachability";
        yield return "wifi-details";
        yield return "routing-details";
        if (options.LanTarget is not null) yield return "local-link-throughput";
    }

    private static void AddAdvancedFindings(
        NetworkDiagnosticsReportV2 report,
        ICollection<DiagnosticFinding> findings)
    {
        if (report.NetworkChange is { Changed: true } change)
        {
            findings.Add(new DiagnosticFinding(
                "network-changed-during-run",
                "measurement-quality",
                "warning",
                "high",
                "The active network changed during the measurement",
                "The result combines evidence collected across a changing interface, route, gateway, address-family, or proxy state and should not be treated as a stable baseline.",
                change.Changes.Select((item, index) => new DiagnosticEvidence(
                    $"networkChange.changes[{index}]",
                    "Network change",
                    item)).ToArray(),
                ["Repeat the same profile after the device has remained on one interface and network for the complete run."],
                "Repeat the same profile on a stable connection."));
        }
        if (report.NetworkChange is { CaptivePortalSuspected: true })
        {
            findings.Add(new DiagnosticFinding(
                "captive-portal-suspected",
                "reachability",
                "warning",
                "medium",
                "A captive portal or HTTP interception may be active",
                "The first-party ping request was redirected or returned HTML instead of the expected measurement response.",
                [new DiagnosticEvidence("networkChange.captivePortalSuspected", "Captive portal", "Suspected")],
                ["Open a normal browser page and complete any sign-in prompt, then repeat Connection Check."],
                "Repeat Connection Check after portal authentication."));
        }
        if (report.DualStack is { } dualStack)
        {
            var brokenIpv6 = dualStack.Ipv6.AddressAvailable
                && !dualStack.Ipv6.TcpReachable
                && dualStack.Ipv4.TcpReachable;
            var brokenIpv4 = dualStack.Ipv4.AddressAvailable
                && !dualStack.Ipv4.TcpReachable
                && dualStack.Ipv6.TcpReachable;
            if (brokenIpv6 || brokenIpv4)
            {
                var family = brokenIpv6 ? "IPv6" : "IPv4";
                findings.Add(new DiagnosticFinding(
                    $"{family.ToLowerInvariant()}-path-broken",
                    "protocol",
                    "warning",
                    "high",
                    $"{family} was advertised but could not establish the test connection",
                    $"DNS returned a {family} address, but the direct TCP probe failed while the other address family remained reachable.",
                    [new DiagnosticEvidence(
                        brokenIpv6 ? "dualStack.ipv6.tcpReachable" : "dualStack.ipv4.tcpReachable",
                        family,
                        "Unreachable",
                        brokenIpv6 ? dualStack.Ipv6.Error : dualStack.Ipv4.Error)],
                    ["Compare another network and review router, VPN, firewall, and ISP address-family configuration before disabling the protocol globally."],
                    "Repeat Full on another network or with the VPN disabled."));
            }
            if (dualStack.Ipv4.TcpReachable
                && dualStack.Ipv6.TcpReachable
                && dualStack.Ipv4.TcpConnectMs is { } ipv4Ms
                && dualStack.Ipv6.TcpConnectMs is { } ipv6Ms
                && ipv6Ms - ipv4Ms > 100)
            {
                findings.Add(new DiagnosticFinding(
                    "ipv6-path-slower",
                    "protocol",
                    "info",
                    "medium",
                    "IPv6 connected materially slower than IPv4",
                    "Both address families worked, but the IPv6 TCP connection took more than 100 milliseconds longer on this endpoint and route.",
                    [
                        new DiagnosticEvidence("dualStack.ipv4.tcpConnectMs", "IPv4 connect", Milliseconds(ipv4Ms)),
                        new DiagnosticEvidence("dualStack.ipv6.tcpConnectMs", "IPv6 connect", Milliseconds(ipv6Ms))
                    ],
                    ["Repeat the same test before changing settings; endpoint routing and transient congestion can affect one sample."],
                    "Compare another Full report on the same network."));
            }
        }
        if (report.LoadLocalization is { LikelyBoundary: not null } localization)
        {
            findings.Add(new DiagnosticFinding(
                $"loaded-latency-boundary-{localization.LikelyBoundary}",
                "responsiveness",
                localization.LikelyBoundary == "upstream-path" ? "info" : "warning",
                "medium",
                localization.LikelyBoundary switch
                {
                    "local-network" => "Loaded latency begins on the local network",
                    "access-link" => "Loaded latency begins near the ISP access link",
                    _ => "Loaded latency appears farther upstream"
                },
                localization.Summary,
                localization.Targets.Select(target => new DiagnosticEvidence(
                    $"loadLocalization.{target.Id}",
                    target.Label,
                    $"idle {Milliseconds(target.Idle.MedianMs)} · down {Milliseconds(target.Download.MedianMs)} · up {Milliseconds(target.Upload.MedianMs)}")).ToArray(),
                localization.LikelyBoundary == "local-network"
                    ? ["Compare Ethernet with Wi-Fi and inspect router queue-management settings."]
                    : ["Repeat against another endpoint and compare at another time before attributing the issue to one provider."],
                localization.LikelyBoundary == "local-network"
                    ? "Run LAN isolation and repeat Full over Ethernet."
                    : "Repeat Full with another endpoint candidate."));
        }
        if (report.HostResources is { PotentialClientBottleneck: true } resources)
        {
            var counterIssues = resources.Interfaces
                .Where(item => item.IncomingErrors + item.OutgoingErrors + item.IncomingDiscards + item.OutgoingDiscards > 0)
                .ToArray();
            findings.Add(new DiagnosticFinding(
                "client-resource-bottleneck",
                "client",
                "warning",
                "medium",
                "The client may have limited the measurement",
                "The diagnostic process used a high share of available CPU or interface error/discard counters increased during the run.",
                [
                    new DiagnosticEvidence("hostResources.processCpuPercent", "Diagnostic process CPU", Percent(resources.ProcessCpuPercent)),
                    new DiagnosticEvidence("hostResources.interfaceCounterIssues", "Interfaces with errors or discards", counterIssues.Length.ToString(CultureInfo.InvariantCulture))
                ],
                ["Close other heavy workloads, disable power saving for the adapter, and repeat the same profile before treating the throughput result as a network ceiling."],
                "Repeat the same profile with the client otherwise idle."));
        }
    }

    private static string SafeError(Exception error)
    {
        var message = string.IsNullOrWhiteSpace(error.Message)
            ? error.GetType().Name
            : $"{error.GetType().Name}: {error.Message}";
        return message.Length <= 320 ? message : $"{message[..317]}...";
    }

    private static string Milliseconds(double? value) => value is null
        ? "not measured"
        : $"{value.Value.ToString("0.0", CultureInfo.InvariantCulture)} ms";

    private static string Percent(double value) =>
        $"{value.ToString("0.0", CultureInfo.InvariantCulture)}%";
}

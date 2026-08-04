using System.Globalization;
using NetworkDeepProbe.Contracts;
using NetworkDeepProbe.Models;
using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

public static class DiagnosticClassifier
{
    private static readonly Lazy<DiagnosticRulesDocument> Rules = new(DiagnosticRulesContract.Load);

    public static IReadOnlyList<DiagnosticFinding> Classify(NetworkDiagnosticsReportV2 report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var findings = new List<DiagnosticFinding>();
        var internet = report.InternetTransfer;
        if (internet is not null)
        {
            AddInternetFindings(findings, report, internet);
        }

        if (report.DeepDiagnostics is not null)
        {
            AddLocalFindings(findings, report.DeepDiagnostics, internet);
        }

        if (findings.Count == 0 && internet is null && report.DeepDiagnostics is null)
        {
            findings.Add(new DiagnosticFinding(
                "no-measurements",
                "measurement-quality",
                "info",
                "low",
                "No supported measurements were present",
                "The report is valid, but this reader did not find measurements it can evaluate.",
                [],
                ["Run a new diagnostic or inspect the report's technical evidence."],
                null));
            return findings;
        }

        if (!findings.Any(item => item.Severity is "warning" or "critical"))
        {
            findings.Insert(0, new DiagnosticFinding(
                "no-obvious-instability",
                "summary",
                "info",
                report.Run.Profile is TestProfileId.ConnectionCheck or TestProfileId.Quick ? "medium" : "high",
                "No obvious instability appeared",
                "This run did not cross the shared warning thresholds for loss, latency variation, loaded delay, flow scaling, throughput stability, or local-path evidence.",
                internet is null
                    ? []
                    :
                    [
                        Evidence("internetTransfer.idleLatency.lossPercent", "Application request loss", Percent(internet.IdleLatency.LossPercent)),
                        Evidence("internetTransfer.loadedLatency.worstIncreaseMs", "Worst loaded increase", $"+{Milliseconds(WorstLoadedIncrease(internet))}")
                    ],
                ["Save this report as a baseline and compare it with a future run if the connection feels worse."],
                report.Run.Profile == TestProfileId.ConnectionCheck
                    ? "Run Quick when you want a broader performance snapshot."
                    : report.Run.Profile == TestProfileId.Quick
                        ? "Run Full only when you need deeper confirmation."
                        : null));
        }

        return findings;
    }

    private static void AddInternetFindings(
        List<DiagnosticFinding> findings,
        NetworkDiagnosticsReportV2 report,
        NativeInternetTransferReport internet)
    {
        var rules = Rules.Value;
        var nextTest = NextTest(report.Run.Profile);

        if (internet.IdleLatency.LossPercent >= rules.ApplicationLatency.RequestLossWarningPercent)
        {
            var hasCriticalSampleCount = internet.IdleLatency.Sent >= rules.ApplicationLatency.MinimumSamplesForCriticalLoss;
            var severity = hasCriticalSampleCount
                && internet.IdleLatency.LossPercent >= rules.ApplicationLatency.RequestLossCriticalPercent
                    ? "critical"
                    : "warning";
            var confidence = internet.IdleLatency.Sent >= rules.ApplicationLatency.MinimumSamplesForCriticalLoss
                ? "high"
                : internet.IdleLatency.Sent >= 8 ? "medium" : "low";
            findings.Add(new DiagnosticFinding(
                "application-request-loss",
                "reliability",
                severity,
                confidence,
                "Requests were lost while the connection was idle",
                hasCriticalSampleCount
                    ? "One or more first-party HTTP latency requests failed or timed out. This is application request loss; native ICMP evidence is evaluated separately when available."
                    : "At least one request failed in a small sample. Treat this as a warning to repeat, not proof of severe persistent loss.",
                [
                    Evidence("internetTransfer.idleLatency.lossPercent", "Request loss", Percent(internet.IdleLatency.LossPercent)),
                    Evidence("internetTransfer.idleLatency.received", "Responses", $"{internet.IdleLatency.Received} of {internet.IdleLatency.Sent}")
                ],
                [
                    "Repeat the same profile once before changing network settings.",
                    "If loss persists, compare Ethernet or another device."
                ],
                nextTest));
        }

        var worstLoaded = WorstLoadedIncrease(internet);
        if (worstLoaded >= rules.LoadedLatency.WarningIncreaseMs)
        {
            var uploadIsWorst = (internet.UploadLatency.IncreaseMs ?? 0) >= (internet.DownloadLatency.IncreaseMs ?? 0);
            var direction = uploadIsWorst ? "upload" : "download";
            findings.Add(new DiagnosticFinding(
                "loaded-latency",
                "responsiveness",
                worstLoaded >= rules.LoadedLatency.CriticalIncreaseMs ? "critical" : "warning",
                "high",
                "Responsiveness falls under load",
                $"Latency rose most during {direction}. This pattern is consistent with queueing on the measured path, often called bufferbloat.",
                [
                    Evidence("internetTransfer.idleLatency.medianMs", "Idle median", Milliseconds(internet.IdleLatency.MedianMs)),
                    Evidence($"internetTransfer.{direction}Latency.increaseMs", "Worst increase", $"+{Milliseconds(worstLoaded)}")
                ],
                [
                    "Enable Smart Queue Management on the router if it is available.",
                    "Limit heavy background uploads or downloads, then repeat the same profile."
                ],
                nextTest));
        }

        if ((internet.IdleLatency.JitterMs ?? 0) >= rules.ApplicationLatency.IdleJitterWarningMs)
        {
            findings.Add(new DiagnosticFinding(
                "idle-latency-variation",
                "responsiveness",
                "warning",
                internet.IdleLatency.Sent >= rules.ApplicationLatency.MinimumSamplesForCriticalLoss ? "high" : "medium",
                "Idle latency is inconsistent",
                "Round-trip time varied enough to affect interactive traffic even before the throughput phases began.",
                [
                    Evidence("internetTransfer.idleLatency.jitterMs", "Jitter", Milliseconds(internet.IdleLatency.JitterMs)),
                    Evidence("internetTransfer.idleLatency.p95Ms", "95th percentile", Milliseconds(internet.IdleLatency.P95Ms))
                ],
                ["Compare Ethernet and Wi-Fi runs, and pause other traffic while testing."],
                nextTest));
        }
        else if ((internet.IdleLatency.MedianMs ?? 0) >= rules.ApplicationLatency.IdleMedianWarningMs)
        {
            findings.Add(new DiagnosticFinding(
                "high-idle-latency",
                "responsiveness",
                "warning",
                "medium",
                "The measured Internet path has high baseline latency",
                "The first-party endpoint answered consistently, but the median response time is high for interactive work.",
                [Evidence("internetTransfer.idleLatency.medianMs", "Idle median", Milliseconds(internet.IdleLatency.MedianMs))],
                ["Compare another device or run Full to determine whether the delay begins locally or upstream."],
                nextTest));
        }

        var single = internet.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Single)?.Download;
        var aggregate = internet.FlowMeasurements.FirstOrDefault(item => item.Strategy == TransferStrategy.Aggregate)?.Download;
        if (single is not null && aggregate is not null && aggregate.SteadyMbps >= rules.Throughput.MinimumAggregateMbpsForFlowComparison)
        {
            var share = single.SteadyMbps / Math.Max(aggregate.SteadyMbps, 0.001) * 100;
            if (share < rules.Throughput.SingleFlowShareWarningPercent)
            {
                findings.Add(new DiagnosticFinding(
                    "single-flow-limited",
                    "throughput",
                    "warning",
                    single.CapReached || aggregate.CapReached ? "medium" : "high",
                    "One connection cannot use the available capacity",
                    "Parallel transfers were materially faster than one sustained transfer. A single download, tunnel, or remote session may underperform even when aggregate speed looks healthy.",
                    [
                        Evidence("internetTransfer.flowMeasurements.single.download.steadyMbps", "Single connection", Megabits(single.SteadyMbps)),
                        Evidence("internetTransfer.flowMeasurements.aggregate.download.steadyMbps", "Aggregate", Megabits(aggregate.SteadyMbps)),
                        Evidence("internetTransfer.flowMeasurements.singleSharePercent", "Single-flow share", Percent(share))
                    ],
                    ["Compare another endpoint or time of day before attributing this to the local network or ISP."],
                    nextTest));
            }
        }

        if (Variable(internet.Download) || Variable(internet.Upload))
        {
            findings.Add(new DiagnosticFinding(
                "variable-throughput",
                "throughput",
                "warning",
                "medium",
                "Throughput changed materially during the run",
                "At least one direction was unstable or declining, so a single average hides how the transfer behaved over time.",
                [
                    Evidence("internetTransfer.throughput.minimumStabilityPercent", "Lowest stability", Percent(Math.Min(internet.Download.StabilityPercent, internet.Upload.StabilityPercent))),
                    Evidence("internetTransfer.download.qualification", "Download sample", internet.Download.Qualification),
                    Evidence("internetTransfer.upload.qualification", "Upload sample", internet.Upload.Qualification)
                ],
                ["Repeat the same profile after background traffic settles and compare saved reports."],
                nextTest));
        }

        if (internet.Download.CapReached || internet.Upload.CapReached)
        {
            var directions = string.Join(" and ", new[]
            {
                internet.Download.CapReached ? "download" : null,
                internet.Upload.CapReached ? "upload" : null
            }.Where(item => item is not null));
            var lightweight = report.Run.Profile is TestProfileId.ConnectionCheck or TestProfileId.Quick;
            findings.Add(new DiagnosticFinding(
                "measurement-cap-reached",
                "measurement-quality",
                "info",
                "high",
                lightweight
                    ? "The lightweight data ceiling limited this sample"
                    : "The profile data ceiling limited this sample",
                $"The {directions} phase reached its byte cap before time expired. The result remains useful, but may be below peak capacity.",
                [Evidence("internetTransfer.dataUsedBytes", "Transferred", Bytes(internet.DataUsedBytes))],
                [lightweight
                    ? "Use Full when you need a longer, more representative throughput measurement."
                    : "Repeat the same profile only when you need another comparable sample."],
                lightweight ? "Run the Full diagnostic." : nextTest));
        }
    }

    private static void AddLocalFindings(
        List<DiagnosticFinding> findings,
        DeepProbeReport deep,
        NativeInternetTransferReport? internet)
    {
        var rules = Rules.Value.LocalDiagnostics;
        var gatewayMedian = deep.GatewayPing?.Statistics.MedianMs;
        var publicMedian = deep.InternetPing.Statistics.MedianMs;
        if (gatewayMedian is not null
            && publicMedian is not null
            && gatewayMedian <= rules.HealthyGatewayMedianMs
            && publicMedian >= rules.HighInternetMedianMs)
        {
            findings.Add(new DiagnosticFinding(
                "delay-begins-upstream",
                "path",
                "warning",
                "medium",
                "The largest baseline delay begins beyond the router",
                "The local gateway responded quickly while the public target was much slower. The evidence points beyond the first local hop, but does not identify one carrier or device as the cause.",
                [
                    Evidence("deepDiagnostics.gatewayPing.statistics.medianMs", "Gateway median", Milliseconds(gatewayMedian)),
                    Evidence("deepDiagnostics.internetPing.statistics.medianMs", "Public target median", Milliseconds(publicMedian))
                ],
                ["Compare another public target or a second measurement provider before escalating to the ISP."],
                "Repeat Full at a different time of day."));
        }

        if (deep.Wifi?.SignalPercent is int signalPercent && signalPercent < rules.WeakWifiSignalPercent)
        {
            findings.Add(new DiagnosticFinding(
                "weak-wifi-signal",
                "local-link",
                "warning",
                "high",
                "Wi-Fi signal is weak",
                "The operating system reported a low signal level on the active wireless interface.",
                [Evidence("deepDiagnostics.wifi.signalPercent", "Signal", Percent(signalPercent))],
                ["Move closer to the access point or compare the same profile over Ethernet."],
                "Repeat the Connection Check after changing location."));
        }

        var successfulDns = deep.DnsResolvers
            .Where(item => item.Successful > 0 && item.MedianMs is not null)
            .ToArray();
        if (successfulDns.Length > 0 && successfulDns.Min(item => item.MedianMs!.Value) >= rules.SlowDnsMedianMs)
        {
            var fastest = successfulDns.OrderBy(item => item.MedianMs).First();
            findings.Add(new DiagnosticFinding(
                "slow-dns",
                "dns",
                "warning",
                "medium",
                "DNS responses are slow across the tested resolvers",
                "Even the fastest successful resolver crossed the shared warning threshold in this run.",
                [Evidence("deepDiagnostics.dnsResolvers.fastestMedianMs", $"Fastest · {fastest.Name}", Milliseconds(fastest.MedianMs))],
                ["Retry before changing resolver settings; DNS latency can be transient and route-dependent."],
                "Repeat Full and compare the per-resolver table."));
        }

        if (deep.LocalLink is not null && internet is not null)
        {
            var localMaximum = Math.Max(deep.LocalLink.DownloadMbps, deep.LocalLink.UploadMbps);
            var internetMaximum = Math.Max(internet.Download.SteadyMbps, internet.Upload.SteadyMbps);
            var share = localMaximum / Math.Max(internetMaximum, 0.001) * 100;
            if (internetMaximum >= Rules.Value.Throughput.MinimumAggregateMbpsForFlowComparison
                && share < rules.LocalLinkShareWarningPercent)
            {
                findings.Add(new DiagnosticFinding(
                    "local-link-limited",
                    "local-link",
                    "warning",
                    "high",
                    "The measured local link is slower than the Internet result",
                    "The optional LAN server path produced materially less throughput than the public endpoint, which points to the device-to-LAN path or LAN test host.",
                    [
                        Evidence("deepDiagnostics.localLink.maximumMbps", "LAN maximum", Megabits(localMaximum)),
                        Evidence("internetTransfer.maximumMbps", "Internet maximum", Megabits(internetMaximum)),
                        Evidence("deepDiagnostics.localLink.sharePercent", "LAN share", Percent(share))
                    ],
                    ["Verify the LAN server is wired and idle, then compare Ethernet and Wi-Fi from this device."],
                    "Repeat the LAN test after checking both link speeds."));
            }
        }
    }

    private static bool Variable(NativeThroughputSummary summary) =>
        summary.StabilityPercent < Rules.Value.Throughput.StabilityWarningPercent
        || summary.Qualification is "unstable" or "declining";

    private static double WorstLoadedIncrease(NativeInternetTransferReport report) =>
        Math.Max(report.DownloadLatency.IncreaseMs ?? 0, report.UploadLatency.IncreaseMs ?? 0);

    private static string NextTest(TestProfileId profile) => profile switch
    {
        TestProfileId.ConnectionCheck => "Run Full if you need to separate a local-network issue from an upstream path issue.",
        TestProfileId.Quick => "Run Full to confirm this finding with longer samples and local-path evidence.",
        TestProfileId.Standard => "Run Stress only if you need a longer capacity and connection-scaling measurement.",
        _ => "Repeat Stress after changing one condition so the reports remain comparable."
    };

    private static DiagnosticEvidence Evidence(string metric, string label, string value, string? detail = null) =>
        new(metric, label, value, detail);

    private static string Percent(double value) => $"{value.ToString("0.0", CultureInfo.InvariantCulture)}%";
    private static string Megabits(double value) => $"{value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} Mbps";
    private static string Milliseconds(double? value) => value is null ? "Unavailable" : $"{value.Value.ToString(value >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms";
    private static string Bytes(long value) => $"{(value / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture)} MB";
}

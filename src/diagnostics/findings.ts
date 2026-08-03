import rules from "../../contracts/diagnostic-rules.v1.json";
import { formatLatency, formatRate } from "../core/format";
import type {
  DiagnosticEvidence,
  DiagnosticFinding,
  DiagnosticResult,
  FindingSeverity,
  FlowMeasurement,
  ThroughputSummary
} from "../types/diagnostics";

function evidence(metric: string, label: string, value: string, detail?: string): DiagnosticEvidence {
  return { metric, label, value, detail };
}

function loadedSeverity(increaseMs: number): FindingSeverity {
  return increaseMs >= rules.loadedLatency.criticalIncreaseMs ? "critical" : "warning";
}

function nextProfile(result: DiagnosticResult): string {
  if (result.mode === "quick") return "Run the Full diagnostic to confirm this finding with longer samples.";
  if (result.mode === "standard") return "Run Stress only if you need a longer capacity and connection-scaling measurement.";
  return "Repeat the Stress diagnostic after changing one condition so the reports remain comparable.";
}

function flow(result: DiagnosticResult, strategy: FlowMeasurement["strategy"]): FlowMeasurement | undefined {
  return result.flowMeasurements?.find((measurement) => measurement.strategy === strategy);
}

function variable(summary: ThroughputSummary): boolean {
  return summary.stabilityPercent < rules.throughput.stabilityWarningPercent
    || summary.qualification === "unstable"
    || summary.qualification === "declining";
}

export function classifyDiagnosticResult(result: DiagnosticResult): DiagnosticFinding[] {
  const findings: DiagnosticFinding[] = [];
  const worstLoadedIncrease = Math.max(
    result.downloadLatency.increaseMs ?? 0,
    result.uploadLatency.increaseMs ?? 0
  );

  if (result.idleLatency.lossPercent >= rules.applicationLatency.requestLossWarningPercent) {
    const severity: FindingSeverity = result.idleLatency.lossPercent >= rules.applicationLatency.requestLossCriticalPercent
      ? "critical"
      : "warning";
    findings.push({
      id: "application-request-loss",
      category: "reliability",
      severity,
      confidence: result.idleLatency.sent >= 20 ? "high" : "medium",
      title: "Requests were lost while the connection was idle",
      summary: "One or more first-party HTTP latency requests failed or timed out. This is application request loss, not raw IP packet loss.",
      evidence: [
        evidence("idleLatency.lossPercent", "Request loss", `${result.idleLatency.lossPercent.toFixed(1)}%`),
        evidence("idleLatency.received", "Responses", `${result.idleLatency.received} of ${result.idleLatency.sent}`)
      ],
      recommendations: [
        "Repeat the check over Ethernet or close to the Wi-Fi access point.",
        "If loss persists, compare another device before changing router or ISP settings."
      ],
      nextTest: nextProfile(result)
    });
  }

  if (worstLoadedIncrease >= rules.loadedLatency.warningIncreaseMs) {
    const direction = (result.uploadLatency.increaseMs ?? 0) >= (result.downloadLatency.increaseMs ?? 0)
      ? "upload"
      : "download";
    findings.push({
      id: "loaded-latency",
      category: "responsiveness",
      severity: loadedSeverity(worstLoadedIncrease),
      confidence: "high",
      title: "Responsiveness falls under load",
      summary: `Latency rose most during ${direction}. This pattern is consistent with queueing on the measured path, often called bufferbloat.`,
      evidence: [
        evidence("idleLatency.medianMs", "Idle median", `${formatLatency(result.idleLatency.medianMs)} ms`),
        evidence(`${direction}Latency.increaseMs`, "Worst increase", `+${formatLatency(worstLoadedIncrease)} ms`)
      ],
      recommendations: [
        "Enable Smart Queue Management on the router if it is available.",
        "Limit heavy background uploads or downloads, then repeat the same profile."
      ],
      nextTest: nextProfile(result)
    });
  }

  if ((result.idleLatency.jitterMs ?? 0) >= rules.applicationLatency.idleJitterWarningMs) {
    findings.push({
      id: "idle-latency-variation",
      category: "responsiveness",
      severity: "warning",
      confidence: result.idleLatency.sent >= 20 ? "high" : "medium",
      title: "Idle latency is inconsistent",
      summary: "Round-trip time varied enough to affect interactive traffic even before the throughput phases began.",
      evidence: [
        evidence("idleLatency.jitterMs", "Jitter", `${formatLatency(result.idleLatency.jitterMs)} ms`),
        evidence("idleLatency.p95Ms", "95th percentile", `${formatLatency(result.idleLatency.p95Ms)} ms`)
      ],
      recommendations: ["Compare Ethernet and Wi-Fi runs, and pause other traffic while testing."],
      nextTest: nextProfile(result)
    });
  } else if ((result.idleLatency.medianMs ?? 0) >= rules.applicationLatency.idleMedianWarningMs) {
    findings.push({
      id: "high-idle-latency",
      category: "responsiveness",
      severity: "warning",
      confidence: "medium",
      title: "The measured Internet path has high baseline latency",
      summary: "The first-party endpoint answered consistently, but the median response time is high for interactive work.",
      evidence: [evidence("idleLatency.medianMs", "Idle median", `${formatLatency(result.idleLatency.medianMs)} ms`)],
      recommendations: ["Compare a native gateway-latency result to determine whether the delay starts locally or farther upstream."],
      nextTest: "Run the desktop Full diagnostic for gateway and route evidence."
    });
  }

  const single = flow(result, "single")?.download;
  const aggregate = flow(result, "aggregate")?.download;
  if (single && aggregate && aggregate.steadyMbps >= rules.throughput.minimumAggregateMbpsForFlowComparison) {
    const share = (single.steadyMbps / Math.max(aggregate.steadyMbps, 0.001)) * 100;
    if (share < rules.throughput.singleFlowShareWarningPercent) {
      findings.push({
        id: "single-flow-limited",
        category: "throughput",
        severity: "warning",
        confidence: single.capReached || aggregate.capReached ? "medium" : "high",
        title: "One connection cannot use the available capacity",
        summary: "Parallel transfers were materially faster than one sustained transfer. A single download, tunnel, or remote session may underperform even when aggregate speed looks healthy.",
        evidence: [
          evidence("flowMeasurements.single.download.steadyMbps", "Single connection", `${formatRate(single.steadyMbps)} Mbps`),
          evidence("flowMeasurements.aggregate.download.steadyMbps", "Aggregate", `${formatRate(aggregate.steadyMbps)} Mbps`),
          evidence("flowMeasurements.singleSharePercent", "Single-flow share", `${share.toFixed(0)}%`)
        ],
        recommendations: ["Compare another endpoint or time of day before attributing this to the local network."],
        nextTest: nextProfile(result)
      });
    }
  }

  if (variable(result.download) || variable(result.upload)) {
    const stability = Math.min(result.download.stabilityPercent, result.upload.stabilityPercent);
    findings.push({
      id: "variable-throughput",
      category: "throughput",
      severity: "warning",
      confidence: "medium",
      title: "Throughput changed materially during the run",
      summary: "At least one direction was unstable or declining, so a single average hides how the transfer behaved over time.",
      evidence: [
        evidence("throughput.minimumStabilityPercent", "Lowest stability", `${stability.toFixed(0)}%`),
        evidence("download.qualification", "Download sample", result.download.qualification),
        evidence("upload.qualification", "Upload sample", result.upload.qualification)
      ],
      recommendations: ["Repeat the same profile after background traffic settles and compare multiple saved reports."],
      nextTest: nextProfile(result)
    });
  }

  if (result.download.capReached || result.upload.capReached) {
    const directions = [result.download.capReached ? "download" : null, result.upload.capReached ? "upload" : null]
      .filter((value): value is string => value !== null)
      .join(" and ");
    findings.push({
      id: "measurement-cap-reached",
      category: "measurement-quality",
      severity: "info",
      confidence: "high",
      title: "The lightweight data ceiling limited this sample",
      summary: `The ${directions} phase reached its byte cap before time expired. The result is useful as a connection check, but it may be below peak capacity.`,
      evidence: [evidence("dataUsedBytes", "Transferred", `${(result.dataUsedBytes / 1_000_000).toFixed(1)} MB`)],
      recommendations: ["Use Full when you need a more representative throughput measurement."],
      nextTest: result.mode === "quick" ? "Run the Full diagnostic." : nextProfile(result)
    });
  }

  if (!findings.some((finding) => finding.severity !== "info")) {
    findings.unshift({
      id: "no-obvious-instability",
      category: "summary",
      severity: "info",
      confidence: result.mode === "quick" ? "medium" : "high",
      title: "No obvious instability appeared",
      summary: "This run did not cross the shared warning thresholds for request loss, latency variation, loaded delay, flow scaling, or throughput stability.",
      evidence: [
        evidence("idleLatency.lossPercent", "Request loss", `${result.idleLatency.lossPercent.toFixed(1)}%`),
        evidence("loadedLatency.worstIncreaseMs", "Worst loaded increase", `+${formatLatency(worstLoadedIncrease)} ms`)
      ],
      recommendations: ["Save this report as a baseline and compare it with a future run if the connection feels worse."],
      nextTest: result.mode === "quick" ? "Run Full only when you need deeper confirmation." : null
    });
  }

  return findings;
}

export const DIAGNOSTIC_RULES_VERSION = rules.schemaVersion;

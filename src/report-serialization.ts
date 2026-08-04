import { TEST_MODES } from "./diagnostics/config";
import { buildDiagnosticTestPlan } from "./diagnostics/flow-plan";
import type { NativeCombinedReport, NativeInternetTransferReport } from "./types/deep-probe";
import type {
  DiagnosticResult,
  LatencySummary,
  LoadedLatencySummary,
  ThroughputSummary,
} from "./types/diagnostics";

function nativeLatency(latency: LatencySummary) {
  return {
    sent: latency.sent,
    received: latency.received,
    lost: latency.lost,
    lossPercent: latency.lossPercent,
    minimumMs: latency.minMs ?? undefined,
    maximumMs: latency.maxMs ?? undefined,
    meanMs: latency.meanMs ?? undefined,
    medianMs: latency.medianMs ?? undefined,
    p95Ms: latency.p95Ms ?? undefined,
    jitterMs: latency.jitterMs ?? undefined,
    samples: latency.samples,
  };
}

function nativeLoadedLatency(latency: LoadedLatencySummary) {
  return {
    statistics: nativeLatency(latency),
    increaseMs: latency.increaseMs ?? undefined,
    grade: latency.grade,
  };
}

function nativeThroughput(throughput: ThroughputSummary) {
  return {
    mbps: throughput.mbps,
    steadyMbps: throughput.steadyMbps,
    bytes: throughput.bytes,
    durationMs: throughput.durationMs,
    peakMbps: throughput.peakMbps,
    stabilityPercent: throughput.stabilityPercent,
    rampRatio: throughput.rampRatio ?? undefined,
    capReached: throughput.capReached,
    qualification: throughput.qualification,
    timeline: throughput.timeline.map((point) => ({
      elapsedMs: point.elapsedMs,
      mbps: point.value,
    })),
    aggregation: throughput.aggregation ?? "single",
    samples: (throughput.samples ?? []).map((sample) => ({
      ...sample,
      rampRatio: sample.rampRatio ?? undefined,
    })),
  };
}

function internetTransfer(result: DiagnosticResult): NativeInternetTransferReport {
  return {
    origin: result.measurement?.selectedEndpoint.origin ?? "https://network.johnnyli.dev/",
    idleLatency: nativeLatency(result.idleLatency),
    download: nativeThroughput(result.download),
    upload: nativeThroughput(result.upload),
    downloadLatency: nativeLoadedLatency(result.downloadLatency),
    uploadLatency: nativeLoadedLatency(result.uploadLatency),
    flowMeasurements: (result.flowMeasurements ?? []).map((measurement) => ({
      strategy: measurement.strategy,
      connections: measurement.concurrency,
      download: measurement.download ? nativeThroughput(measurement.download) : undefined,
      upload: measurement.upload ? nativeThroughput(measurement.upload) : undefined,
      downloadLatency: measurement.downloadLatency ? nativeLoadedLatency(measurement.downloadLatency) : undefined,
      uploadLatency: measurement.uploadLatency ? nativeLoadedLatency(measurement.uploadLatency) : undefined,
    })),
    downloadScaling: (result.downloadScaling ?? []).map((point) => ({
      connections: point.concurrency,
      download: nativeThroughput(point.download),
      downloadLatency: nativeLoadedLatency(point.downloadLatency),
    })),
    dataUsedBytes: result.dataUsedBytes,
  };
}

export function toSchemaTwoBrowserReport(result: DiagnosticResult): NativeCombinedReport {
  const config = TEST_MODES[result.mode];
  const transferMode = result.transferMode ?? "compare";
  const flowMeasurements = result.flowMeasurements ?? [];
  const downloadScaling = result.downloadScaling ?? [];
  const plan = buildDiagnosticTestPlan(config, transferMode);
  const estimatedSeconds = Number.parseInt(plan.estimatedTime, 10);
  const stage = (direction: "download" | "upload") =>
    (item: (typeof plan.downloads)[number]) => ({
      id: item.id,
      strategy: item.strategy,
      direction,
      connections: item.concurrency,
      durationMs: item.durationMs,
      capBytes: item.capBytes,
      samples: item.samples,
    });

  return {
    schemaVersion: "2.0",
    generatedAt: result.completedAt,
    producer: {
      application: "web",
      version: null,
      engine: "network-diagnostics-web",
    },
    run: {
      id: result.id,
      platform: globalThis.navigator?.platform || "browser",
      architecture: null,
      profile: result.mode,
      transferMethod: transferMode,
      startedAt: result.startedAt,
      completedAt: result.completedAt,
      includesLocalAddresses: false,
    },
    transferPlan: {
      profile: result.mode,
      method: transferMode,
      profileName: config.name,
      estimatedSeconds: Number.isFinite(estimatedSeconds) ? estimatedSeconds : 0,
      transferCapBytes: plan.transferCapBytes,
      includeServices: config.includeServices,
      downloadStages: plan.downloads.map(stage("download")),
      uploadStages: plan.uploads.map(stage("upload")),
    },
    internetTransfer: internetTransfer(result),
    deepDiagnostics: null,
    localLink: null,
    measurement: result.measurement ?? {
      contractVersion: "1.0",
      engine: "network-diagnostics-web",
      engineVersion: "browser",
      capabilities: [
        "browser-http-latency",
        "download-throughput",
        "upload-throughput",
        "loaded-latency",
        ...(flowMeasurements.length > 1 ? ["flow-comparison"] : []),
        ...(downloadScaling.length > 0 ? ["connection-scaling"] : []),
        ...(result.services.length > 0 ? ["service-reachability"] : []),
      ],
      selectedEndpoint: {
        id: "website-edge",
        name: result.edge?.edge ?? "Website edge",
        provider: "Cloudflare",
        origin: "https://network.johnnyli.dev/",
        selectionReason: "selected-by-browser",
        preflightLatencyMs: null,
      },
      endpointCandidates: [],
    },
    findings: result.findings ?? [],
    browserEvidence: {
      edge: result.edge
        ? {
            edge: result.edge.edge,
            network: result.edge.network,
            asn: result.edge.asn,
            protocol: result.edge.protocol,
            tlsVersion: result.edge.tlsVersion,
            ipVersion: result.edge.ipVersion,
          }
        : null,
      serviceChecks: result.services.map((service) => ({
        id: service.id,
        name: service.name,
        reachable: service.reachable,
        durationMs: service.durationMs,
        note: service.note,
      })),
    },
  };
}

export function serializeBrowserReport(result: DiagnosticResult): string {
  return JSON.stringify(toSchemaTwoBrowserReport(result), null, 2);
}

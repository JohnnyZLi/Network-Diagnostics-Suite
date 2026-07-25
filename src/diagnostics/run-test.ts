import { summarizeLatency, summarizeLoadedLatency } from "../core/statistics";
import type {
  DiagnosticResult,
  DownloadDeliverySummary,
  DownloadPathPreference,
  LatencySummary,
  TestMode,
  TestProgress,
  ThroughputQualification,
  ThroughputSampleSummary,
  ThroughputSummary
} from "../types/diagnostics";
import { TEST_MODES } from "./config";
import { fetchMetadata, TestCancelledError, throwIfAborted } from "./http";
import { collectLatencySamples, collectLatencyUntilStopped } from "./latency";
import { runServiceBattery } from "./services";
import { runDownload, runUpload } from "./throughput";

interface RunTestOptions {
  mode: TestMode;
  downloadPath: DownloadPathPreference;
  signal: AbortSignal;
  onProgress: (progress: TestProgress) => void;
}

const EDGE_SERVED_STATUSES = new Set(["HIT", "REVALIDATED", "STALE", "UPDATING"]);
const QUALIFICATION_RANK: Record<ThroughputQualification, number> = {
  qualified: 0,
  unstable: 1,
  "still-ramping": 2,
  declining: 3,
  "cap-limited": 4
};

function median(values: number[]): number {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((left, right) => left - right);
  const middle = Math.floor(sorted.length / 2);
  if (sorted.length % 2 === 1) return sorted[middle] ?? 0;
  return ((sorted[middle - 1] ?? 0) + (sorted[middle] ?? 0)) / 2;
}

function combineDownloadDelivery(samples: ThroughputSummary[]): DownloadDeliverySummary | undefined {
  const deliveries = samples
    .map((sample) => sample.delivery)
    .filter((delivery): delivery is DownloadDeliverySummary => delivery !== undefined);
  const first = deliveries[0];
  if (!first) return undefined;

  const cacheStatusCounts: Record<string, number> = {};
  const generationMap = new Map<number, { requests: number; bytes: number }>();
  const fallbackReasons = new Set<string>();
  const protocols = new Set<string>();
  let maxAgeSeconds: number | null = null;

  for (const delivery of deliveries) {
    for (const [status, count] of Object.entries(delivery.cacheStatusCounts)) {
      cacheStatusCounts[status] = (cacheStatusCounts[status] ?? 0) + count;
    }
    for (const protocol of delivery.protocols) protocols.add(protocol);
    for (const generation of delivery.requestGenerations) {
      const current = generationMap.get(generation.generation) ?? { requests: 0, bytes: 0 };
      current.requests += generation.requests;
      current.bytes += generation.bytes;
      generationMap.set(generation.generation, current);
    }
    if (delivery.pathFallbackReason) fallbackReasons.add(delivery.pathFallbackReason);
    if (delivery.maxAgeSeconds !== null) {
      maxAgeSeconds = maxAgeSeconds === null
        ? delivery.maxAgeSeconds
        : Math.max(maxAgeSeconds, delivery.maxAgeSeconds);
    }
  }

  const knownStatuses = Object.values(cacheStatusCounts).reduce((sum, count) => sum + count, 0);
  const edgeServed = Object.entries(cacheStatusCounts)
    .filter(([status]) => EDGE_SERVED_STATUSES.has(status))
    .reduce((sum, [, count]) => sum + count, 0);
  const selectedPath = deliveries.every((delivery) => delivery.selectedPath === first.selectedPath)
    ? first.selectedPath
    : "worker-stream-v4";
  const r2ProbeStatus = deliveries.some((delivery) => delivery.r2ProbeStatus === "unavailable")
    ? "unavailable"
    : deliveries.some((delivery) => delivery.r2ProbeStatus === "available")
      ? "available"
      : "not-requested";

  return {
    ...first,
    selectedPath,
    pathFallbackReason: fallbackReasons.size > 0 ? [...fallbackReasons].join(" ") : null,
    r2ProbeStatus,
    r2Requests: deliveries.reduce((sum, delivery) => sum + delivery.r2Requests, 0),
    staticRequests: deliveries.reduce((sum, delivery) => sum + delivery.staticRequests, 0),
    workerFallbackRequests: deliveries.reduce((sum, delivery) => sum + delivery.workerFallbackRequests, 0),
    rejectedStaticRequests: deliveries.reduce((sum, delivery) => sum + delivery.rejectedStaticRequests, 0),
    streamRejections: deliveries.flatMap((delivery) => delivery.streamRejections),
    cacheStatusCounts,
    edgeCacheServedPercent: knownStatuses === 0 ? null : (edgeServed / knownStatuses) * 100,
    maxAgeSeconds,
    protocols: [...protocols].sort(),
    logicalStreamBytes: Math.max(...deliveries.map((delivery) => delivery.logicalStreamBytes)),
    startedRequests: deliveries.reduce((sum, delivery) => sum + delivery.startedRequests, 0),
    completedRequests: deliveries.reduce((sum, delivery) => sum + delivery.completedRequests, 0),
    replacementRequests: deliveries.reduce((sum, delivery) => sum + delivery.replacementRequests, 0),
    interruptedRequests: deliveries.reduce((sum, delivery) => sum + delivery.interruptedRequests, 0),
    requestGenerations: [...generationMap.entries()]
      .sort(([left], [right]) => left - right)
      .map(([generation, values]) => ({ generation, ...values })),
    warmupBytes: deliveries.reduce((sum, delivery) => sum + delivery.warmupBytes, 0),
    warmupCachedBytes: Math.max(...deliveries.map((delivery) => delivery.warmupCachedBytes)),
    warmupSource: first.warmupSource,
    warmupCacheStatus: deliveries.map((delivery) => delivery.warmupCacheStatus).find((status) => status !== null) ?? null
  };
}

function sampleSummary(sample: ThroughputSummary, index: number): ThroughputSampleSummary {
  return {
    sample: index + 1,
    mbps: sample.mbps,
    steadyMbps: sample.steadyMbps,
    bytes: sample.bytes,
    durationMs: sample.durationMs,
    peakMbps: sample.peakMbps,
    stabilityPercent: sample.stabilityPercent,
    rampRatio: sample.rampRatio,
    capReached: sample.capReached,
    qualification: sample.qualification
  };
}

function aggregateDownloadSamples(samples: ThroughputSummary[]): ThroughputSummary {
  if (samples.length === 1) {
    return { ...samples[0], aggregation: "single", samples: [sampleSummary(samples[0], 0)] };
  }

  let elapsedOffset = 0;
  const timeline = samples.flatMap((sample) => {
    const shifted = sample.timeline.map((point) => ({ ...point, elapsedMs: point.elapsedMs + elapsedOffset }));
    elapsedOffset += sample.durationMs;
    return shifted;
  });
  const rampRatios = samples
    .map((sample) => sample.rampRatio)
    .filter((value): value is number => value !== null && Number.isFinite(value));
  const qualification = samples.reduce<ThroughputQualification>((worst, sample) => (
    QUALIFICATION_RANK[sample.qualification] > QUALIFICATION_RANK[worst] ? sample.qualification : worst
  ), "qualified");

  return {
    mbps: median(samples.map((sample) => sample.mbps)),
    steadyMbps: median(samples.map((sample) => sample.steadyMbps)),
    bytes: samples.reduce((sum, sample) => sum + sample.bytes, 0),
    durationMs: samples.reduce((sum, sample) => sum + sample.durationMs, 0),
    peakMbps: Math.max(...samples.map((sample) => sample.peakMbps)),
    stabilityPercent: median(samples.map((sample) => sample.stabilityPercent)),
    rampRatio: rampRatios.length > 0 ? median(rampRatios) : null,
    capReached: samples.some((sample) => sample.capReached),
    qualification,
    timeline,
    aggregation: "median",
    samples: samples.map(sampleSummary),
    delivery: combineDownloadDelivery(samples)
  };
}

async function runLoadedPhase(
  kind: "download" | "upload",
  idleLatency: LatencySummary,
  options: RunTestOptions,
  baseFraction: number,
  fractionSpan: number
): Promise<{ throughput: ThroughputSummary; latency: ReturnType<typeof summarizeLoadedLatency> }> {
  const config = TEST_MODES[options.mode];
  const phaseController = new AbortController();
  const forwardAbort = () => phaseController.abort(options.signal.reason);
  options.signal.addEventListener("abort", forwardAbort, { once: true });
  const latencyPromise = collectLatencyUntilStopped(phaseController.signal, 225, (sample) => {
    options.onProgress({
      phase: kind,
      fraction: baseFraction,
      liveLatencyMs: sample ?? undefined,
      bytesTransferred: 0
    });
  });

  let throughput: ThroughputSummary;
  try {
    if (kind === "download") {
      const sampleCount = Math.max(1, config.downloadSamples);
      const samples: ThroughputSummary[] = [];
      let completedBytes = 0;
      for (let index = 0; index < sampleCount; index += 1) {
        const durationMs = index === sampleCount - 1
          ? config.downloadDurationMs - Math.floor(config.downloadDurationMs / sampleCount) * (sampleCount - 1)
          : Math.floor(config.downloadDurationMs / sampleCount);
        const capBytes = index === sampleCount - 1
          ? config.downloadCapBytes - Math.floor(config.downloadCapBytes / sampleCount) * (sampleCount - 1)
          : Math.floor(config.downloadCapBytes / sampleCount);
        const sample = await runDownload({
          durationMs,
          capBytes,
          concurrency: config.concurrency,
          signal: options.signal,
          downloadPath: options.downloadPath,
          onProgress: (liveMbps, bytesTransferred) => {
            const sampleFraction = Math.min(1, bytesTransferred / Math.max(capBytes, 1));
            options.onProgress({
              phase: kind,
              fraction: baseFraction + ((index + sampleFraction) / sampleCount) * fractionSpan,
              liveMbps,
              bytesTransferred: completedBytes + bytesTransferred
            });
          }
        });
        samples.push(sample);
        completedBytes += sample.bytes;
        options.onProgress({
          phase: kind,
          fraction: baseFraction + ((index + 1) / sampleCount) * fractionSpan,
          liveMbps: sample.steadyMbps,
          bytesTransferred: completedBytes
        });
      }
      throughput = aggregateDownloadSamples(samples);
    } else {
      throughput = await runUpload({
        durationMs: config.uploadDurationMs,
        capBytes: config.uploadCapBytes,
        concurrency: config.uploadConcurrency,
        signal: options.signal,
        onProgress: (liveMbps, bytesTransferred) => {
          const elapsedFraction = Math.min(1, bytesTransferred / Math.max(config.uploadCapBytes, 1));
          options.onProgress({
            phase: kind,
            fraction: baseFraction + elapsedFraction * fractionSpan,
            liveMbps,
            bytesTransferred
          });
        }
      });
    }
  } finally {
    phaseController.abort("transfer-complete");
    options.signal.removeEventListener("abort", forwardAbort);
  }
  const latencySamples = await latencyPromise;
  return {
    throughput,
    latency: summarizeLoadedLatency(latencySamples, idleLatency.medianMs)
  };
}

export async function runDiagnosticTest(options: RunTestOptions): Promise<DiagnosticResult> {
  const config = TEST_MODES[options.mode];
  const startedAt = new Date();
  throwIfAborted(options.signal);

  // Metadata is useful context, but it must never make the measurement fail or
  // leave a rejected background promise when the user cancels midway through.
  const metadataPromise = fetchMetadata(options.signal).catch(() => null);
  let idleSamplesSoFar = 0;
  const idleSamples = await collectLatencySamples(
    config.idlePingCount,
    config.pingIntervalMs,
    options.signal,
    (sample) => {
      const completed = idleSamplesSoFar += 1;
      options.onProgress({
        phase: "idle",
        fraction: (completed / config.idlePingCount) * 0.15,
        liveLatencyMs: sample ?? undefined,
        bytesTransferred: 0
      });
    }
  );
  const idleLatency = summarizeLatency(idleSamples);

  const download = await runLoadedPhase("download", idleLatency, options, 0.15, 0.35);
  const upload = await runLoadedPhase("upload", idleLatency, options, 0.5, 0.35);

  let services: DiagnosticResult["services"] = [];
  if (config.includeServices) {
    options.onProgress({ phase: "services", fraction: 0.88, bytesTransferred: 0 });
    services = await runServiceBattery(options.signal);
  }

  throwIfAborted(options.signal);
  const edge = await metadataPromise;
  const completedAt = new Date();
  const downloadWarmupBytes = download.throughput.delivery?.warmupBytes ?? 0;
  const dataUsedBytes = download.throughput.bytes + upload.throughput.bytes + downloadWarmupBytes;
  options.onProgress({
    phase: "complete",
    fraction: 1,
    bytesTransferred: dataUsedBytes
  });

  return {
    id: crypto.randomUUID(),
    startedAt: startedAt.toISOString(),
    completedAt: completedAt.toISOString(),
    mode: options.mode,
    edge,
    idleLatency,
    download: download.throughput,
    upload: upload.throughput,
    downloadLatency: download.latency,
    uploadLatency: upload.latency,
    services,
    dataUsedBytes
  };
}

export { TestCancelledError };

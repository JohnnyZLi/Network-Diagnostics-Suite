import { throughputFromTimeline } from "../core/statistics";
import type {
  DownloadDeliverySummary,
  DownloadImplementation,
  DownloadPathPreference,
  DownloadRequestGenerationSummary,
  DownloadStreamRejection,
  ThroughputSummary,
  TimedSample,
  UploadRequestGenerationSummary
} from "../types/diagnostics";
import {
  downloadLongStream,
  downloadR2Range,
  type DownloadDeliveryObservation,
  type DownloadWarmupObservation,
  probeR2DownloadPath,
  R2_DOWNLOAD_OBJECT_BYTES,
  R2_DOWNLOAD_OBJECT_PATH,
  R2_DOWNLOAD_ORIGIN,
  R2_DOWNLOAD_RANGE_BYTES,
  sleep,
  STATIC_DOWNLOAD_PATH_PREFIX,
  STATIC_DOWNLOAD_STREAM_BYTES,
  TestCancelledError,
  throwIfAborted,
  uploadChunk,
  warmDownloadPath
} from "./http";

interface ThroughputOptions {
  durationMs: number;
  capBytes: number;
  concurrency: number;
  signal: AbortSignal;
  downloadPath?: DownloadPathPreference;
  onProgress?: (mbps: number, bytes: number) => void;
}

interface TransferState {
  bytes: number;
}

interface UploadTransferState extends TransferState {
  claimedBytes: number;
}

interface MutableGenerationSummary {
  requests: number;
  bytes: number;
}

interface SelectedDownloadPath {
  requestedPath: DownloadPathPreference;
  selectedPath: DownloadImplementation;
  warmup: DownloadWarmupObservation;
  r2ProbeStatus: "available" | "unavailable" | "not-requested";
  fallbackReason: string | null;
}

const EDGE_SERVED_STATUSES = new Set(["HIT", "REVALIDATED", "STALE", "UPDATING"]);
const UPLOAD_REQUEST_BYTES = 16 * 1024 * 1024;
const STRESS_UPLOAD_REQUEST_BYTES = 32 * 1024 * 1024;
const UPLOAD_INITIAL_STAGGER_MS = 40;

export function uploadRequestBytesForDuration(durationMs: number): number {
  return durationMs >= 20_000 ? STRESS_UPLOAD_REQUEST_BYTES : UPLOAD_REQUEST_BYTES;
}

function createPhaseSignal(parent: AbortSignal, durationMs: number): {
  signal: AbortSignal;
  durationReached: () => boolean;
  capReached: () => boolean;
  reachCap: () => void;
  stop: () => void;
} {
  const controller = new AbortController();
  let timedOut = false;
  let capped = false;
  const timer = window.setTimeout(() => {
    timedOut = true;
    controller.abort("duration-complete");
  }, durationMs);
  const onAbort = () => controller.abort(parent.reason);
  parent.addEventListener("abort", onAbort, { once: true });
  return {
    signal: controller.signal,
    durationReached: () => timedOut,
    capReached: () => capped,
    reachCap: () => {
      if (controller.signal.aborted) return;
      capped = true;
      controller.abort("cap-reached");
    },
    stop: () => {
      window.clearTimeout(timer);
      parent.removeEventListener("abort", onAbort);
      if (!controller.signal.aborted) controller.abort("phase-complete");
    }
  };
}

function startTimeline(
  state: TransferState,
  startedAt: number,
  onProgress?: (mbps: number, bytes: number) => void
): { timeline: TimedSample[]; stop: () => void } {
  const timeline: TimedSample[] = [];
  let lastBytes = 0;
  let lastTime = startedAt;
  const capture = () => {
    const now = performance.now();
    const elapsedMs = now - startedAt;
    const intervalSeconds = Math.max((now - lastTime) / 1000, 0.001);
    const intervalBytes = state.bytes - lastBytes;
    const mbps = (intervalBytes * 8) / intervalSeconds / 1_000_000;
    timeline.push({ elapsedMs, value: mbps });
    lastBytes = state.bytes;
    lastTime = now;
    onProgress?.(mbps, state.bytes);
  };
  const timer = window.setInterval(capture, 250);
  return {
    timeline,
    stop: () => {
      window.clearInterval(timer);
      if (state.bytes !== lastBytes) capture();
    }
  };
}

function collectDownloadProtocols(startedAt: number): string[] {
  if (typeof window === "undefined" || typeof performance.getEntriesByType !== "function") return [];

  const protocols = new Set<string>();
  for (const entry of performance.getEntriesByType("resource") as PerformanceResourceTiming[]) {
    if (entry.startTime < startedAt || !entry.nextHopProtocol) continue;
    try {
      const url = new URL(entry.name, window.location.href);
      const isWorkerPath = url.origin === window.location.origin && url.pathname.startsWith(STATIC_DOWNLOAD_PATH_PREFIX);
      const isR2Path = url.origin === R2_DOWNLOAD_ORIGIN && url.pathname === R2_DOWNLOAD_OBJECT_PATH;
      if (isWorkerPath || isR2Path) protocols.add(entry.nextHopProtocol);
    } catch {
      // Ignore malformed resource timing names supplied by the browser.
    }
  }
  return [...protocols].sort();
}

async function waitForPhaseDelay(ms: number, signal: AbortSignal): Promise<boolean> {
  if (ms <= 0) return true;
  try {
    await sleep(ms, signal);
    return true;
  } catch (error) {
    if (signal.aborted) return false;
    throw error;
  }
}

async function selectDownloadPath(
  requestedPath: DownloadPathPreference,
  signal: AbortSignal,
  concurrency: number
): Promise<SelectedDownloadPath> {
  if (requestedPath === "worker-stream") {
    return {
      requestedPath,
      selectedPath: "worker-stream-v4",
      warmup: await warmDownloadPath(signal, concurrency),
      r2ProbeStatus: "not-requested",
      fallbackReason: null
    };
  }

  const probe = await probeR2DownloadPath(signal);
  if (probe.available) {
    return {
      requestedPath,
      selectedPath: "r2-direct-v1",
      warmup: probe.warmup,
      r2ProbeStatus: "available",
      fallbackReason: null
    };
  }

  return {
    requestedPath,
    selectedPath: "worker-stream-v4",
    warmup: await warmDownloadPath(signal, concurrency),
    r2ProbeStatus: "unavailable",
    fallbackReason: probe.reason
  };
}

export function summarizeDownloadDelivery(
  path: SelectedDownloadPath,
  observations: DownloadDeliveryObservation[],
  rejections: DownloadStreamRejection[],
  protocols: string[],
  requestCounts: {
    started: number;
    completed: number;
    replacements: number;
    generations: DownloadRequestGenerationSummary[];
  },
  runtimeFallbackReason: string | null
): DownloadDeliverySummary {
  const cacheStatusCounts: Record<string, number> = {};
  let knownCacheStatuses = 0;
  let edgeServed = 0;
  let maxAgeSeconds: number | null = null;

  for (const observation of observations) {
    if (observation.source === "worker") continue;
    if (observation.cacheStatus) {
      cacheStatusCounts[observation.cacheStatus] = (cacheStatusCounts[observation.cacheStatus] ?? 0) + 1;
      knownCacheStatuses += 1;
      if (EDGE_SERVED_STATUSES.has(observation.cacheStatus)) edgeServed += 1;
    }
    if (observation.ageSeconds !== null) {
      maxAgeSeconds = maxAgeSeconds === null
        ? observation.ageSeconds
        : Math.max(maxAgeSeconds, observation.ageSeconds);
    }
  }

  return {
    requestedPath: path.requestedPath,
    selectedPath: path.selectedPath,
    pathFallbackReason: runtimeFallbackReason ?? path.fallbackReason,
    r2Origin: R2_DOWNLOAD_ORIGIN,
    r2ObjectBytes: R2_DOWNLOAD_OBJECT_BYTES,
    r2RangeBytes: R2_DOWNLOAD_RANGE_BYTES,
    r2ProbeStatus: path.r2ProbeStatus,
    r2Requests: observations.filter((observation) => observation.source === "r2").length,
    staticRequests: observations.filter((observation) => observation.source === "static").length,
    workerFallbackRequests: observations.filter((observation) => observation.source === "worker").length,
    rejectedStaticRequests: rejections.length,
    streamRejections: rejections,
    cacheStatusCounts,
    edgeCacheServedPercent: knownCacheStatuses === 0 ? null : (edgeServed / knownCacheStatuses) * 100,
    maxAgeSeconds,
    protocols: [...new Set(protocols.filter(Boolean))].sort(),
    logicalStreamBytes: path.selectedPath === "r2-direct-v1" ? R2_DOWNLOAD_RANGE_BYTES : STATIC_DOWNLOAD_STREAM_BYTES,
    startedRequests: requestCounts.started,
    completedRequests: requestCounts.completed,
    replacementRequests: requestCounts.replacements,
    interruptedRequests: Math.max(0, requestCounts.started - requestCounts.completed),
    requestGenerations: requestCounts.generations,
    warmupBytes: path.warmup.bytes,
    warmupCachedBytes: path.warmup.cachedBytes,
    warmupSource: path.warmup.source,
    warmupCacheStatus: path.warmup.cacheStatus
  };
}

export async function runDownload(options: ThroughputOptions): Promise<ThroughputSummary> {
  throwIfAborted(options.signal);
  const transportStartedAt = performance.now();
  const path = await selectDownloadPath(options.downloadPath ?? "auto", options.signal, options.concurrency);
  throwIfAborted(options.signal);

  const startedAt = performance.now();
  const state: TransferState = { bytes: 0 };
  const phase = createPhaseSignal(options.signal, options.durationMs);
  const sampler = startTimeline(state, startedAt, options.onProgress);
  const observations: DownloadDeliveryObservation[] = [];
  const rejections: DownloadStreamRejection[] = [];
  const generationMap = new Map<number, MutableGenerationSummary>();
  let startedRequests = 0;
  let completedRequests = 0;
  let replacementRequests = 0;
  let runtimeFallbackReason: string | null = null;

  const addBytes = (generationSummary: MutableGenerationSummary, delta: number) => {
    state.bytes += delta;
    generationSummary.bytes += delta;
    if (state.bytes >= options.capBytes) phase.reachCap();
  };

  const worker = async (workerIndex: number) => {
    let generation = 0;
    while (!phase.signal.aborted) {
      const generationSummary = generationMap.get(generation) ?? { requests: 0, bytes: 0 };
      generationSummary.requests += 1;
      generationMap.set(generation, generationSummary);
      startedRequests += 1;
      if (generation > 0) replacementRequests += 1;

      try {
        if (path.selectedPath === "r2-direct-v1") {
          const completed = await downloadR2Range(
            workerIndex,
            generation,
            phase.signal,
            (delta) => addBytes(generationSummary, delta),
            (observation) => observations.push(observation),
            (rejection) => rejections.push(rejection)
          );
          if (!completed && !phase.signal.aborted) {
            runtimeFallbackReason ??= "At least one direct R2 range failed, so that worker used the Worker stream for the remainder of the request.";
            await downloadLongStream(
              phase.signal,
              (delta) => addBytes(generationSummary, delta),
              (observation) => observations.push(observation),
              (rejection) => rejections.push(rejection)
            );
          }
        } else {
          await downloadLongStream(
            phase.signal,
            (delta) => addBytes(generationSummary, delta),
            (observation) => observations.push(observation),
            (rejection) => rejections.push(rejection)
          );
        }
        completedRequests += 1;
      } catch (error) {
        if (phase.signal.aborted) break;
        throw error;
      }

      generation += 1;
    }
  };

  try {
    await Promise.all(Array.from({ length: options.concurrency }, (_, index) => worker(index)));
  } finally {
    phase.stop();
    sampler.stop();
  }
  if (options.signal.aborted) throw new TestCancelledError();
  const durationMs = performance.now() - startedAt;
  const summary = throughputFromTimeline(state.bytes, durationMs, sampler.timeline, {
    capReached: phase.capReached(),
    targetDurationMs: options.durationMs
  });
  const generations = [...generationMap.entries()]
    .sort(([left], [right]) => left - right)
    .map(([generation, values]) => ({ generation, ...values }));
  return {
    ...summary,
    delivery: summarizeDownloadDelivery(
      path,
      observations,
      rejections,
      collectDownloadProtocols(transportStartedAt),
      {
        started: startedRequests,
        completed: completedRequests,
        replacements: replacementRequests,
        generations
      },
      runtimeFallbackReason
    )
  };
}

export async function runUpload(options: ThroughputOptions): Promise<ThroughputSummary> {
  throwIfAborted(options.signal);
  const startedAt = performance.now();
  const requestBytes = uploadRequestBytesForDuration(options.durationMs);
  const state: UploadTransferState = { bytes: 0, claimedBytes: 0 };
  const phase = createPhaseSignal(options.signal, options.durationMs);
  const sampler = startTimeline(state, startedAt, options.onProgress);
  const generationMap = new Map<number, MutableGenerationSummary>();
  let startedRequests = 0;
  let completedRequests = 0;
  let replacementRequests = 0;

  const worker = async (workerIndex: number) => {
    if (!await waitForPhaseDelay(workerIndex * UPLOAD_INITIAL_STAGGER_MS, phase.signal)) return;

    let generation = 0;
    while (!phase.signal.aborted && state.claimedBytes < options.capBytes) {
      const size = Math.min(requestBytes, options.capBytes - state.claimedBytes);
      if (size <= 0) break;
      state.claimedBytes += size;

      const generationSummary = generationMap.get(generation) ?? { requests: 0, bytes: 0 };
      generationSummary.requests += 1;
      generationMap.set(generation, generationSummary);
      startedRequests += 1;
      if (generation > 0) replacementRequests += 1;

      try {
        await uploadChunk(size, phase.signal, (delta) => {
          state.bytes += delta;
          generationSummary.bytes += delta;
          if (state.bytes >= options.capBytes) phase.reachCap();
        });
        completedRequests += 1;
      } catch (error) {
        if (phase.signal.aborted) break;
        throw error;
      }

      generation += 1;
      const restartDelayMs = 20 + ((workerIndex * 17 + generation * 13) % 36);
      if (!await waitForPhaseDelay(restartDelayMs, phase.signal)) break;
    }
  };

  try {
    await Promise.all(Array.from({ length: options.concurrency }, (_, index) => worker(index)));
  } finally {
    phase.stop();
    sampler.stop();
  }
  if (options.signal.aborted) throw new TestCancelledError();
  const durationMs = performance.now() - startedAt;
  const generations: UploadRequestGenerationSummary[] = [...generationMap.entries()]
    .sort(([left], [right]) => left - right)
    .map(([generation, values]) => ({ generation, ...values }));
  return {
    ...throughputFromTimeline(state.bytes, durationMs, sampler.timeline, {
      capReached: phase.capReached(),
      targetDurationMs: options.durationMs
    }),
    uploadDelivery: {
      requestSizeBytes: requestBytes,
      initialStaggerMs: UPLOAD_INITIAL_STAGGER_MS,
      startedRequests,
      completedRequests,
      replacementRequests,
      interruptedRequests: Math.max(0, startedRequests - completedRequests),
      requestGenerations: generations
    }
  };
}

import { throughputFromTimeline } from "../core/statistics";
import type { DownloadDeliverySummary, ThroughputSummary, TimedSample } from "../types/diagnostics";
import {
  downloadChunk,
  type DownloadDeliveryObservation,
  type DownloadWarmupObservation,
  STATIC_DOWNLOAD_ASSET_BYTES,
  STATIC_DOWNLOAD_PATH_PREFIX,
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
  onProgress?: (mbps: number, bytes: number) => void;
}

interface TransferState {
  bytes: number;
  claimedBytes: number;
}

const EDGE_SERVED_STATUSES = new Set(["HIT", "REVALIDATED", "STALE", "UPDATING"]);

function createPhaseSignal(parent: AbortSignal, durationMs: number): {
  signal: AbortSignal;
  durationReached: () => boolean;
  stop: () => void;
} {
  const controller = new AbortController();
  let timedOut = false;
  const timer = window.setTimeout(() => {
    timedOut = true;
    controller.abort("duration-complete");
  }, durationMs);
  const onAbort = () => controller.abort(parent.reason);
  parent.addEventListener("abort", onAbort, { once: true });
  return {
    signal: controller.signal,
    durationReached: () => timedOut,
    stop: () => {
      window.clearTimeout(timer);
      parent.removeEventListener("abort", onAbort);
      controller.abort("phase-complete");
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
      if (url.origin === window.location.origin && url.pathname.startsWith(STATIC_DOWNLOAD_PATH_PREFIX)) {
        protocols.add(entry.nextHopProtocol);
      }
    } catch {
      // Ignore malformed resource timing names supplied by the browser.
    }
  }
  return [...protocols].sort();
}

export function summarizeDownloadDelivery(
  warmup: DownloadWarmupObservation,
  observations: DownloadDeliveryObservation[],
  protocols: string[]
): DownloadDeliverySummary {
  const cacheStatusCounts: Record<string, number> = {};
  let knownCacheStatuses = 0;
  let edgeServed = 0;
  let maxAgeSeconds: number | null = null;

  for (const observation of observations) {
    if (observation.source !== "static") continue;
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
    staticRequests: observations.filter((observation) => observation.source === "static").length,
    workerFallbackRequests: observations.filter((observation) => observation.source === "worker").length,
    cacheStatusCounts,
    edgeCacheServedPercent: knownCacheStatuses === 0 ? null : (edgeServed / knownCacheStatuses) * 100,
    maxAgeSeconds,
    protocols: [...new Set(protocols.filter(Boolean))].sort(),
    warmupBytes: warmup.bytes,
    warmupSource: warmup.source,
    warmupCacheStatus: warmup.cacheStatus
  };
}

export async function runDownload(options: ThroughputOptions): Promise<ThroughputSummary> {
  throwIfAborted(options.signal);
  const transportStartedAt = performance.now();
  const warmup = await warmDownloadPath(options.signal, options.concurrency);
  throwIfAborted(options.signal);

  const startedAt = performance.now();
  const state: TransferState = { bytes: 0, claimedBytes: 0 };
  const phase = createPhaseSignal(options.signal, options.durationMs);
  const sampler = startTimeline(state, startedAt, options.onProgress);
  const requestSize = STATIC_DOWNLOAD_ASSET_BYTES;
  const observations: DownloadDeliveryObservation[] = [];

  const worker = async () => {
    while (!phase.signal.aborted && state.claimedBytes < options.capBytes) {
      const size = Math.min(requestSize, options.capBytes - state.claimedBytes);
      state.claimedBytes += size;
      try {
        const observation = await downloadChunk(size, phase.signal, (delta) => {
          state.bytes += delta;
        });
        observations.push(observation);
      } catch (error) {
        if (phase.signal.aborted) break;
        throw error;
      }
    }
  };

  try {
    await Promise.all(Array.from({ length: options.concurrency }, worker));
  } finally {
    phase.stop();
    sampler.stop();
  }
  if (options.signal.aborted) throw new TestCancelledError();
  const durationMs = performance.now() - startedAt;
  const summary = throughputFromTimeline(state.bytes, durationMs, sampler.timeline, {
    capReached: state.claimedBytes >= options.capBytes && !phase.durationReached(),
    targetDurationMs: options.durationMs
  });
  return {
    ...summary,
    delivery: summarizeDownloadDelivery(warmup, observations, collectDownloadProtocols(transportStartedAt))
  };
}

export async function runUpload(options: ThroughputOptions): Promise<ThroughputSummary> {
  throwIfAborted(options.signal);
  const startedAt = performance.now();
  const state: TransferState = { bytes: 0, claimedBytes: 0 };
  const phase = createPhaseSignal(options.signal, options.durationMs);
  const sampler = startTimeline(state, startedAt, options.onProgress);
  const requestSize = 8 * 1024 * 1024;

  const worker = async () => {
    while (!phase.signal.aborted && state.claimedBytes < options.capBytes) {
      const size = Math.min(requestSize, options.capBytes - state.claimedBytes);
      state.claimedBytes += size;
      try {
        await uploadChunk(size, phase.signal, (delta) => {
          state.bytes += delta;
        });
      } catch (error) {
        if (phase.signal.aborted) break;
        throw error;
      }
    }
  };

  try {
    await Promise.all(Array.from({ length: options.concurrency }, worker));
  } finally {
    phase.stop();
    sampler.stop();
  }
  if (options.signal.aborted) throw new TestCancelledError();
  const durationMs = performance.now() - startedAt;
  return throughputFromTimeline(state.bytes, durationMs, sampler.timeline, {
    capReached: state.claimedBytes >= options.capBytes && !phase.durationReached(),
    targetDurationMs: options.durationMs
  });
}

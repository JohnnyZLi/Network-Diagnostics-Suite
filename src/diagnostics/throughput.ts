import { throughputFromTimeline } from "../core/statistics";
import type { ThroughputSummary, TimedSample } from "../types/diagnostics";
import { downloadChunk, TestCancelledError, throwIfAborted, uploadChunk } from "./http";

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

function createPhaseSignal(parent: AbortSignal, durationMs: number): {
  signal: AbortSignal;
  stop: () => void;
} {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort("duration-complete"), durationMs);
  const onAbort = () => controller.abort(parent.reason);
  parent.addEventListener("abort", onAbort, { once: true });
  return {
    signal: controller.signal,
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
      capture();
    }
  };
}

export async function runDownload(options: ThroughputOptions): Promise<ThroughputSummary> {
  throwIfAborted(options.signal);
  const startedAt = performance.now();
  const state: TransferState = { bytes: 0, claimedBytes: 0 };
  const phase = createPhaseSignal(options.signal, options.durationMs);
  const sampler = startTimeline(state, startedAt, options.onProgress);
  const requestSize = 12 * 1024 * 1024;

  const worker = async () => {
    while (!phase.signal.aborted && state.claimedBytes < options.capBytes) {
      const size = Math.min(requestSize, options.capBytes - state.claimedBytes);
      state.claimedBytes += size;
      try {
        await downloadChunk(size, phase.signal, (delta) => {
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
  return throughputFromTimeline(state.bytes, durationMs, sampler.timeline);
}

export async function runUpload(options: ThroughputOptions): Promise<ThroughputSummary> {
  throwIfAborted(options.signal);
  const startedAt = performance.now();
  const state: TransferState = { bytes: 0, claimedBytes: 0 };
  const phase = createPhaseSignal(options.signal, options.durationMs);
  const sampler = startTimeline(state, startedAt, options.onProgress);
  const requestSize = 4 * 1024 * 1024;

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
  return throughputFromTimeline(state.bytes, durationMs, sampler.timeline);
}

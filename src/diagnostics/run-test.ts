import { summarizeLatency, summarizeLoadedLatency } from "../core/statistics";
import type {
  DiagnosticResult,
  LatencySummary,
  TestMode,
  TestProgress,
  ThroughputSummary
} from "../types/diagnostics";
import { TEST_MODES } from "./config";
import { fetchMetadata, TestCancelledError, throwIfAborted } from "./http";
import { collectLatencySamples, collectLatencyUntilStopped } from "./latency";
import { runServiceBattery } from "./services";
import { runDownload, runUpload } from "./throughput";

interface RunTestOptions {
  mode: TestMode;
  signal: AbortSignal;
  onProgress: (progress: TestProgress) => void;
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

  const transfer = kind === "download" ? runDownload : runUpload;
  const durationMs = kind === "download" ? config.downloadDurationMs : config.uploadDurationMs;
  const capBytes = kind === "download" ? config.downloadCapBytes : config.uploadCapBytes;
  let throughput: ThroughputSummary;
  try {
    throughput = await transfer({
      durationMs,
      capBytes,
      concurrency: config.concurrency,
      signal: options.signal,
      onProgress: (liveMbps, bytesTransferred) => {
        const elapsedFraction = Math.min(1, bytesTransferred / Math.max(capBytes, 1));
        options.onProgress({
          phase: kind,
          fraction: baseFraction + elapsedFraction * fractionSpan,
          liveMbps,
          bytesTransferred
        });
      }
    });
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
  options.onProgress({
    phase: "complete",
    fraction: 1,
    bytesTransferred: download.throughput.bytes + upload.throughput.bytes
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
    dataUsedBytes: download.throughput.bytes + upload.throughput.bytes
  };
}

export { TestCancelledError };

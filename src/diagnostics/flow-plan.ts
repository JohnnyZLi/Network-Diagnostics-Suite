import type { TransferMode, TransferStrategy } from "../types/diagnostics";
import type { TestModeConfig } from "./config";

export interface ThroughputStagePlan {
  id: string;
  strategy: TransferStrategy;
  concurrency: number;
  durationMs: number;
  capBytes: number;
  samples: number;
}

export interface DiagnosticTestPlan {
  downloads: ThroughputStagePlan[];
  uploads: ThroughputStagePlan[];
  transferCapBytes: number;
  estimatedTime: string;
  downloadConnectionLabel: string;
  uploadConnectionLabel: string;
  sampleLabel: string;
  methodDescription: string;
}

function stage(
  id: string,
  strategy: TransferStrategy,
  concurrency: number,
  durationMs: number,
  capBytes: number,
  samples = 1
): ThroughputStagePlan {
  return { id, strategy, concurrency, durationMs, capBytes, samples };
}

function formatCountSeries(counts: number[]): string {
  if (counts.length === 1) return String(counts[0]);
  if (counts.length === 2) return `${counts[0]} and ${counts[1]}`;
  return `${counts.slice(0, -1).join(", ")}, and ${counts.at(-1)}`;
}

function transferConnectionLabel(counts: number[], direction: "download" | "upload"): string {
  if (counts.length === 1 && counts[0] === 1) return `1 ${direction}`;
  const noun = `${direction}s`;
  return counts.length === 1
    ? `${counts[0]} concurrent ${noun}`
    : `${formatCountSeries(counts)} concurrent ${noun}`;
}

function singlePlan(config: TestModeConfig): Pick<DiagnosticTestPlan, "downloads" | "uploads" | "downloadConnectionLabel" | "uploadConnectionLabel" | "sampleLabel" | "methodDescription"> {
  return {
    downloads: [stage("single", "single", 1, config.downloadDurationMs, config.downloadCapBytes, config.downloadSamples)],
    uploads: [stage("single", "single", 1, config.uploadDurationMs, config.uploadCapBytes)],
    downloadConnectionLabel: transferConnectionLabel([1], "download"),
    uploadConnectionLabel: transferConnectionLabel([1], "upload"),
    sampleLabel: `Median of ${config.downloadSamples} single-connection downloads`,
    methodDescription: "One isolated connection in each direction exposes single-flow behavior that parallel transfers can hide."
  };
}

function aggregatePlan(config: TestModeConfig): Pick<DiagnosticTestPlan, "downloads" | "uploads" | "downloadConnectionLabel" | "uploadConnectionLabel" | "sampleLabel" | "methodDescription"> {
  return {
    downloads: [stage("aggregate", "aggregate", config.concurrency, config.downloadDurationMs, config.downloadCapBytes, config.downloadSamples)],
    uploads: [stage("aggregate", "aggregate", config.uploadConcurrency, config.uploadDurationMs, config.uploadCapBytes)],
    downloadConnectionLabel: transferConnectionLabel([config.concurrency], "download"),
    uploadConnectionLabel: transferConnectionLabel([config.uploadConcurrency], "upload"),
    sampleLabel: `Median of ${config.downloadSamples} aggregate downloads`,
    methodDescription: "Parallel connections estimate the total application capacity available to several simultaneous transfers."
  };
}

function comparePlan(config: TestModeConfig): Pick<DiagnosticTestPlan, "downloads" | "uploads" | "downloadConnectionLabel" | "uploadConnectionLabel" | "sampleLabel" | "methodDescription"> {
  if (config.id === "extended") {
    const scaling = [
      stage("scale-1", "single", 1, 4_000, 400 * 1_000_000),
      stage("scale-2", "aggregate", 2, 4_000, 500 * 1_000_000),
      stage("scale-4", "aggregate", 4, 4_000, 600 * 1_000_000),
      stage("scale-8", "aggregate", 8, 4_000, 600 * 1_000_000),
      stage("scale-10", "aggregate", config.concurrency, 8_000, 900 * 1_000_000)
    ];
    return {
      downloads: scaling,
      uploads: [
        stage("single", "single", 1, config.comparisonSingleUploadDurationMs, config.comparisonSingleUploadCapBytes),
        stage(
          "aggregate",
          "aggregate",
          config.uploadConcurrency,
          config.uploadDurationMs,
          config.uploadCapBytes - config.comparisonSingleUploadCapBytes
        )
      ],
      downloadConnectionLabel: transferConnectionLabel([1, 2, 4, 8, config.concurrency], "download"),
      uploadConnectionLabel: transferConnectionLabel([1, config.uploadConcurrency], "upload"),
      sampleLabel: "Five-stage download scaling test",
      methodDescription: "Measures download capacity at 1, 2, 4, 8, and 10 concurrent connections, then compares single-connection and aggregate upload capacity."
    };
  }

  const aggregateDownloadCapBytes = config.downloadCapBytes - config.comparisonSingleDownloadCapBytes;
  const uploads = config.comparisonSingleUploadDurationMs > 0
    ? [
        stage("single", "single", 1, config.comparisonSingleUploadDurationMs, config.comparisonSingleUploadCapBytes),
        stage(
          "aggregate",
          "aggregate",
          config.uploadConcurrency,
          config.uploadDurationMs,
          config.uploadCapBytes - config.comparisonSingleUploadCapBytes
        )
      ]
    : [stage("aggregate", "aggregate", config.uploadConcurrency, config.uploadDurationMs, config.uploadCapBytes)];

  return {
    downloads: [
      stage("single", "single", 1, config.comparisonSingleDownloadDurationMs, config.comparisonSingleDownloadCapBytes),
      stage("aggregate", "aggregate", config.concurrency, config.downloadDurationMs, aggregateDownloadCapBytes, config.downloadSamples)
    ],
    uploads,
    downloadConnectionLabel: transferConnectionLabel([1, config.concurrency], "download"),
    uploadConnectionLabel: config.comparisonSingleUploadDurationMs > 0
      ? transferConnectionLabel([1, config.uploadConcurrency], "upload")
      : transferConnectionLabel([config.uploadConcurrency], "upload"),
    sampleLabel: `Single flow + median of ${config.downloadSamples} aggregate downloads`,
    methodDescription: config.id === "quick"
      ? "Compares one download connection with parallel capacity, then uses aggregate upload to keep the test compact."
      : "Compares one connection with parallel capacity in both directions, then checks common-service reachability."
  };
}

export function buildDiagnosticTestPlan(config: TestModeConfig, transferMode: TransferMode): DiagnosticTestPlan {
  const selected = transferMode === "single"
    ? singlePlan(config)
    : transferMode === "aggregate"
      ? aggregatePlan(config)
      : comparePlan(config);
  const transferCapBytes = [...selected.downloads, ...selected.uploads]
    .reduce((sum, current) => sum + current.capBytes, 0);
  const measuredMs = [...selected.downloads, ...selected.uploads]
    .reduce((sum, current) => sum + current.durationMs, 0);
  const idleMs = config.idlePingCount * config.pingIntervalMs;
  const overheadMs = 2_000 + (config.includeServices ? 5_000 : 0);
  const configuredSeconds = Number.parseInt(config.estimatedTime, 10);
  const roundedSeconds = Math.max(
    Number.isFinite(configuredSeconds) ? configuredSeconds : 0,
    5,
    Math.ceil((idleMs + measuredMs + overheadMs) / 5_000) * 5
  );

  return {
    ...selected,
    transferCapBytes,
    estimatedTime: `${roundedSeconds} seconds`
  };
}

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

function connectionSequence(counts: number[]): string {
  return counts.join(" + ");
}

function singlePlan(config: TestModeConfig): Pick<DiagnosticTestPlan, "downloads" | "uploads" | "downloadConnectionLabel" | "uploadConnectionLabel" | "sampleLabel" | "methodDescription"> {
  return {
    downloads: [stage("single", "single", 1, config.downloadDurationMs, config.downloadCapBytes, config.downloadSamples)],
    uploads: [stage("single", "single", 1, config.uploadDurationMs, config.uploadCapBytes)],
    downloadConnectionLabel: "1",
    uploadConnectionLabel: "1",
    sampleLabel: `${config.downloadSamples} single`,
    methodDescription: "Measures one connection in each direction."
  };
}

function aggregatePlan(config: TestModeConfig): Pick<DiagnosticTestPlan, "downloads" | "uploads" | "downloadConnectionLabel" | "uploadConnectionLabel" | "sampleLabel" | "methodDescription"> {
  return {
    downloads: [stage("aggregate", "aggregate", config.concurrency, config.downloadDurationMs, config.downloadCapBytes, config.downloadSamples)],
    uploads: [stage("aggregate", "aggregate", config.uploadConcurrency, config.uploadDurationMs, config.uploadCapBytes)],
    downloadConnectionLabel: String(config.concurrency),
    uploadConnectionLabel: String(config.uploadConcurrency),
    sampleLabel: `${config.downloadSamples} parallel`,
    methodDescription: "Measures total speed across parallel connections."
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
      downloadConnectionLabel: connectionSequence([1, 2, 4, 8, config.concurrency]),
      uploadConnectionLabel: connectionSequence([1, config.uploadConcurrency]),
      sampleLabel: "5 scaling stages",
      methodDescription: "Tests how download speed scales with more connections, then compares single and parallel upload."
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
    downloadConnectionLabel: connectionSequence([1, config.concurrency]),
    uploadConnectionLabel: config.comparisonSingleUploadDurationMs > 0
      ? connectionSequence([1, config.uploadConcurrency])
      : String(config.uploadConcurrency),
    sampleLabel: `1 single · ${config.downloadSamples} parallel`,
    methodDescription: config.id === "quick"
      ? "Compares single and parallel download speed, then tests parallel upload."
      : "Compares single and parallel speed in both directions."
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

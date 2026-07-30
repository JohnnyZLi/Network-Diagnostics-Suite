import type { TestMode } from "../types/diagnostics";

export interface TestModeConfig {
  id: TestMode;
  name: string;
  description: string;
  estimatedTime: string;
  idlePingCount: number;
  pingIntervalMs: number;
  downloadDurationMs: number;
  downloadCapBytes: number;
  downloadSamples: number;
  uploadDurationMs: number;
  uploadCapBytes: number;
  concurrency: number;
  uploadConcurrency: number;
  comparisonSingleDownloadDurationMs: number;
  comparisonSingleDownloadCapBytes: number;
  comparisonSingleUploadDurationMs: number;
  comparisonSingleUploadCapBytes: number;
  includeServices: boolean;
}

export const TEST_MODES: Record<TestMode, TestModeConfig> = {
  quick: {
    id: "quick",
    name: "Quick",
    description: "Core speed, latency, jitter, request loss, and loaded latency with an optional single-versus-aggregate comparison.",
    estimatedTime: "20 seconds",
    idlePingCount: 12,
    pingIntervalMs: 150,
    downloadDurationMs: 8_000,
    downloadCapBytes: 600 * 1_000_000,
    downloadSamples: 3,
    uploadDurationMs: 8_000,
    uploadCapBytes: 128 * 1_000_000,
    concurrency: 6,
    uploadConcurrency: 6,
    comparisonSingleDownloadDurationMs: 4_000,
    comparisonSingleDownloadCapBytes: 250 * 1_000_000,
    comparisonSingleUploadDurationMs: 0,
    comparisonSingleUploadCapBytes: 0,
    includeServices: false
  },
  standard: {
    id: "standard",
    name: "Full",
    description: "Longer transfer phases, single-versus-aggregate behavior, and common-service reachability for a broader view of everyday network quality.",
    estimatedTime: "35 seconds",
    idlePingCount: 20,
    pingIntervalMs: 175,
    downloadDurationMs: 12_000,
    downloadCapBytes: 900 * 1_000_000,
    downloadSamples: 3,
    uploadDurationMs: 12_000,
    uploadCapBytes: 256 * 1_000_000,
    concurrency: 8,
    uploadConcurrency: 8,
    comparisonSingleDownloadDurationMs: 6_000,
    comparisonSingleDownloadCapBytes: 400 * 1_000_000,
    comparisonSingleUploadDurationMs: 6_000,
    comparisonSingleUploadCapBytes: 96 * 1_000_000,
    includeServices: true
  },
  extended: {
    id: "extended",
    name: "Stress",
    description: "Sustained high-capacity testing with a 1 → 2 → 4 → 8 → 10 connection scaling curve in Compare mode.",
    estimatedTime: "60 seconds",
    idlePingCount: 30,
    pingIntervalMs: 175,
    downloadDurationMs: 20_000,
    downloadCapBytes: 3_000 * 1_000_000,
    downloadSamples: 3,
    uploadDurationMs: 20_000,
    uploadCapBytes: 512 * 1_000_000,
    concurrency: 10,
    uploadConcurrency: 8,
    comparisonSingleDownloadDurationMs: 4_000,
    comparisonSingleDownloadCapBytes: 400 * 1_000_000,
    comparisonSingleUploadDurationMs: 8_000,
    comparisonSingleUploadCapBytes: 128 * 1_000_000,
    includeServices: true
  }
};

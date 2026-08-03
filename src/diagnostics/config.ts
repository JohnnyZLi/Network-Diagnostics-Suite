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
    name: "Connection Check",
    description: "A lightweight application-layer check of content speed, responsiveness, request loss, and delay under load.",
    estimatedTime: "15 seconds",
    idlePingCount: 8,
    pingIntervalMs: 150,
    downloadDurationMs: 3_000,
    downloadCapBytes: 20 * 1_000_000,
    downloadSamples: 1,
    uploadDurationMs: 3_000,
    uploadCapBytes: 8 * 1_000_000,
    concurrency: 2,
    uploadConcurrency: 2,
    comparisonSingleDownloadDurationMs: 1_500,
    comparisonSingleDownloadCapBytes: 5 * 1_000_000,
    comparisonSingleUploadDurationMs: 0,
    comparisonSingleUploadCapBytes: 0,
    includeServices: false
  },
  standard: {
    id: "standard",
    name: "Full",
    description: "Peak transfer phases, single-versus-aggregate behavior, and common-service reachability for a full diagnostic.",
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
    comparisonSingleDownloadCapBytes: 250 * 1_000_000,
    comparisonSingleUploadDurationMs: 6_000,
    comparisonSingleUploadCapBytes: 64 * 1_000_000,
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

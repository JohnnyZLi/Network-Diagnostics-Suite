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
  uploadDurationMs: number;
  uploadCapBytes: number;
  concurrency: number;
  includeServices: boolean;
}

export const TEST_MODES: Record<TestMode, TestModeConfig> = {
  quick: {
    id: "quick",
    name: "Quick",
    description: "Core speed, latency, jitter, request loss, and loaded latency.",
    estimatedTime: "about 15 seconds",
    idlePingCount: 12,
    pingIntervalMs: 150,
    downloadDurationMs: 6_000,
    downloadCapBytes: 100 * 1_000_000,
    uploadDurationMs: 6_000,
    uploadCapBytes: 32 * 1_000_000,
    concurrency: 4,
    includeServices: false
  },
  standard: {
    id: "standard",
    name: "Full",
    description: "Longer throughput runs plus a common-service reachability battery.",
    estimatedTime: "about 30 seconds",
    idlePingCount: 20,
    pingIntervalMs: 175,
    downloadDurationMs: 10_000,
    downloadCapBytes: 300 * 1_000_000,
    uploadDurationMs: 10_000,
    uploadCapBytes: 96 * 1_000_000,
    concurrency: 6,
    includeServices: true
  },
  extended: {
    id: "extended",
    name: "Stress",
    description: "Higher data caps for fast connections and sustained-load behavior.",
    estimatedTime: "about 45 seconds",
    idlePingCount: 30,
    pingIntervalMs: 175,
    downloadDurationMs: 16_000,
    downloadCapBytes: 1_000 * 1_000_000,
    uploadDurationMs: 16_000,
    uploadCapBytes: 256 * 1_000_000,
    concurrency: 8,
    includeServices: true
  }
};

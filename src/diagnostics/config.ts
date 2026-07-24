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
    estimatedTime: "about 20 seconds",
    idlePingCount: 12,
    pingIntervalMs: 150,
    downloadDurationMs: 8_000,
    downloadCapBytes: 250 * 1_000_000,
    uploadDurationMs: 8_000,
    uploadCapBytes: 96 * 1_000_000,
    concurrency: 6,
    includeServices: false
  },
  standard: {
    id: "standard",
    name: "Full",
    description: "Longer throughput runs plus a common-service reachability battery.",
    estimatedTime: "about 35 seconds",
    idlePingCount: 20,
    pingIntervalMs: 175,
    downloadDurationMs: 12_000,
    downloadCapBytes: 600 * 1_000_000,
    uploadDurationMs: 12_000,
    uploadCapBytes: 192 * 1_000_000,
    concurrency: 8,
    includeServices: true
  },
  extended: {
    id: "extended",
    name: "Stress",
    description: "Higher data caps for fast connections and sustained-load behavior.",
    estimatedTime: "about 60 seconds",
    idlePingCount: 30,
    pingIntervalMs: 175,
    downloadDurationMs: 20_000,
    downloadCapBytes: 1_500 * 1_000_000,
    uploadDurationMs: 20_000,
    uploadCapBytes: 512 * 1_000_000,
    concurrency: 10,
    includeServices: true
  }
};

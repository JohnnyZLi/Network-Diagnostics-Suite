import type { EdgeMetadata } from "./api";

export type TestMode = "quick" | "standard" | "extended";
export type TestPhase = "idle" | "download" | "upload" | "services" | "complete";

export interface TimedSample {
  elapsedMs: number;
  value: number;
}

export interface LatencySummary {
  sent: number;
  received: number;
  lost: number;
  lossPercent: number;
  minMs: number | null;
  maxMs: number | null;
  meanMs: number | null;
  medianMs: number | null;
  p95Ms: number | null;
  jitterMs: number | null;
  samples: Array<number | null>;
}

export type ThroughputQualification = "qualified" | "cap-limited" | "still-ramping" | "unstable";

export interface ThroughputSummary {
  mbps: number;
  steadyMbps: number;
  bytes: number;
  durationMs: number;
  peakMbps: number;
  stabilityPercent: number;
  rampRatio: number | null;
  capReached: boolean;
  qualification: ThroughputQualification;
  timeline: TimedSample[];
}

export interface LoadedLatencySummary extends LatencySummary {
  increaseMs: number | null;
  grade: "A+" | "A" | "B" | "C" | "D" | "F" | "—";
}

export interface ServiceCheckResult {
  id: string;
  name: string;
  reachable: boolean;
  durationMs: number | null;
  note?: string;
}

export interface DiagnosticResult {
  id: string;
  startedAt: string;
  completedAt: string;
  mode: TestMode;
  edge: EdgeMetadata | null;
  idleLatency: LatencySummary;
  download: ThroughputSummary;
  upload: ThroughputSummary;
  downloadLatency: LoadedLatencySummary;
  uploadLatency: LoadedLatencySummary;
  services: ServiceCheckResult[];
  dataUsedBytes: number;
}

export interface TestProgress {
  phase: TestPhase;
  fraction: number;
  liveMbps?: number;
  liveLatencyMs?: number;
  bytesTransferred: number;
}

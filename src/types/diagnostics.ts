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

export type ThroughputQualification = "qualified" | "cap-limited" | "still-ramping" | "declining" | "unstable";

export interface DownloadRequestGenerationSummary {
  generation: number;
  requests: number;
  bytes: number;
}

export type DownloadStreamRejectionReason =
  | "fetch-error"
  | "status"
  | "missing-body"
  | "wrong-marker"
  | "wrong-logical-size"
  | "wrong-segment-count"
  | "wrong-content-length"
  | "truncated-body"
  | "stream-error";

export interface DownloadStreamRejection {
  reason: DownloadStreamRejectionReason;
  status: number;
  marker: string | null;
  logicalBytes: number | null;
  segmentCount: number | null;
  contentLength: number | null;
  receivedBytes: number | null;
}

export interface DownloadDeliverySummary {
  staticRequests: number;
  workerFallbackRequests: number;
  rejectedStaticRequests: number;
  streamRejections: DownloadStreamRejection[];
  cacheStatusCounts: Record<string, number>;
  edgeCacheServedPercent: number | null;
  maxAgeSeconds: number | null;
  protocols: string[];
  logicalStreamBytes: number;
  startedRequests: number;
  completedRequests: number;
  replacementRequests: number;
  interruptedRequests: number;
  requestGenerations: DownloadRequestGenerationSummary[];
  warmupBytes: number;
  warmupCachedBytes: number;
  warmupSource: "static" | "worker";
  warmupCacheStatus: string | null;
}

export interface UploadRequestGenerationSummary {
  generation: number;
  requests: number;
  bytes: number;
}

export interface UploadDeliverySummary {
  requestSizeBytes: number;
  initialStaggerMs: number;
  startedRequests: number;
  completedRequests: number;
  replacementRequests: number;
  interruptedRequests: number;
  requestGenerations: UploadRequestGenerationSummary[];
}

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
  delivery?: DownloadDeliverySummary;
  uploadDelivery?: UploadDeliverySummary;
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

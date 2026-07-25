import type { EdgeMetadata } from "./api";

export type TestMode = "quick" | "standard" | "extended";
export type TestPhase = "idle" | "download" | "upload" | "services" | "complete";
export type DownloadPathPreference = "auto" | "r2-direct" | "worker-stream";
export type DownloadImplementation = "r2-direct-v1" | "worker-stream-v4";

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
  | "stream-error"
  | "r2-fetch-error"
  | "r2-status"
  | "r2-missing-body"
  | "r2-wrong-size"
  | "r2-wrong-range"
  | "r2-truncated-body"
  | "r2-stream-error";

export interface DownloadStreamRejection {
  reason: DownloadStreamRejectionReason;
  status: number;
  marker: string | null;
  logicalBytes: number | null;
  segmentCount: number | null;
  contentLength: number | null;
  receivedBytes: number | null;
  contentRange?: string | null;
}

export interface DownloadDeliverySummary {
  requestedPath: DownloadPathPreference;
  selectedPath: DownloadImplementation;
  pathFallbackReason: string | null;
  r2Origin: string;
  r2ObjectBytes: number;
  r2RangeBytes: number;
  r2ProbeStatus: "available" | "unavailable" | "not-requested";
  r2Requests: number;
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
  warmupSource: "r2" | "static" | "worker";
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

export interface ThroughputSampleSummary {
  sample: number;
  mbps: number;
  steadyMbps: number;
  bytes: number;
  durationMs: number;
  peakMbps: number;
  stabilityPercent: number;
  rampRatio: number | null;
  capReached: boolean;
  qualification: ThroughputQualification;
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
  aggregation?: "single" | "median";
  samples?: ThroughputSampleSummary[];
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

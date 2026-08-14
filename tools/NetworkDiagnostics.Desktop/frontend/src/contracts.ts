export type AppearanceMode = 'system' | 'light' | 'dark';
export type TransferMethod = 'compare' | 'single' | 'aggregate';
export type DownloadPathPreference = 'automatic' | 'direct-r2' | 'worker';
export type DiagnosticProfile = 'connection-check' | 'quick' | 'full' | 'stress';
export type WorkbenchSection = 'live-network-health' | 'run-diagnostics' | 'advanced-diagnostics';
export type InterfaceChoice = Record<string, unknown>;

export type DiagnosticStage = {
  id: string;
  direction: 'download' | 'upload';
  strategy: 'single' | 'aggregate';
  connections: number;
  durationMs: number;
  capBytes: number;
  samples: number;
};

export type DiagnosticPlan = {
  profile: DiagnosticProfile;
  profileName: string;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  estimatedSeconds: number;
  internetEstimatedSeconds: number;
  transferCapBytes: number;
  internetTransferCapBytes: number;
  includeServices: boolean;
  serviceCheckCount: number;
  deepDiagnostics: boolean;
  diagnosticDepth: string;
  idlePingCount: number;
  pingIntervalMs: number;
  downloadStages: DiagnosticStage[];
  uploadStages: DiagnosticStage[];
  downloadRuns: number;
  maxDownloadConnections: number;
  maxUploadConnections: number;
  totalTransferStages: number;
  lanEnabled: boolean;
  lanTarget?: string | null;
  lanPort?: number | null;
  lanDurationSeconds?: number | null;
  lanConnections?: number | null;
  lanEstimatedSeconds: number;
};

export type DownloadDelivery = {
  requestedPath: string;
  selectedPath: string;
  r2ProbeStatus: string;
  fallbackReason?: string | null;
  r2Origin: string;
  bytes: number;
  requestsStarted: number;
  requestsCompleted: number;
  r2Requests: number;
  workerRequests: number;
};

export type SavedReportSummary = {
  id: string;
  generatedAt: string;
  storedAt: string;
  profile: string;
  profileName: string;
  label?: string | null;
  tags: string[];
  savedLocally?: boolean;
  outcome?: string | null;
  outcomeLabel?: string | null;
  latencyMs?: number | null;
  requestLossPercent?: number | null;
  downloadMbps?: number | null;
  uploadMbps?: number | null;
  dataUsedBytes?: number | null;
  requestedDownloadPath?: string | null;
  selectedDownloadPath?: string | null;
};

export type ReportPresentation = {
  outcome: string;
  label: string;
  verdict: string;
  summary: string;
  nextAction: string;
  metrics: Array<{ label: string; value: string; detail: string; wasMeasured: boolean }>;
  findings: Array<{ label: string; title: string; summary: string }>;
  technicalData?: string[];
  technicalEvidence?: string[];
};

export type LanThroughputReport = {
  target: string;
  resolvedAddress?: string | null;
  port: number;
  durationMs: number;
  concurrency: number;
  latency: { sent: number; received: number; lost: number; lossPercent: number; medianMs?: number | null; jitterMs?: number | null };
  downloadMbps: number;
  downloadBytes: number;
  uploadMbps: number;
  uploadBytes: number;
};

export type SavedReportDetail = {
  report: SavedReportSummary;
  context: string;
  method: string;
  downloadDelivery?: DownloadDelivery | null;
  measurement?: unknown;
  localLink?: LanThroughputReport | null;
  technicalReport?: unknown;
  presentation: ReportPresentation;
};

export type RunAccepted = {
  runId: string;
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  transferCapBytes: number;
  estimatedSeconds: number;
  totalStages: number;
};

export type DiagnosticProgress = {
  runId: string;
  phase: string;
  stage: string;
  stageLabel: string;
  message: string;
  fraction: number;
  overallFraction: number;
  stageIndex: number;
  totalStages: number;
  elapsedSeconds: number;
  estimatedSecondsRemaining?: number | null;
  liveMbps?: number | null;
  liveLatencyMs?: number | null;
  bytesTransferred: number;
  totalBytesTransferred: number;
};

export type DiagnosticResult = {
  runId: string;
  reportId: string;
  generatedAt: string;
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath?: DownloadPathPreference;
  downloadDelivery?: DownloadDelivery | null;
  latencyMs?: number | null;
  requestLossPercent?: number | null;
  downloadMbps?: number | null;
  uploadMbps?: number | null;
  dataUsedBytes?: number | null;
  savedLocally: boolean;
  storageError?: string | null;
  storedReport?: SavedReportSummary | null;
  detail: SavedReportDetail;
};

export type AdvancedSettings = {
  endpointCandidates: string[];
  interfaceId?: string | null;
  includeLocalIdentifiers: boolean;
  lanTarget?: string | null;
  lanPort: number;
  lanDurationSeconds: number;
  lanConnections: number;
};

export type PreflightResult = {
  measurement?: unknown;
  interfaces?: InterfaceChoice[];
  downloadPath?: {
    requestedPath: string;
    selectedPath: string;
    r2ProbeStatus: string;
    fallbackReason?: string | null;
    r2Origin?: string | null;
  };
};

export type MeasurementPathSummary = {
  endpoint: string;
  origin: string;
  providerNetwork: string;
  edge: string;
  latency: string;
  interface: string;
  protocol: string;
  ipVersion: string;
  tls: string;
  http3: string;
};

export type LanServerStatus = {
  running: boolean;
  port?: number | null;
  addresses: string[];
  clientRunning?: boolean;
};

export type AppSettings = {
  appearance: AppearanceMode;
  monitoringEnabled: boolean;
  monitoringWindow: string;
  monitoringIntervalSeconds: number;
  monitoringAlertScoreThreshold: number;
  expectedDownloadMbps: number;
  expectedUploadMbps: number;
  reportsDirectory?: string | null;
  effectiveReportsDirectory?: string | null;
  reportRetentionDays: number;
};

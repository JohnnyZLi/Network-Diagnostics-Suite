export interface DeepLatencyStatistics {
  sent: number;
  received: number;
  lost: number;
  lossPercent: number;
  minimumMs?: number;
  maximumMs?: number;
  meanMs?: number;
  medianMs?: number;
  p95Ms?: number;
  jitterMs?: number;
  samples: Array<number | null>;
}

export interface DeepPingTarget {
  label: string;
  address?: string;
  statistics: DeepLatencyStatistics;
}

export interface DeepTraceHop {
  hop: number;
  address?: string;
  hostname?: string;
  roundTripsMs: Array<number | null>;
  reachedDestination: boolean;
  addressRedacted?: boolean;
}

export interface DeepDnsResolver {
  name: string;
  address: string;
  attempts: number;
  successful: number;
  minimumMs?: number;
  medianMs?: number;
  p95Ms?: number;
  maximumMs?: number;
  error?: string;
}

export interface DeepTlsEndpoint {
  name: string;
  host: string;
  reachable: boolean;
  dnsMs?: number;
  tcpMs?: number;
  tlsMs?: number;
  tlsProtocol?: string;
  applicationProtocol?: string;
  error?: string;
}

export interface DeepNetworkInterface {
  name: string;
  description: string;
  type: string;
  linkSpeedMbps?: number;
  ipv4Mtu?: number;
  supportsIpv4: boolean;
  supportsIpv6: boolean;
  unicastAddresses?: string[];
  gateways?: string[];
  dnsServers?: string[];
}

export interface DeepLanThroughput {
  target: string;
  resolvedAddress?: string;
  port: number;
  durationMs: number;
  concurrency: number;
  latency: DeepLatencyStatistics;
  downloadMbps: number;
  downloadBytes: number;
  uploadMbps: number;
  uploadBytes: number;
}

export interface DeepWifiDetails {
  status: "available" | "not-connected" | "unavailable" | string;
  interfaceName?: string;
  ssid?: string;
  signalPercent?: number;
  rssiDbm?: number;
  channel?: number;
  band?: string;
  protocol?: string;
  receiveRateMbps?: number;
  transmitRateMbps?: number;
  security?: string;
  error?: string;
}

export interface DeepRouteEntry {
  destination: string;
  gateway?: string;
  interfaceName?: string;
  metric?: number;
  addressFamily: string;
  isDefault: boolean;
}

export interface DeepRoutingDetails {
  status: "available" | "unavailable" | string;
  entries: DeepRouteEntry[];
  error?: string;
}

export interface DeepProbeReport {
  schemaVersion: "1.0" | "1.1" | "1.2";
  generatedAt: string;
  target: string;
  operatingSystem: string;
  architecture: string;
  includesLocalAddresses: boolean;
  interfaces: DeepNetworkInterface[];
  gatewayPing?: DeepPingTarget;
  internetPing: DeepPingTarget;
  traceRoute: {
    target: string;
    resolvedAddress?: string;
    maximumHops: number;
    reachedDestination: boolean;
    hops: DeepTraceHop[];
  };
  dnsResolvers: DeepDnsResolver[];
  pathMtu: {
    target: string;
    payloadBytes?: number;
    estimatedIpv4Mtu?: number;
    status: string;
  };
  serviceEndpoints: DeepTlsEndpoint[];
  localLink?: DeepLanThroughput;
  wifi?: DeepWifiDetails;
  routing?: DeepRoutingDetails;
}

export interface NativeThroughputSummary {
  mbps: number;
  steadyMbps: number;
  bytes: number;
  durationMs: number;
  peakMbps: number;
  stabilityPercent: number;
  rampRatio?: number;
  capReached: boolean;
  qualification: string;
}

export interface NativeLoadedLatencyReport {
  statistics: DeepLatencyStatistics;
  increaseMs?: number;
  grade: string;
}

export interface NativeFlowMeasurement {
  strategy: "single" | "aggregate";
  connections: number;
  download?: NativeThroughputSummary;
  upload?: NativeThroughputSummary;
  downloadLatency?: NativeLoadedLatencyReport;
  uploadLatency?: NativeLoadedLatencyReport;
}

export interface NativeInternetTransferReport {
  origin: string;
  idleLatency: DeepLatencyStatistics;
  download: NativeThroughputSummary;
  upload: NativeThroughputSummary;
  downloadLatency: NativeLoadedLatencyReport;
  uploadLatency: NativeLoadedLatencyReport;
  flowMeasurements: NativeFlowMeasurement[];
  downloadScaling: Array<{
    connections: number;
    download: NativeThroughputSummary;
    downloadLatency: NativeLoadedLatencyReport;
  }>;
  dataUsedBytes: number;
}

export interface NativeCombinedReport {
  schemaVersion: "2.0";
  generatedAt: string;
  run: {
    id: string;
    platform: string;
    architecture: string;
    profile: "quick" | "standard" | "extended";
    transferMethod: "compare" | "single" | "aggregate";
    startedAt: string;
    completedAt: string;
    includesLocalAddresses: boolean;
  };
  transferPlan: {
    profile: "quick" | "standard" | "extended";
    method: "compare" | "single" | "aggregate";
    profileName: string;
    estimatedSeconds: number;
    transferCapBytes: number;
    includeServices: boolean;
  };
  internetTransfer?: NativeInternetTransferReport;
  deepDiagnostics: DeepProbeReport;
  localLink?: DeepLanThroughput;
}

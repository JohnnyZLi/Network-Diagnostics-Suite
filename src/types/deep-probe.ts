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

export interface DeepProbeReport {
  schemaVersion: "1.0";
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
}

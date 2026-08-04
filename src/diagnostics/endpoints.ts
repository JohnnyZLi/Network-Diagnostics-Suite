import type { MeasurementContext, MeasurementEndpointProbe } from "../types/diagnostics";

export interface MeasurementEndpointDefinition {
  id: string;
  name: string;
  provider: string;
  origin: string;
  r2Origin?: string;
  independentProvider?: boolean;
}

export interface SelectedWebEndpoint {
  endpoint: MeasurementEndpointDefinition;
  probes: MeasurementEndpointProbe[];
  medianLatencyMs: number | null;
  selectedAt: number;
}

export const PRIMARY_MEASUREMENT_ENDPOINT: MeasurementEndpointDefinition = {
  id: "cloudflare-primary",
  name: "Network Diagnostics primary",
  provider: "Cloudflare",
  origin: "",
  r2Origin: "https://speed.johnnyli.dev"
};

export function configuredMeasurementEndpoints(): MeasurementEndpointDefinition[] {
  const configured = (import.meta.env.VITE_NDS_ENDPOINTS as string | undefined)
    ?.split(/[\n,;]+/)
    .map((value) => value.trim())
    .filter(Boolean) ?? [];
  const endpoints: MeasurementEndpointDefinition[] = [PRIMARY_MEASUREMENT_ENDPOINT];
  for (const [index, origin] of configured.entries()) {
    try {
      const parsed = new URL(origin);
      endpoints.push({
        id: `configured-${index + 1}`,
        name: parsed.host,
        provider: "Configured",
        origin: parsed.toString()
      });
    } catch {
      // Ignore malformed deployment configuration rather than breaking the site.
    }
  }
  return endpoints
    .filter((endpoint, index, all) => all.findIndex((candidate) => endpointOrigin(candidate) === endpointOrigin(endpoint)) === index)
    .slice(0, 8);
}

export function endpointUrl(endpoint: MeasurementEndpointDefinition, path: string): string {
  if (!endpoint.origin) return path;
  return new URL(path.replace(/^\//, ""), endpoint.origin.endsWith("/") ? endpoint.origin : `${endpoint.origin}/`).toString();
}

export function endpointOrigin(endpoint: MeasurementEndpointDefinition): string {
  if (endpoint.origin) return new URL(endpoint.origin).origin;
  return typeof window === "undefined" ? "https://network.johnnyli.dev" : window.location.origin;
}

function median(values: number[]): number | null {
  if (values.length === 0) return null;
  const sorted = [...values].sort((left, right) => left - right);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1
    ? sorted[middle] ?? null
    : ((sorted[middle - 1] ?? 0) + (sorted[middle] ?? 0)) / 2;
}

async function probeEndpoint(
  endpoint: MeasurementEndpointDefinition,
  signal: AbortSignal
): Promise<MeasurementEndpointProbe> {
  const observations: number[] = [];
  let error: string | null = null;

  for (let attempt = 0; attempt < 2; attempt += 1) {
    const startedAt = performance.now();
    const requestController = new AbortController();
    const timeout = globalThis.setTimeout(() => requestController.abort("endpoint-preflight-timeout"), 1_800);
    const forwardAbort = () => requestController.abort(signal.reason);
    signal.addEventListener("abort", forwardAbort, { once: true });
    try {
      const response = await fetch(endpointUrl(endpoint, `/api/ping?n=${crypto.randomUUID()}`), {
        cache: "no-store",
        credentials: "omit",
        signal: requestController.signal
      });
      if (!response.ok) {
        error = `HTTP ${response.status}`;
        continue;
      }
      await response.text();
      observations.push(performance.now() - startedAt);
    } catch (cause) {
      if (signal.aborted) throw cause;
      error = requestController.signal.aborted
        ? "Timed out"
        : cause instanceof Error ? cause.message : "Endpoint probe failed.";
    } finally {
      globalThis.clearTimeout(timeout);
      signal.removeEventListener("abort", forwardAbort);
    }
  }

  const latency = median(observations);
  return {
    id: endpoint.id,
    name: endpoint.name,
    provider: endpoint.provider,
    origin: endpointOrigin(endpoint),
    available: latency !== null,
    medianLatencyMs: latency,
    error: latency === null ? error ?? "No probe response." : null
  };
}

export async function selectMeasurementEndpoint(
  endpoints: MeasurementEndpointDefinition[],
  signal: AbortSignal
): Promise<SelectedWebEndpoint> {
  if (endpoints.length === 0) throw new Error("At least one measurement endpoint is required.");
  const probes = await Promise.all(endpoints.map((endpoint) => probeEndpoint(endpoint, signal)));
  const available = probes
    .map((probe, index) => ({ probe, endpoint: endpoints[index] }))
    .filter((candidate): candidate is { probe: MeasurementEndpointProbe; endpoint: MeasurementEndpointDefinition } =>
      candidate.endpoint !== undefined && candidate.probe.available)
    .sort((left, right) => (left.probe.medianLatencyMs ?? Infinity) - (right.probe.medianLatencyMs ?? Infinity));
  const selected = available[0];
  if (!selected) throw new Error("No configured measurement endpoint answered the preflight check.");
  return {
    endpoint: selected.endpoint,
    probes,
    medianLatencyMs: selected.probe.medianLatencyMs,
    selectedAt: Date.now()
  };
}

export function createWebMeasurementContext(selection: SelectedWebEndpoint): MeasurementContext {
  const navigation = performance.getEntriesByType("navigation")[0] as PerformanceNavigationTiming | undefined;
  const observedProtocol = navigation?.nextHopProtocol || null;
  return {
    contractVersion: "1.1",
    engine: "network-diagnostics-web",
    engineVersion: "browser",
    capabilities: [
      "endpoint-preflight",
      "network-metadata",
      "application-latency",
      "content-throughput",
      "loaded-latency",
      "single-flow",
      "aggregate-flow",
      "service-reachability",
      "browser-protocol-observation"
    ],
    selectedEndpoint: {
      id: selection.endpoint.id,
      name: selection.endpoint.name,
      provider: selection.endpoint.provider,
      origin: endpointOrigin(selection.endpoint),
      selectionReason: selection.probes.length === 1
        ? "Only configured endpoint"
        : "Lowest median preflight latency",
      preflightLatencyMs: selection.medianLatencyMs
    },
    endpointCandidates: selection.probes,
    http3: {
      advertised: observedProtocol === "h3",
      observedProtocol,
      note: observedProtocol === "h3"
        ? "The browser reports HTTP/3 for the current page navigation."
        : "Browsers do not expose an exact-version QUIC probe API; this records the negotiated page protocol only."
    }
  };
}

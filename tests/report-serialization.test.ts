import { describe, expect, it, vi } from "vitest";
import { serializeBrowserReport, toSchemaTwoBrowserReport } from "../src/report-serialization";
import type { DiagnosticResult, LatencySummary, ThroughputResult } from "../src/types/diagnostics";

const latency = (medianMs: number): LatencySummary => ({
  sent: 8,
  received: 8,
  lost: 0,
  lossPercent: 0,
  minMs: medianMs - 2,
  maxMs: medianMs + 4,
  meanMs: medianMs,
  medianMs,
  p95Ms: medianMs + 3,
  jitterMs: 2,
  samples: [medianMs - 2, medianMs - 1, medianMs, medianMs, medianMs + 1, medianMs + 2, medianMs + 3, medianMs + 4],
});

const throughput = (steadyMbps: number): ThroughputResult => ({
  mbps: steadyMbps + 5,
  steadyMbps,
  bytes: 50_000_000,
  durationMs: 2_000,
  peakMbps: steadyMbps + 10,
  stabilityPercent: 94,
  rampRatio: 1.02,
  capReached: false,
  qualification: "qualified",
  timeline: [{ elapsedMs: 250, value: steadyMbps }],
  aggregation: "single",
  samples: [],
});

const result: DiagnosticResult = {
  id: "55555555-5555-5555-5555-555555555555",
  startedAt: "2026-08-03T00:00:00Z",
  completedAt: "2026-08-03T00:00:20Z",
  mode: "quick",
  transferMode: "compare",
  edge: {
    edge: "LAX",
    network: "Example Fiber",
    asn: 64500,
    protocol: "h2",
    tlsVersion: "TLSv1.3",
    ipVersion: "IPv6",
  },
  idleLatency: latency(14),
  download: throughput(500),
  upload: throughput(40),
  downloadLatency: { ...latency(21), increaseMs: 7, grade: "A" },
  uploadLatency: { ...latency(28), increaseMs: 14, grade: "A" },
  flowMeasurements: [
    { strategy: "single", concurrency: 1, download: throughput(310) },
    { strategy: "aggregate", concurrency: 6, download: throughput(500), upload: throughput(40) },
  ],
  downloadScaling: [],
  services: [{ id: "github", name: "GitHub", reachable: true, durationMs: 35 }],
  dataUsedBytes: 100_000_000,
};

describe("schema 2.0 website report serialization", () => {
  it("wraps the browser result without dropping measurements", () => {
    vi.stubGlobal("navigator", { platform: "MacIntel" });

    const report = toSchemaTwoBrowserReport(result);

    expect(report.schemaVersion).toBe("2.0");
    expect(report.producer?.application).toBe("web");
    expect(report.run.profile).toBe("quick");
    expect(report.run.platform).toBe("MacIntel");
    expect(report.transferPlan?.transferCapBytes).toBe(728_000_000);
    expect(report.internetTransfer?.download.steadyMbps).toBe(500);
    expect(report.internetTransfer?.flowMeasurements[1].connections).toBe(6);
    expect(report.internetTransfer?.download.timeline?.[0]).toEqual({ elapsedMs: 250, mbps: 500 });
    expect(report.browserEvidence).toMatchObject({
      edge: { edge: "LAX", network: "Example Fiber" },
      serviceChecks: [{ id: "github", reachable: true }],
    });
    expect(report.deepDiagnostics).toBeNull();

    vi.unstubAllGlobals();
  });

  it("emits formatted JSON using the shared wrapper", () => {
    const parsed = JSON.parse(serializeBrowserReport(result)) as Record<string, unknown>;

    expect(parsed.schemaVersion).toBe("2.0");
    expect(parsed).toHaveProperty("internetTransfer");
    expect(parsed).toHaveProperty("browserEvidence");
    expect(parsed).not.toHaveProperty("mode");
  });
});

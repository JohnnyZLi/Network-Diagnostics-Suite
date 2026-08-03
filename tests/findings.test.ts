import { describe, expect, it } from "vitest";
import { classifyDiagnosticResult } from "../src/diagnostics/findings";
import type {
  DiagnosticResult,
  LatencySummary,
  LoadedLatencySummary,
  ThroughputSummary
} from "../src/types/diagnostics";

function latency(overrides: Partial<LatencySummary> = {}): LatencySummary {
  return {
    sent: 20,
    received: 20,
    lost: 0,
    lossPercent: 0,
    minMs: 18,
    maxMs: 24,
    meanMs: 20,
    medianMs: 20,
    p95Ms: 23,
    jitterMs: 2,
    samples: Array.from({ length: 20 }, () => 20),
    ...overrides
  };
}

function loaded(increaseMs: number): LoadedLatencySummary {
  return { ...latency(), increaseMs, grade: increaseMs >= 100 ? "F" : "A" };
}

function throughput(steadyMbps: number, overrides: Partial<ThroughputSummary> = {}): ThroughputSummary {
  return {
    mbps: steadyMbps,
    steadyMbps,
    bytes: 12_000_000,
    durationMs: 4_000,
    peakMbps: steadyMbps * 1.05,
    stabilityPercent: 92,
    rampRatio: 1,
    capReached: false,
    qualification: "qualified",
    timeline: [],
    ...overrides
  };
}

function result(overrides: Partial<DiagnosticResult> = {}): DiagnosticResult {
  const aggregate = throughput(100);
  const single = throughput(90);
  return {
    id: "fixture",
    startedAt: "2026-08-03T00:00:00.000Z",
    completedAt: "2026-08-03T00:00:20.000Z",
    mode: "standard",
    transferMode: "compare",
    edge: null,
    idleLatency: latency(),
    download: aggregate,
    upload: throughput(40),
    downloadLatency: loaded(8),
    uploadLatency: loaded(10),
    flowMeasurements: [
      { strategy: "single", concurrency: 1, download: single },
      { strategy: "aggregate", concurrency: 8, download: aggregate }
    ],
    services: [],
    dataUsedBytes: 50_000_000,
    ...overrides
  };
}

describe("evidence-backed diagnostic findings", () => {
  it("classifies severe loaded latency with the measured direction and evidence", () => {
    const findings = classifyDiagnosticResult(result({
      downloadLatency: loaded(55),
      uploadLatency: loaded(135)
    }));
    const finding = findings.find((candidate) => candidate.id === "loaded-latency");

    expect(finding?.severity).toBe("critical");
    expect(finding?.summary).toContain("upload");
    expect(finding?.evidence.map((item) => item.metric)).toContain("uploadLatency.increaseMs");
    expect(finding?.recommendations.length).toBeGreaterThan(0);
  });

  it("reports a constrained single flow without inventing a cause", () => {
    const single = throughput(24);
    const aggregate = throughput(120);
    const findings = classifyDiagnosticResult(result({
      download: aggregate,
      flowMeasurements: [
        { strategy: "single", concurrency: 1, download: single },
        { strategy: "aggregate", concurrency: 8, download: aggregate }
      ]
    }));
    const finding = findings.find((candidate) => candidate.id === "single-flow-limited");

    expect(finding?.severity).toBe("warning");
    expect(finding?.evidence.find((item) => item.metric.endsWith("singleSharePercent"))?.value).toBe("20%");
    expect(finding?.summary).not.toMatch(/ISP is|router is|server is/i);
  });

  it("returns a transparent baseline finding when no warning threshold is crossed", () => {
    const findings = classifyDiagnosticResult(result());

    expect(findings[0]?.id).toBe("no-obvious-instability");
    expect(findings[0]?.confidence).toBe("high");
    expect(findings.every((finding) => finding.severity === "info")).toBe(true);
  });
});

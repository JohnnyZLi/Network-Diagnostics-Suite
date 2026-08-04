import { describe, expect, it } from "vitest";
import fixtures from "../contracts/diagnostic-parity-fixtures.v1.json";
import { classifyDiagnosticResult } from "../src/diagnostics/findings";
import type { DiagnosticResult, LatencySummary, ThroughputSummary } from "../src/types/diagnostics";

interface Scenario {
  id: string;
  sent: number;
  received: number;
  idleMedianMs: number;
  idleJitterMs: number;
  downloadIncreaseMs: number;
  uploadIncreaseMs: number;
  singleMbps: number;
  aggregateMbps: number;
  expectedFindingIds: string[];
}

function latency(medianMs: number, jitterMs: number, sent = 20, received = 20): LatencySummary {
  const samples = Array.from({ length: sent }, (_, index) => index < received ? medianMs : null);
  return {
    sent,
    received,
    lost: sent - received,
    lossPercent: sent === 0 ? 0 : ((sent - received) / sent) * 100,
    minMs: medianMs - 2,
    maxMs: medianMs + 3,
    meanMs: medianMs,
    medianMs,
    p95Ms: medianMs + 2,
    jitterMs,
    samples
  };
}

function throughput(steadyMbps: number): ThroughputSummary {
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
    timeline: []
  };
}

function result(scenario: Scenario): DiagnosticResult {
  const idle = latency(scenario.idleMedianMs, scenario.idleJitterMs, scenario.sent, scenario.received);
  const download = throughput(scenario.aggregateMbps);
  const upload = throughput(40);
  return {
    id: scenario.id,
    startedAt: "2026-08-04T00:00:00Z",
    completedAt: "2026-08-04T00:00:20Z",
    mode: "standard",
    transferMode: "compare",
    edge: null,
    idleLatency: idle,
    download,
    upload,
    downloadLatency: {
      ...latency(scenario.idleMedianMs + scenario.downloadIncreaseMs, 2),
      increaseMs: scenario.downloadIncreaseMs,
      grade: "A"
    },
    uploadLatency: {
      ...latency(scenario.idleMedianMs + scenario.uploadIncreaseMs, 2),
      increaseMs: scenario.uploadIncreaseMs,
      grade: "A"
    },
    flowMeasurements: [
      { strategy: "single", concurrency: 1, download: throughput(scenario.singleMbps) },
      { strategy: "aggregate", concurrency: 8, download, upload }
    ],
    services: [],
    dataUsedBytes: 50_000_000
  };
}

describe("browser/native diagnostic finding parity fixtures", () => {
  for (const scenario of fixtures.scenarios as Scenario[]) {
    it(scenario.id, () => {
      const ids = classifyDiagnosticResult(result(scenario)).map((finding) => finding.id);
      for (const expected of scenario.expectedFindingIds) expect(ids).toContain(expected);
    });
  }
});

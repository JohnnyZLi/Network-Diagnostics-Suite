import { describe, expect, it } from "vitest";
import { clearRecentResults, loadRecentResults, MAX_RECENT_RESULTS, saveRecentResult } from "../src/core/result-history";
import type { DiagnosticResult } from "../src/types/diagnostics";

class MemoryStorage {
  private readonly values = new Map<string, string>();
  getItem(key: string): string | null { return this.values.get(key) ?? null; }
  setItem(key: string, value: string): void { this.values.set(key, value); }
  removeItem(key: string): void { this.values.delete(key); }
}

function result(id: string, startedAt: string): DiagnosticResult {
  const latency = {
    sent: 1,
    received: 1,
    lost: 0,
    lossPercent: 0,
    minMs: 20,
    maxMs: 20,
    meanMs: 20,
    medianMs: 20,
    p95Ms: 20,
    jitterMs: 0,
    samples: [20]
  };
  const loaded = { ...latency, increaseMs: 5, grade: "A+" as const };
  const throughput = {
    mbps: 200,
    steadyMbps: 200,
    bytes: 1_000,
    durationMs: 1_000,
    peakMbps: 210,
    stabilityPercent: 90,
    rampRatio: 1,
    capReached: false,
    qualification: "qualified" as const,
    timeline: []
  };
  return {
    id,
    startedAt,
    completedAt: startedAt,
    mode: "quick",
    edge: null,
    idleLatency: latency,
    download: throughput,
    upload: throughput,
    downloadLatency: loaded,
    uploadLatency: loaded,
    services: [],
    dataUsedBytes: 2_000
  };
}

describe("local result history", () => {
  it("deduplicates reports and keeps the newest configured limit", () => {
    const storage = new MemoryStorage();
    for (let index = 0; index < MAX_RECENT_RESULTS + 3; index += 1) {
      saveRecentResult(result(`result-${index}`, new Date(2026, 0, index + 1).toISOString()), storage);
    }
    saveRecentResult(result("result-5", new Date(2026, 2, 1).toISOString()), storage);

    const saved = loadRecentResults(storage);
    expect(saved).toHaveLength(MAX_RECENT_RESULTS);
    expect(saved[0]?.id).toBe("result-5");
    expect(saved.filter((candidate) => candidate.id === "result-5")).toHaveLength(1);
  });

  it("ignores malformed stored values and clears history", () => {
    const storage = new MemoryStorage();
    storage.setItem("network-diagnostics.recent-results.v1", JSON.stringify([{ id: "incomplete" }]));
    expect(loadRecentResults(storage)).toEqual([]);

    saveRecentResult(result("valid", new Date(2026, 0, 1).toISOString()), storage);
    clearRecentResults(storage);
    expect(loadRecentResults(storage)).toEqual([]);
  });
});

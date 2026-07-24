import { describe, expect, it } from "vitest";
import type { DownloadDeliveryObservation, DownloadWarmupObservation } from "../src/diagnostics/http";
import { summarizeDownloadDelivery } from "../src/diagnostics/throughput";

const warmup: DownloadWarmupObservation = {
  source: "static",
  cacheStatus: "MISS",
  ageSeconds: null,
  bytes: 24 * 1024 * 1024
};

describe("download delivery summary", () => {
  it("separates warmup cache state from measured edge hits", () => {
    const observations: DownloadDeliveryObservation[] = [
      { source: "static", cacheStatus: "HIT", ageSeconds: 3 },
      { source: "static", cacheStatus: "HIT", ageSeconds: 4 },
      { source: "static", cacheStatus: "MISS", ageSeconds: null }
    ];

    expect(summarizeDownloadDelivery(warmup, observations, ["h3", "h3", "h2"])).toEqual({
      staticRequests: 3,
      workerFallbackRequests: 0,
      cacheStatusCounts: { HIT: 2, MISS: 1 },
      edgeCacheServedPercent: (2 / 3) * 100,
      maxAgeSeconds: 4,
      protocols: ["h2", "h3"],
      warmupBytes: warmup.bytes,
      warmupSource: "static",
      warmupCacheStatus: "MISS"
    });
  });

  it("reports Worker fallbacks without inventing a cache percentage", () => {
    const observations: DownloadDeliveryObservation[] = [
      { source: "worker", cacheStatus: null, ageSeconds: null }
    ];

    const summary = summarizeDownloadDelivery(
      { source: "worker", cacheStatus: null, ageSeconds: null, bytes: 8 * 1024 * 1024 },
      observations,
      []
    );

    expect(summary.staticRequests).toBe(0);
    expect(summary.workerFallbackRequests).toBe(1);
    expect(summary.edgeCacheServedPercent).toBeNull();
    expect(summary.cacheStatusCounts).toEqual({});
  });
});

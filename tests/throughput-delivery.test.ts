import { describe, expect, it } from "vitest";
import type { DownloadDeliveryObservation, DownloadWarmupObservation } from "../src/diagnostics/http";
import { STATIC_DOWNLOAD_STREAM_BYTES } from "../src/diagnostics/http";
import type { DownloadStreamRejection } from "../src/types/diagnostics";
import { summarizeDownloadDelivery } from "../src/diagnostics/throughput";

const warmup: DownloadWarmupObservation = {
  source: "static",
  cacheStatus: "MISS",
  ageSeconds: null,
  bytes: 0,
  cachedBytes: STATIC_DOWNLOAD_STREAM_BYTES
};

describe("download delivery summary", () => {
  it("separates server-side warmup from measured edge hits and request generations", () => {
    const observations: DownloadDeliveryObservation[] = [
      { source: "static", cacheStatus: "HIT", ageSeconds: 3 },
      { source: "static", cacheStatus: "HIT", ageSeconds: 4 },
      { source: "static", cacheStatus: "HIT", ageSeconds: 4 }
    ];

    expect(summarizeDownloadDelivery(
      warmup,
      observations,
      [],
      ["h3", "h3", "h2"],
      {
        started: 3,
        completed: 0,
        replacements: 0,
        generations: [{ generation: 0, requests: 3, bytes: 220_000_000 }]
      }
    )).toEqual({
      staticRequests: 3,
      workerFallbackRequests: 0,
      rejectedStaticRequests: 0,
      streamRejections: [],
      cacheStatusCounts: { HIT: 3 },
      edgeCacheServedPercent: 100,
      maxAgeSeconds: 4,
      protocols: ["h2", "h3"],
      logicalStreamBytes: STATIC_DOWNLOAD_STREAM_BYTES,
      startedRequests: 3,
      completedRequests: 0,
      replacementRequests: 0,
      interruptedRequests: 3,
      requestGenerations: [{ generation: 0, requests: 3, bytes: 220_000_000 }],
      warmupBytes: 0,
      warmupCachedBytes: STATIC_DOWNLOAD_STREAM_BYTES,
      warmupSource: "static",
      warmupCacheStatus: "MISS"
    });
  });

  it("reports Worker fallbacks and their rejected static response", () => {
    const observations: DownloadDeliveryObservation[] = [
      { source: "worker", cacheStatus: null, ageSeconds: null }
    ];
    const rejections: DownloadStreamRejection[] = [{
      reason: "wrong-content-length",
      status: 200,
      marker: "stream-edge-v4",
      logicalBytes: STATIC_DOWNLOAD_STREAM_BYTES,
      segmentCount: 4,
      contentLength: 32 * 1024 * 1024
    }];

    const summary = summarizeDownloadDelivery(
      { source: "worker", cacheStatus: null, ageSeconds: null, bytes: 8 * 1024 * 1024, cachedBytes: 0 },
      observations,
      rejections,
      [],
      {
        started: 1,
        completed: 1,
        replacements: 0,
        generations: [{ generation: 0, requests: 1, bytes: 32 * 1024 * 1024 }]
      }
    );

    expect(summary.staticRequests).toBe(0);
    expect(summary.workerFallbackRequests).toBe(1);
    expect(summary.rejectedStaticRequests).toBe(1);
    expect(summary.streamRejections).toEqual(rejections);
    expect(summary.edgeCacheServedPercent).toBeNull();
    expect(summary.cacheStatusCounts).toEqual({});
  });
});

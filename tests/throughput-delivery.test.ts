import { describe, expect, it } from "vitest";
import type { DownloadDeliveryObservation, DownloadWarmupObservation } from "../src/diagnostics/http";
import {
  R2_DOWNLOAD_OBJECT_BYTES,
  R2_DOWNLOAD_ORIGIN,
  R2_DOWNLOAD_RANGE_BYTES,
  STATIC_DOWNLOAD_STREAM_BYTES
} from "../src/diagnostics/http";
import type { DownloadStreamRejection } from "../src/types/diagnostics";
import { summarizeDownloadDelivery } from "../src/diagnostics/throughput";

const workerWarmup: DownloadWarmupObservation = {
  source: "static",
  cacheStatus: "MISS",
  ageSeconds: null,
  bytes: 0,
  cachedBytes: STATIC_DOWNLOAD_STREAM_BYTES
};

describe("download delivery summary", () => {
  it("separates server-side warmup from measured Worker edge hits", () => {
    const observations: DownloadDeliveryObservation[] = [
      { source: "static", cacheStatus: "HIT", ageSeconds: 3 },
      { source: "static", cacheStatus: "HIT", ageSeconds: 4 },
      { source: "static", cacheStatus: "HIT", ageSeconds: 4 }
    ];

    expect(summarizeDownloadDelivery(
      {
        requestedPath: "worker-stream",
        selectedPath: "worker-stream-v4",
        warmup: workerWarmup,
        r2ProbeStatus: "not-requested",
        fallbackReason: null
      },
      observations,
      [],
      ["h3", "h3", "h2"],
      {
        started: 3,
        completed: 0,
        replacements: 0,
        generations: [{ generation: 0, requests: 3, bytes: 220_000_000 }]
      },
      null
    )).toEqual({
      requestedPath: "worker-stream",
      selectedPath: "worker-stream-v4",
      pathFallbackReason: null,
      r2Origin: R2_DOWNLOAD_ORIGIN,
      r2ObjectBytes: R2_DOWNLOAD_OBJECT_BYTES,
      r2RangeBytes: R2_DOWNLOAD_RANGE_BYTES,
      r2ProbeStatus: "not-requested",
      r2Requests: 0,
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

  it("reports direct R2 cache hits", () => {
    const warmup: DownloadWarmupObservation = {
      source: "r2",
      cacheStatus: "HIT",
      ageSeconds: 20,
      bytes: 0,
      cachedBytes: R2_DOWNLOAD_OBJECT_BYTES
    };
    const summary = summarizeDownloadDelivery(
      {
        requestedPath: "auto",
        selectedPath: "r2-direct-v1",
        warmup,
        r2ProbeStatus: "available",
        fallbackReason: null
      },
      [
        { source: "r2", cacheStatus: "HIT", ageSeconds: 21 },
        { source: "r2", cacheStatus: "HIT", ageSeconds: 21 }
      ],
      [],
      ["h3"],
      {
        started: 2,
        completed: 0,
        replacements: 0,
        generations: [{ generation: 0, requests: 2, bytes: 100_000_000 }]
      },
      null
    );

    expect(summary.selectedPath).toBe("r2-direct-v1");
    expect(summary.r2Requests).toBe(2);
    expect(summary.staticRequests).toBe(0);
    expect(summary.edgeCacheServedPercent).toBe(100);
    expect(summary.logicalStreamBytes).toBe(R2_DOWNLOAD_RANGE_BYTES);
  });

  it("reports Worker fallbacks and their rejected response", () => {
    const observations: DownloadDeliveryObservation[] = [
      { source: "worker", cacheStatus: null, ageSeconds: null }
    ];
    const rejections: DownloadStreamRejection[] = [{
      reason: "wrong-content-length",
      status: 200,
      marker: "stream-edge-v4",
      logicalBytes: STATIC_DOWNLOAD_STREAM_BYTES,
      segmentCount: 4,
      contentLength: 32 * 1024 * 1024,
      receivedBytes: null
    }];

    const summary = summarizeDownloadDelivery(
      {
        requestedPath: "worker-stream",
        selectedPath: "worker-stream-v4",
        warmup: { source: "worker", cacheStatus: null, ageSeconds: null, bytes: 8 * 1024 * 1024, cachedBytes: 0 },
        r2ProbeStatus: "not-requested",
        fallbackReason: null
      },
      observations,
      rejections,
      [],
      {
        started: 1,
        completed: 1,
        replacements: 0,
        generations: [{ generation: 0, requests: 1, bytes: 32 * 1024 * 1024 }]
      },
      null
    );

    expect(summary.staticRequests).toBe(0);
    expect(summary.workerFallbackRequests).toBe(1);
    expect(summary.rejectedStaticRequests).toBe(1);
    expect(summary.streamRejections).toEqual(rejections);
    expect(summary.edgeCacheServedPercent).toBeNull();
    expect(summary.cacheStatusCounts).toEqual({});
  });
});

import { describe, expect, it } from "vitest";
import {
  createBrowserSpeedStreamResponse,
  createCacheableSpeedSegmentResponse,
  createConcatenatedBody,
  createSpeedSegmentCacheKey,
  SPEED_PAYLOAD_MARKER,
  SPEED_SEGMENT_BYTES,
  SPEED_SEGMENT_COUNT,
  SPEED_STREAM_BYTES,
  speedSegmentPath
} from "../worker/index";

async function readBody(body: ReadableStream<Uint8Array>): Promise<number[]> {
  const reader = body.getReader();
  const values: number[] = [];
  while (true) {
    const { done, value } = await reader.read();
    if (done) return values;
    values.push(...value);
  }
}

describe("Worker-local segmented speed cache", () => {
  it("uses one queryless cache key per segment", () => {
    const request = new Request("https://network.johnnyli.dev/speed/v4/stream?worker=2");
    const key = createSpeedSegmentCacheKey(request, 2);

    expect(new URL(key.url).pathname).toBe(speedSegmentPath(2));
    expect(new URL(key.url).search).toBe("");
    expect(key.method).toBe("GET");
  });

  it("stores a complete cacheable segment with deterministic metadata", () => {
    const asset = new Response(new Uint8Array([1]), {
      headers: { ETag: "test-etag", "Cache-Control": "no-store" }
    });
    const cached = createCacheableSpeedSegmentResponse(asset, 1, 100);

    expect(cached.status).toBe(200);
    expect(cached.headers.get("Cache-Control")).toBe("public, max-age=86400");
    expect(cached.headers.get("Content-Length")).toBe(SPEED_SEGMENT_BYTES.toString());
    expect(cached.headers.get("X-NDS-Segment")).toBe("1");
    expect(cached.headers.get("X-NDS-Cached-At")).toBe("100");
    expect(cached.headers.get("ETag")).toBe("test-etag");
  });

  it("exposes a browser-uncacheable logical stream without fixed HTTP framing", () => {
    const responses = Array.from({ length: SPEED_SEGMENT_COUNT }, () => new Response(new Uint8Array([1])));
    const browser = createBrowserSpeedStreamResponse(responses, "HIT", 8);

    expect(browser.headers.get("Cache-Control")).toBe("no-store, no-transform");
    expect(browser.headers.get("Content-Length")).toBeNull();
    expect(browser.headers.get("X-NDS-Logical-Bytes")).toBe(SPEED_STREAM_BYTES.toString());
    expect(browser.headers.get("X-NDS-Segment-Count")).toBe(SPEED_SEGMENT_COUNT.toString());
    expect(browser.headers.get("X-NDS-Cache-Status")).toBe("HIT");
    expect(browser.headers.get("X-NDS-Cache-Age")).toBe("8");
    expect(browser.headers.get("X-NDS-Payload")).toBe(SPEED_PAYLOAD_MARKER);
  });

  it("concatenates segment bodies without buffering the logical response", async () => {
    const body = createConcatenatedBody([
      new Response(new Uint8Array([1, 2])),
      new Response(new Uint8Array([3])),
      new Response(new Uint8Array([4, 5]))
    ]);

    await expect(readBody(body)).resolves.toEqual([1, 2, 3, 4, 5]);
  });
});

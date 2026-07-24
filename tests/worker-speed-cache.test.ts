import { describe, expect, it } from "vitest";
import {
  createBrowserSpeedResponse,
  createCacheableSpeedResponse,
  createSpeedCacheKey,
  createSpeedCacheLookupRequest,
  SPEED_ASSET_BYTES,
  SPEED_ASSET_PATH,
  SPEED_PAYLOAD_MARKER
} from "../worker/index";

describe("Worker-local speed cache", () => {
  it("uses one queryless URL as the cache key", () => {
    const request = new Request("https://network.johnnyli.dev/speed/v3/payload.bin?ignored=1", {
      headers: { Range: "bytes=0-1023" }
    });

    const key = createSpeedCacheKey(request);
    expect(new URL(key.url).pathname).toBe(SPEED_ASSET_PATH);
    expect(new URL(key.url).search).toBe("");
    expect(key.method).toBe("GET");
    expect(key.headers.get("Range")).toBeNull();
  });

  it("preserves a range only on cache lookup", () => {
    const request = new Request("https://network.johnnyli.dev/speed/v3/payload.bin", {
      headers: { Range: "bytes=0-2097151" }
    });

    const lookup = createSpeedCacheLookupRequest(request);
    expect(lookup.headers.get("Range")).toBe("bytes=0-2097151");
  });

  it("stores a full cacheable object with deterministic metadata", () => {
    const asset = new Response(new Uint8Array([1]), {
      headers: { ETag: "test-etag", "Cache-Control": "no-store" }
    });
    const cached = createCacheableSpeedResponse(asset, 100);

    expect(cached.status).toBe(200);
    expect(cached.headers.get("Cache-Control")).toBe("public, max-age=86400");
    expect(cached.headers.get("Content-Length")).toBe(SPEED_ASSET_BYTES.toString());
    expect(cached.headers.get("Accept-Ranges")).toBe("bytes");
    expect(cached.headers.get("X-NDS-Payload")).toBe(SPEED_PAYLOAD_MARKER);
    expect(cached.headers.get("X-NDS-Cached-At")).toBe("100");
    expect(cached.headers.get("ETag")).toBe("test-etag");
  });

  it("prevents browser caching while exposing Worker cache state", () => {
    const cached = createCacheableSpeedResponse(new Response(new Uint8Array([1])), 100);
    const browser = createBrowserSpeedResponse(cached, "HIT", 108);

    expect(browser.headers.get("Cache-Control")).toBe("no-store, no-transform");
    expect(browser.headers.get("X-NDS-Cache-Status")).toBe("HIT");
    expect(browser.headers.get("X-NDS-Cache-Age")).toBe("8");
    expect(browser.headers.get("X-NDS-Cached-At")).toBeNull();
    expect(browser.headers.get("X-NDS-Payload")).toBe(SPEED_PAYLOAD_MARKER);
  });
});

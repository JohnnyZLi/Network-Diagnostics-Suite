import { describe, expect, it } from "vitest";
import { STATIC_DOWNLOAD_ASSET_BYTES, staticResponseMatchesRequest } from "../src/diagnostics/http";

function response(status: number, contentLength: number, contentRange?: string): Response {
  const headers = new Headers({
    "Content-Length": contentLength.toString(),
    "X-NDS-Payload": "static-edge-v2"
  });
  if (contentRange) headers.set("Content-Range", contentRange);
  return new Response(new Uint8Array(1), { status, headers });
}

describe("static download response validation", () => {
  it("accepts an exact partial-content response", () => {
    const requested = 2 * 1024 * 1024;
    expect(staticResponseMatchesRequest(
      response(206, requested, `bytes 0-${requested - 1}/${STATIC_DOWNLOAD_ASSET_BYTES}`),
      requested
    )).toBe(true);
  });

  it("rejects a full asset when the server ignores a smaller range request", () => {
    const requested = 2 * 1024 * 1024;
    expect(staticResponseMatchesRequest(
      response(200, STATIC_DOWNLOAD_ASSET_BYTES),
      requested
    )).toBe(false);
  });

  it("accepts a full static asset for a full-size request", () => {
    expect(staticResponseMatchesRequest(
      response(200, STATIC_DOWNLOAD_ASSET_BYTES),
      STATIC_DOWNLOAD_ASSET_BYTES
    )).toBe(true);
  });

  it("rejects unmarked responses", () => {
    const unmarked = new Response(new Uint8Array(1), {
      status: 206,
      headers: {
        "Content-Length": "1",
        "Content-Range": `bytes 0-0/${STATIC_DOWNLOAD_ASSET_BYTES}`
      }
    });
    expect(staticResponseMatchesRequest(unmarked, 1)).toBe(false);
  });
});

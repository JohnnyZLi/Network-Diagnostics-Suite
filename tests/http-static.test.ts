import { describe, expect, it } from "vitest";
import { STATIC_DOWNLOAD_STREAM_BYTES, streamResponseMatchesRequest } from "../src/diagnostics/http";

function response(
  status: number,
  contentLength: number,
  logicalBytes: number,
  marker = "stream-edge-v4"
): Response {
  return new Response(new Uint8Array(1), {
    status,
    headers: {
      "Content-Length": contentLength.toString(),
      "X-NDS-Logical-Bytes": logicalBytes.toString(),
      "X-NDS-Payload": marker
    }
  });
}

describe("long-lived download response validation", () => {
  it("accepts the exact v4 logical stream", () => {
    expect(streamResponseMatchesRequest(
      response(200, STATIC_DOWNLOAD_STREAM_BYTES, STATIC_DOWNLOAD_STREAM_BYTES)
    )).toBe(true);
  });

  it("rejects a short response", () => {
    expect(streamResponseMatchesRequest(
      response(200, 24 * 1024 * 1024, STATIC_DOWNLOAD_STREAM_BYTES)
    )).toBe(false);
  });

  it("rejects mismatched logical metadata", () => {
    expect(streamResponseMatchesRequest(
      response(200, STATIC_DOWNLOAD_STREAM_BYTES, 24 * 1024 * 1024)
    )).toBe(false);
  });

  it("rejects an unmarked response", () => {
    expect(streamResponseMatchesRequest(
      response(200, STATIC_DOWNLOAD_STREAM_BYTES, STATIC_DOWNLOAD_STREAM_BYTES, "static-segment-v4")
    )).toBe(false);
  });
});

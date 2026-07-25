import { describe, expect, it } from "vitest";
import {
  inspectStreamResponse,
  STATIC_DOWNLOAD_SEGMENT_COUNT,
  STATIC_DOWNLOAD_STREAM_BYTES,
  streamResponseMatchesRequest
} from "../src/diagnostics/http";

interface ResponseOptions {
  status?: number;
  contentLength?: number | null;
  logicalBytes?: number;
  segmentCount?: number;
  marker?: string;
  body?: BodyInit | null;
}

function response(options: ResponseOptions = {}): Response {
  const headers = new Headers({
    "X-NDS-Logical-Bytes": (options.logicalBytes ?? STATIC_DOWNLOAD_STREAM_BYTES).toString(),
    "X-NDS-Payload": options.marker ?? "stream-edge-v4",
    "X-NDS-Segment-Count": (options.segmentCount ?? STATIC_DOWNLOAD_SEGMENT_COUNT).toString()
  });
  if (options.contentLength !== null && options.contentLength !== undefined) {
    headers.set("Content-Length", options.contentLength.toString());
  }
  return new Response(options.body === undefined ? new Uint8Array(1) : options.body, {
    status: options.status ?? 200,
    headers
  });
}

describe("long-lived download response validation", () => {
  it("accepts a correctly identified stream without Content-Length", () => {
    expect(streamResponseMatchesRequest(response({ contentLength: null }))).toBe(true);
  });

  it("accepts exact Content-Length as supporting evidence", () => {
    expect(streamResponseMatchesRequest(response({
      contentLength: STATIC_DOWNLOAD_STREAM_BYTES
    }))).toBe(true);
  });

  it("rejects a conflicting Content-Length when one is present", () => {
    const result = inspectStreamResponse(response({ contentLength: 24 * 1024 * 1024 }));
    expect(result?.reason).toBe("wrong-content-length");
    expect(result?.contentLength).toBe(24 * 1024 * 1024);
  });

  it("rejects mismatched logical metadata", () => {
    expect(inspectStreamResponse(response({ logicalBytes: 24 * 1024 * 1024 }))?.reason)
      .toBe("wrong-logical-size");
  });

  it("rejects an unmarked response", () => {
    expect(inspectStreamResponse(response({ marker: "static-segment-v4" }))?.reason)
      .toBe("wrong-marker");
  });

  it("rejects the wrong segment count", () => {
    expect(inspectStreamResponse(response({ segmentCount: 3 }))?.reason)
      .toBe("wrong-segment-count");
  });

  it("rejects a response without a body", () => {
    expect(inspectStreamResponse(response({ body: null }))?.reason).toBe("missing-body");
  });

  it("rejects a non-success status", () => {
    expect(inspectStreamResponse(response({ status: 502 }))?.reason).toBe("status");
  });
});

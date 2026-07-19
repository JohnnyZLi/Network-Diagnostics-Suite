import type { EdgeMetadata, UploadReceipt } from "../src/types/api";

interface Env {
  ASSETS: {
    fetch(request: Request): Promise<Response>;
  };
}

interface CloudflareRequestProperties {
  colo?: unknown;
  asOrganization?: unknown;
  asn?: unknown;
  httpProtocol?: unknown;
  tlsVersion?: unknown;
}

type WorkerRequest = Request & { cf?: CloudflareRequestProperties };

const DOWNLOAD_MIN_BYTES = 1_024;
const DOWNLOAD_MAX_BYTES = 24 * 1024 * 1024;
const UPLOAD_MAX_BYTES = 8 * 1024 * 1024;
const CHUNK_SIZE = 64 * 1024;

const streamChunk = createIncompressibleChunk(CHUNK_SIZE);

export function redirectToHttps(request: Request): Response | null {
  const url = new URL(request.url);
  if (url.protocol !== "http:") return null;

  url.protocol = "https:";
  return Response.redirect(url.toString(), 308);
}

function createIncompressibleChunk(size: number): Uint8Array {
  const bytes = new Uint8Array(size);
  let state = 0x6d2b79f5;
  for (let index = 0; index < size; index += 1) {
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    bytes[index] = state & 0xff;
  }
  return bytes;
}

export function clampDownloadSize(rawValue: string | null): number {
  const parsed = Number.parseInt(rawValue ?? "", 10);
  if (!Number.isFinite(parsed)) return 1024 * 1024;
  return Math.min(DOWNLOAD_MAX_BYTES, Math.max(DOWNLOAD_MIN_BYTES, parsed));
}

function diagnosticHeaders(contentType: string): Headers {
  return new Headers({
    "Cache-Control": "no-store, no-cache, must-revalidate, max-age=0",
    "Content-Type": contentType,
    "Cross-Origin-Resource-Policy": "same-origin",
    "Strict-Transport-Security": "max-age=31536000",
    "Timing-Allow-Origin": "*",
    "X-Content-Type-Options": "nosniff"
  });
}

function isCrossSite(request: Request): boolean {
  return request.headers.get("Sec-Fetch-Site") === "cross-site";
}

function errorResponse(message: string, status: number, extraHeaders?: HeadersInit): Response {
  const headers = diagnosticHeaders("application/json; charset=utf-8");
  if (extraHeaders) {
    new Headers(extraHeaders).forEach((value, key) => headers.set(key, value));
  }
  return Response.json({ error: message }, { status, headers });
}

function handlePing(request: Request): Response {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return errorResponse("Method not allowed", 405, { Allow: "GET, HEAD" });
  }
  const headers = diagnosticHeaders("text/plain; charset=utf-8");
  headers.set("Server-Timing", "edge;dur=0");
  return new Response(request.method === "HEAD" ? null : "ok", { status: 200, headers });
}

function handleMetadata(request: WorkerRequest): Response {
  if (request.method !== "GET") {
    return errorResponse("Method not allowed", 405, { Allow: "GET" });
  }

  const connectingIp = request.headers.get("CF-Connecting-IP") ?? "";
  const cf = request.cf;
  const metadata: EdgeMetadata = {
    edge: typeof cf?.colo === "string" ? cf.colo : null,
    network: typeof cf?.asOrganization === "string" ? cf.asOrganization : null,
    asn: typeof cf?.asn === "number" ? cf.asn : null,
    protocol: typeof cf?.httpProtocol === "string" ? cf.httpProtocol : null,
    tlsVersion: typeof cf?.tlsVersion === "string" ? cf.tlsVersion : null,
    ipVersion: connectingIp.includes(":") ? "IPv6" : connectingIp.includes(".") ? "IPv4" : "Unknown"
  };

  return Response.json(metadata, {
    headers: diagnosticHeaders("application/json; charset=utf-8")
  });
}

function handleDownload(request: Request, url: URL): Response {
  if (request.method !== "GET") {
    return errorResponse("Method not allowed", 405, { Allow: "GET" });
  }
  if (isCrossSite(request)) return errorResponse("Cross-site requests are not accepted", 403);

  const totalBytes = clampDownloadSize(url.searchParams.get("bytes"));
  let remaining = totalBytes;
  const body = new ReadableStream<Uint8Array>({
    pull(controller) {
      if (remaining <= 0) {
        controller.close();
        return;
      }
      const length = Math.min(remaining, streamChunk.byteLength);
      controller.enqueue(length === streamChunk.byteLength ? streamChunk : streamChunk.slice(0, length));
      remaining -= length;
    }
  });

  const headers = diagnosticHeaders("application/octet-stream");
  headers.set("Content-Length", totalBytes.toString());
  headers.set("Content-Encoding", "identity");
  return new Response(body, { headers });
}

async function handleUpload(request: Request): Promise<Response> {
  if (request.method !== "POST") {
    return errorResponse("Method not allowed", 405, { Allow: "POST" });
  }
  if (isCrossSite(request)) return errorResponse("Cross-site requests are not accepted", 403);

  const declaredLength = Number.parseInt(request.headers.get("Content-Length") ?? "0", 10);
  if (declaredLength > UPLOAD_MAX_BYTES) {
    return errorResponse(`Upload chunks may not exceed ${UPLOAD_MAX_BYTES} bytes`, 413);
  }

  let bytes = 0;
  const reader = request.body?.getReader();
  if (reader) {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      bytes += value.byteLength;
      if (bytes > UPLOAD_MAX_BYTES) {
        await reader.cancel("Upload limit exceeded");
        return errorResponse(`Upload chunks may not exceed ${UPLOAD_MAX_BYTES} bytes`, 413);
      }
    }
  }

  const receipt: UploadReceipt = { bytes };
  return Response.json(receipt, {
    headers: diagnosticHeaders("application/json; charset=utf-8")
  });
}

export default {
  async fetch(request: WorkerRequest, env: Env): Promise<Response> {
    const httpsRedirect = redirectToHttps(request);
    if (httpsRedirect) return httpsRedirect;

    const url = new URL(request.url);
    switch (url.pathname) {
      case "/api/ping":
        return handlePing(request);
      case "/api/meta":
        return handleMetadata(request);
      case "/api/download":
        return handleDownload(request, url);
      case "/api/upload":
        return handleUpload(request);
      case "/api/health":
        return Response.json({ status: "ok" }, {
          headers: diagnosticHeaders("application/json; charset=utf-8")
        });
      default:
        if (url.pathname.startsWith("/api/")) return errorResponse("Not found", 404);
        return env.ASSETS.fetch(request);
    }
  }
} satisfies { fetch(request: WorkerRequest, env: Env): Promise<Response> };

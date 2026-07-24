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
const DOWNLOAD_MAX_BYTES = 32 * 1024 * 1024;
const UPLOAD_MAX_BYTES = 16 * 1024 * 1024;
const CHUNK_SIZE = 1024 * 1024;

export const SPEED_ASSET_BYTES = 24 * 1024 * 1024;
export const SPEED_ASSET_PATH = "/speed/v3/payload.bin";
export const SPEED_PAYLOAD_MARKER = "static-edge-v3";
const SPEED_CACHE_TTL_SECONDS = 24 * 60 * 60;
const SPEED_CACHED_AT_HEADER = "X-NDS-Cached-At";

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

function speedAssetUrl(request: Request): URL {
  const url = new URL(request.url);
  url.pathname = SPEED_ASSET_PATH;
  url.search = "";
  url.hash = "";
  return url;
}

export function createSpeedCacheKey(request: Request): Request {
  return new Request(speedAssetUrl(request).toString(), { method: "GET" });
}

export function createSpeedCacheLookupRequest(request: Request): Request {
  const headers = new Headers();
  const range = request.headers.get("Range");
  if (range) headers.set("Range", range);
  return new Request(speedAssetUrl(request).toString(), { method: "GET", headers });
}

export function createCacheableSpeedResponse(
  assetResponse: Response,
  cachedAtSeconds = Math.floor(Date.now() / 1000)
): Response {
  const headers = new Headers({
    "Accept-Ranges": "bytes",
    "Cache-Control": `public, max-age=${SPEED_CACHE_TTL_SECONDS}`,
    "Content-Encoding": "identity",
    "Content-Length": SPEED_ASSET_BYTES.toString(),
    "Content-Type": "application/octet-stream",
    "Cross-Origin-Resource-Policy": "same-origin",
    "Timing-Allow-Origin": "*",
    "X-Content-Type-Options": "nosniff",
    "X-NDS-Payload": SPEED_PAYLOAD_MARKER,
    [SPEED_CACHED_AT_HEADER]: cachedAtSeconds.toString()
  });

  const etag = assetResponse.headers.get("ETag");
  const lastModified = assetResponse.headers.get("Last-Modified");
  if (etag) headers.set("ETag", etag);
  if (lastModified) headers.set("Last-Modified", lastModified);

  return new Response(assetResponse.body, { status: 200, headers });
}

export function createBrowserSpeedResponse(
  cachedResponse: Response,
  cacheStatus: "HIT" | "MISS" | "BYPASS",
  nowSeconds = Math.floor(Date.now() / 1000),
  headOnly = false
): Response {
  const headers = new Headers(cachedResponse.headers);
  const cachedAt = Number.parseInt(headers.get(SPEED_CACHED_AT_HEADER) ?? "", 10);

  headers.set("Cache-Control", "no-store, no-transform");
  headers.delete("Cloudflare-CDN-Cache-Control");
  headers.delete("CDN-Cache-Control");
  headers.delete("Expires");
  headers.set("Cross-Origin-Resource-Policy", "same-origin");
  headers.set("Strict-Transport-Security", "max-age=31536000");
  headers.set("Timing-Allow-Origin", "*");
  headers.set("X-Content-Type-Options", "nosniff");
  headers.set("X-NDS-Payload", SPEED_PAYLOAD_MARKER);
  headers.set("X-NDS-Cache-Status", cacheStatus);
  if (Number.isSafeInteger(cachedAt)) {
    headers.set("X-NDS-Cache-Age", Math.max(0, nowSeconds - cachedAt).toString());
  }
  headers.delete(SPEED_CACHED_AT_HEADER);

  return new Response(headOnly ? null : cachedResponse.body, {
    status: cachedResponse.status,
    statusText: cachedResponse.statusText,
    headers
  });
}

async function handleSpeedAsset(request: Request, env: Env): Promise<Response> {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return errorResponse("Method not allowed", 405, { Allow: "GET, HEAD" });
  }
  if (isCrossSite(request)) return errorResponse("Cross-site requests are not accepted", 403);

  const cache = (caches as CacheStorage & { default: Cache }).default;
  const lookupRequest = createSpeedCacheLookupRequest(request);

  try {
    const hit = await cache.match(lookupRequest);
    if (hit) return createBrowserSpeedResponse(hit, "HIT", undefined, request.method === "HEAD");
  } catch {
    // Cache API is unavailable in some local and preview environments.
  }

  const assetRequest = new Request(speedAssetUrl(request).toString(), {
    method: "GET",
    headers: { "Accept-Encoding": "identity" }
  });
  const assetResponse = await env.ASSETS.fetch(assetRequest);
  const declaredLength = Number.parseInt(assetResponse.headers.get("Content-Length") ?? "", 10);
  if (
    assetResponse.status !== 200
    || assetResponse.body === null
    || (Number.isFinite(declaredLength) && declaredLength !== SPEED_ASSET_BYTES)
  ) {
    await assetResponse.body?.cancel("invalid-speed-asset");
    return errorResponse("The static speed payload is unavailable", 502);
  }

  const cacheable = createCacheableSpeedResponse(assetResponse);
  try {
    await cache.put(createSpeedCacheKey(request), cacheable.clone());
    const stored = await cache.match(lookupRequest);
    if (stored) return createBrowserSpeedResponse(stored, "MISS", undefined, request.method === "HEAD");
  } catch {
    // Fall through to a direct response while reporting that local edge caching was bypassed.
  }

  return createBrowserSpeedResponse(cacheable, "BYPASS", undefined, request.method === "HEAD");
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
    if (url.pathname === SPEED_ASSET_PATH) return handleSpeedAsset(request, env);

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
        if (url.pathname.startsWith("/api/") || url.pathname.startsWith("/speed/v3/")) {
          return errorResponse("Not found", 404);
        }
        return env.ASSETS.fetch(request);
    }
  }
} satisfies { fetch(request: WorkerRequest, env: Env): Promise<Response> };

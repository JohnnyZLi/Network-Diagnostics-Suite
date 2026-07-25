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

interface ByteStreamPair {
  readable: ReadableStream<Uint8Array>;
  writable: WritableStream<Uint8Array>;
}

interface FixedLengthStreamConstructor {
  new(length: number | bigint): ByteStreamPair;
}

type WorkerRequest = Request & { cf?: CloudflareRequestProperties };
type SpeedCacheStatus = "HIT" | "MISS" | "BYPASS";

const DOWNLOAD_MIN_BYTES = 1_024;
const DOWNLOAD_MAX_BYTES = 32 * 1024 * 1024;
const UPLOAD_MAX_BYTES = 32 * 1024 * 1024;
const CHUNK_SIZE = 1024 * 1024;

export const SPEED_SEGMENT_BYTES = 24 * 1024 * 1024;
export const SPEED_SEGMENT_COUNT = 4;
export const SPEED_STREAM_BYTES = SPEED_SEGMENT_BYTES * SPEED_SEGMENT_COUNT;
export const SPEED_PATH_PREFIX = "/speed/v4/";
export const SPEED_STREAM_PATH = `${SPEED_PATH_PREFIX}stream`;
export const SPEED_WARM_PATH = `${SPEED_PATH_PREFIX}warm`;
export const SPEED_PAYLOAD_MARKER = "stream-edge-v4";
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

function speedUrl(request: Request, pathname: string): URL {
  const url = new URL(request.url);
  url.pathname = pathname;
  url.search = "";
  url.hash = "";
  return url;
}

export function speedSegmentPath(index: number): string {
  if (!Number.isInteger(index) || index < 0 || index >= SPEED_SEGMENT_COUNT) {
    throw new RangeError(`Speed segment index must be between 0 and ${SPEED_SEGMENT_COUNT - 1}.`);
  }
  return `${SPEED_PATH_PREFIX}segment-${index}.bin`;
}

export function createSpeedSegmentCacheKey(request: Request, index: number): Request {
  return new Request(speedUrl(request, speedSegmentPath(index)).toString(), { method: "GET" });
}

export function createCacheableSpeedSegmentResponse(
  assetResponse: Response,
  index: number,
  cachedAtSeconds = Math.floor(Date.now() / 1000)
): Response {
  const headers = new Headers({
    "Accept-Ranges": "bytes",
    "Cache-Control": `public, max-age=${SPEED_CACHE_TTL_SECONDS}`,
    "Content-Encoding": "identity",
    "Content-Length": SPEED_SEGMENT_BYTES.toString(),
    "Content-Type": "application/octet-stream",
    "Cross-Origin-Resource-Policy": "same-origin",
    "Timing-Allow-Origin": "*",
    "X-Content-Type-Options": "nosniff",
    "X-NDS-Segment": index.toString(),
    [SPEED_CACHED_AT_HEADER]: cachedAtSeconds.toString()
  });

  const etag = assetResponse.headers.get("ETag");
  const lastModified = assetResponse.headers.get("Last-Modified");
  if (etag) headers.set("ETag", etag);
  if (lastModified) headers.set("Last-Modified", lastModified);

  return new Response(assetResponse.body, { status: 200, headers });
}

interface LoadedSpeedSegment {
  response: Response;
  status: SpeedCacheStatus;
  ageSeconds: number | null;
}

function cachedAgeSeconds(response: Response, nowSeconds = Math.floor(Date.now() / 1000)): number | null {
  const cachedAt = Number.parseInt(response.headers.get(SPEED_CACHED_AT_HEADER) ?? "", 10);
  return Number.isSafeInteger(cachedAt) ? Math.max(0, nowSeconds - cachedAt) : null;
}

async function loadSpeedSegment(
  request: Request,
  env: Env,
  cache: Cache,
  index: number
): Promise<LoadedSpeedSegment> {
  const key = createSpeedSegmentCacheKey(request, index);
  try {
    const hit = await cache.match(key);
    if (hit) return { response: hit, status: "HIT", ageSeconds: cachedAgeSeconds(hit) };
  } catch {
    // Cache API is unavailable in some local and preview environments.
  }

  const assetRequest = new Request(speedUrl(request, speedSegmentPath(index)).toString(), {
    method: "GET",
    headers: { "Accept-Encoding": "identity" }
  });
  const assetResponse = await env.ASSETS.fetch(assetRequest);
  const declaredLength = Number.parseInt(assetResponse.headers.get("Content-Length") ?? "", 10);
  if (
    assetResponse.status !== 200
    || assetResponse.body === null
    || (Number.isFinite(declaredLength) && declaredLength !== SPEED_SEGMENT_BYTES)
  ) {
    await assetResponse.body?.cancel("invalid-speed-segment");
    throw new Error(`Static speed segment ${index} is unavailable.`);
  }

  const cacheable = createCacheableSpeedSegmentResponse(assetResponse, index);
  try {
    await cache.put(key, cacheable.clone());
    const stored = await cache.match(key);
    if (stored) return { response: stored, status: "MISS", ageSeconds: cachedAgeSeconds(stored) };
  } catch {
    // Fall through to direct asset delivery when the local Cache API cannot retain the segment.
  }

  return { response: cacheable, status: "BYPASS", ageSeconds: null };
}

function aggregateCacheStatus(segments: LoadedSpeedSegment[]): SpeedCacheStatus {
  if (segments.some((segment) => segment.status === "BYPASS")) return "BYPASS";
  if (segments.some((segment) => segment.status === "MISS")) return "MISS";
  return "HIT";
}

function aggregateCacheAge(segments: LoadedSpeedSegment[]): number | null {
  const ages = segments
    .map((segment) => segment.ageSeconds)
    .filter((age): age is number => age !== null);
  return ages.length > 0 ? Math.max(...ages) : null;
}

function createSpeedOutputStream(): ByteStreamPair {
  const FixedLengthStream = (
    globalThis as typeof globalThis & { FixedLengthStream?: FixedLengthStreamConstructor }
  ).FixedLengthStream;
  if (FixedLengthStream) return new FixedLengthStream(SPEED_STREAM_BYTES);
  return new TransformStream<Uint8Array, Uint8Array>();
}

async function pipeSpeedSegments(
  responses: Response[],
  writable: WritableStream<Uint8Array>
): Promise<void> {
  for (const [index, response] of responses.entries()) {
    if (!response.body) throw new Error(`Speed segment ${index} did not include a body.`);
    await response.body.pipeTo(writable, { preventClose: true });
  }

  const writer = writable.getWriter();
  try {
    await writer.close();
  } finally {
    writer.releaseLock();
  }
}

export function createConcatenatedBody(responses: Response[]): ReadableStream<Uint8Array> {
  const { readable, writable } = createSpeedOutputStream();
  void pipeSpeedSegments(responses, writable).catch(async (error) => {
    try {
      await writable.abort(error);
    } catch {
      // The stream may already be closed or aborted by pipeTo.
    }
  });
  return readable;
}

async function cancelSpeedResponses(responses: Response[]): Promise<void> {
  await Promise.all(responses.map(async (response) => {
    try {
      await response.body?.cancel("head-request-complete");
    } catch {
      // Ignore already-closed segment bodies.
    }
  }));
}

export function createBrowserSpeedStreamResponse(
  responses: Response[],
  cacheStatus: SpeedCacheStatus,
  ageSeconds: number | null,
  headOnly = false
): Response {
  const headers = new Headers({
    "Cache-Control": "no-store, no-transform",
    "Content-Encoding": "identity",
    "Content-Type": "application/octet-stream",
    "Cross-Origin-Resource-Policy": "same-origin",
    "Strict-Transport-Security": "max-age=31536000",
    "Timing-Allow-Origin": "*",
    "X-Content-Type-Options": "nosniff",
    "X-NDS-Cache-Status": cacheStatus,
    "X-NDS-Logical-Bytes": SPEED_STREAM_BYTES.toString(),
    "X-NDS-Payload": SPEED_PAYLOAD_MARKER,
    "X-NDS-Segment-Count": SPEED_SEGMENT_COUNT.toString(),
    "X-NDS-Stream-Mode": "fixed-length-pipe-v1"
  });
  if (ageSeconds !== null) headers.set("X-NDS-Cache-Age", ageSeconds.toString());

  if (headOnly) {
    void cancelSpeedResponses(responses);
    return new Response(null, { status: 200, headers });
  }
  return new Response(createConcatenatedBody(responses), { status: 200, headers });
}

async function handleSpeedWarm(request: Request, env: Env): Promise<Response> {
  if (request.method !== "POST") return errorResponse("Method not allowed", 405, { Allow: "POST" });
  if (isCrossSite(request)) return errorResponse("Cross-site requests are not accepted", 403);

  const cache = (caches as CacheStorage & { default: Cache }).default;
  let segments: LoadedSpeedSegment[];
  try {
    segments = await Promise.all(
      Array.from({ length: SPEED_SEGMENT_COUNT }, (_, index) => loadSpeedSegment(request, env, cache, index))
    );
  } catch (error) {
    return errorResponse(error instanceof Error ? error.message : "The speed payload could not be warmed.", 502);
  }

  const status = aggregateCacheStatus(segments);
  const ageSeconds = aggregateCacheAge(segments);
  await Promise.all(segments.map(async ({ response }) => {
    try {
      await response.body?.cancel("speed-warm-complete");
    } catch {
      // Ignore already-closed cache response bodies.
    }
  }));

  return Response.json({
    status,
    segmentCount: SPEED_SEGMENT_COUNT,
    cachedBytes: status === "BYPASS" ? 0 : SPEED_STREAM_BYTES,
    ageSeconds
  }, { headers: diagnosticHeaders("application/json; charset=utf-8") });
}

async function handleSpeedStream(request: Request, env: Env): Promise<Response> {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return errorResponse("Method not allowed", 405, { Allow: "GET, HEAD" });
  }
  if (isCrossSite(request)) return errorResponse("Cross-site requests are not accepted", 403);

  const cache = (caches as CacheStorage & { default: Cache }).default;
  let segments: LoadedSpeedSegment[];
  try {
    segments = await Promise.all(
      Array.from({ length: SPEED_SEGMENT_COUNT }, (_, index) => loadSpeedSegment(request, env, cache, index))
    );
  } catch (error) {
    return errorResponse(error instanceof Error ? error.message : "The speed stream is unavailable.", 502);
  }

  return createBrowserSpeedStreamResponse(
    segments.map((segment) => segment.response),
    aggregateCacheStatus(segments),
    aggregateCacheAge(segments),
    request.method === "HEAD"
  );
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
    if (url.pathname === SPEED_WARM_PATH) return handleSpeedWarm(request, env);
    if (url.pathname === SPEED_STREAM_PATH) return handleSpeedStream(request, env);

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
        if (url.pathname.startsWith("/api/") || url.pathname.startsWith(SPEED_PATH_PREFIX)) {
          return errorResponse("Not found", 404);
        }
        return env.ASSETS.fetch(request);
    }
  }
} satisfies { fetch(request: WorkerRequest, env: Env): Promise<Response> };

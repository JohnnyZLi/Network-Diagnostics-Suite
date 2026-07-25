import type { EdgeMetadata, UploadReceipt } from "../types/api";
import type { DownloadStreamRejection } from "../types/diagnostics";

export const STATIC_DOWNLOAD_STREAM_BYTES = 96 * 1024 * 1024;
export const STATIC_DOWNLOAD_SEGMENT_COUNT = 4;
export const STATIC_DOWNLOAD_PATH_PREFIX = "/speed/v4/";
export const R2_DOWNLOAD_ORIGIN = "https://speed.johnnyli.dev";
export const R2_DOWNLOAD_OBJECT_PATH = "/network-diagnostics-speed-v1.bin";
export const R2_DOWNLOAD_OBJECT_BYTES = 256 * 1024 * 1024;
export const R2_DOWNLOAD_RANGE_BYTES = 192 * 1024 * 1024;

const STATIC_DOWNLOAD_STREAM = `${STATIC_DOWNLOAD_PATH_PREFIX}stream`;
const STATIC_DOWNLOAD_WARM = `${STATIC_DOWNLOAD_PATH_PREFIX}warm`;
const STATIC_PAYLOAD_MARKER = "stream-edge-v4";
const WORKER_FALLBACK_BYTES = 32 * 1024 * 1024;
const R2_RANGE_STRIDE_BYTES = 6 * 1024 * 1024;
const R2_RANGE_SLOT_COUNT = 10;

export interface DownloadDeliveryObservation {
  source: "r2" | "static" | "worker";
  cacheStatus: string | null;
  ageSeconds: number | null;
}

export interface DownloadWarmupObservation extends DownloadDeliveryObservation {
  bytes: number;
  cachedBytes: number;
}

export interface R2DownloadProbe {
  available: boolean;
  reason: string | null;
  warmup: DownloadWarmupObservation;
}

interface WarmupReceipt {
  status: string;
  segmentCount: number;
  cachedBytes: number;
  ageSeconds: number | null;
}

interface ParsedContentRange {
  start: number;
  end: number;
  total: number;
}

class IncompleteDownloadError extends Error {
  constructor(
    readonly receivedBytes: number,
    readonly expectedBytes: number,
    readonly streamFailed: boolean
  ) {
    super(`Download endpoint returned ${receivedBytes} bytes; expected ${expectedBytes}.`);
    this.name = "IncompleteDownloadError";
  }
}

export class TestCancelledError extends Error {
  constructor() {
    super("The diagnostic test was cancelled.");
    this.name = "TestCancelledError";
  }
}

export function throwIfAborted(signal: AbortSignal): void {
  if (signal.aborted) throw new TestCancelledError();
}

export function createTimedSignal(parent: AbortSignal, timeoutMs: number): {
  signal: AbortSignal;
  dispose: () => void;
} {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort("timeout"), timeoutMs);
  const forwardAbort = () => controller.abort(parent.reason);
  parent.addEventListener("abort", forwardAbort, { once: true });

  return {
    signal: controller.signal,
    dispose: () => {
      window.clearTimeout(timer);
      parent.removeEventListener("abort", forwardAbort);
    }
  };
}

export async function sleep(ms: number, signal: AbortSignal): Promise<void> {
  throwIfAborted(signal);
  await new Promise<void>((resolve, reject) => {
    const timer = window.setTimeout(() => {
      signal.removeEventListener("abort", onAbort);
      resolve();
    }, ms);
    const onAbort = () => {
      window.clearTimeout(timer);
      reject(new TestCancelledError());
    };
    signal.addEventListener("abort", onAbort, { once: true });
  });
}

export async function measurePing(signal: AbortSignal, timeoutMs = 1_500): Promise<number | null> {
  throwIfAborted(signal);
  const timed = createTimedSignal(signal, timeoutMs);
  const started = performance.now();
  try {
    const response = await fetch(`/api/ping?n=${crypto.randomUUID()}`, {
      cache: "no-store",
      credentials: "omit",
      signal: timed.signal
    });
    if (!response.ok) return null;
    await response.text();
    return performance.now() - started;
  } catch (error) {
    if (signal.aborted) throw new TestCancelledError();
    if (error instanceof DOMException && error.name === "AbortError") return null;
    return null;
  } finally {
    timed.dispose();
  }
}

export async function fetchMetadata(signal: AbortSignal): Promise<EdgeMetadata | null> {
  const timed = createTimedSignal(signal, 3_000);
  try {
    const response = await fetch("/api/meta", {
      cache: "no-store",
      credentials: "omit",
      signal: timed.signal
    });
    if (!response.ok) return null;
    return await response.json() as EdgeMetadata;
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    return null;
  } finally {
    timed.dispose();
  }
}

function parseNonNegativeInteger(value: string | null): number | null {
  if (value === null || !/^\d+$/.test(value)) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

function parseContentRange(value: string | null): ParsedContentRange | null {
  const match = /^bytes\s+(\d+)-(\d+)\/(\d+)$/i.exec(value ?? "");
  if (!match) return null;
  const start = Number.parseInt(match[1], 10);
  const end = Number.parseInt(match[2], 10);
  const total = Number.parseInt(match[3], 10);
  if (![start, end, total].every(Number.isSafeInteger) || start < 0 || end < start || total <= end) return null;
  return { start, end, total };
}

function deliveryObservation(response: Response, source: DownloadDeliveryObservation["source"]): DownloadDeliveryObservation {
  const cacheStatus = (
    response.headers.get("X-NDS-Cache-Status")
    ?? response.headers.get("CF-Cache-Status")
  )?.trim().toUpperCase() || null;
  const ageSeconds = parseNonNegativeInteger(
    response.headers.get("X-NDS-Cache-Age")
    ?? response.headers.get("Age")
  );
  return { source, cacheStatus, ageSeconds };
}

function streamSnapshot(
  response: Response
): Omit<DownloadStreamRejection, "reason" | "receivedBytes"> {
  return {
    status: response.status,
    marker: response.headers.get("X-NDS-Payload"),
    logicalBytes: parseNonNegativeInteger(response.headers.get("X-NDS-Logical-Bytes")),
    segmentCount: parseNonNegativeInteger(response.headers.get("X-NDS-Segment-Count")),
    contentLength: parseNonNegativeInteger(response.headers.get("Content-Length")),
    contentRange: response.headers.get("Content-Range")
  };
}

export function inspectStreamResponse(response: Response): DownloadStreamRejection | null {
  const snapshot = streamSnapshot(response);
  if (response.status !== 200) return { reason: "status", ...snapshot, receivedBytes: null };
  if (response.body === null) return { reason: "missing-body", ...snapshot, receivedBytes: null };
  if (snapshot.marker !== STATIC_PAYLOAD_MARKER) {
    return { reason: "wrong-marker", ...snapshot, receivedBytes: null };
  }
  if (snapshot.logicalBytes !== STATIC_DOWNLOAD_STREAM_BYTES) {
    return { reason: "wrong-logical-size", ...snapshot, receivedBytes: null };
  }
  if (snapshot.segmentCount !== STATIC_DOWNLOAD_SEGMENT_COUNT) {
    return { reason: "wrong-segment-count", ...snapshot, receivedBytes: null };
  }

  const contentLengthHeader = response.headers.get("Content-Length");
  if (contentLengthHeader !== null && snapshot.contentLength !== STATIC_DOWNLOAD_STREAM_BYTES) {
    return { reason: "wrong-content-length", ...snapshot, receivedBytes: null };
  }
  return null;
}

export function streamResponseMatchesRequest(response: Response): boolean {
  return inspectStreamResponse(response) === null;
}

async function discardResponse(response: Response): Promise<void> {
  try {
    await response.body?.cancel("response-not-usable-for-speed-test");
  } catch {
    // The response may already be closed. There is nothing else to clean up.
  }
}

async function consumeDownload(
  response: Response,
  expectedBytes: number,
  onBytes: (delta: number) => void
): Promise<void> {
  if (!response.body) throw new IncompleteDownloadError(0, expectedBytes, true);

  let receivedBytes = 0;
  const reader = response.body.getReader();
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      receivedBytes += value.byteLength;
      onBytes(value.byteLength);
    }
  } catch {
    throw new IncompleteDownloadError(receivedBytes, expectedBytes, true);
  }

  if (receivedBytes !== expectedBytes) {
    throw new IncompleteDownloadError(receivedBytes, expectedBytes, false);
  }
}

async function downloadWorkerChunk(
  size: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void,
  onObservation?: (observation: DownloadDeliveryObservation) => void
): Promise<void> {
  const response = await fetch(`/api/download?bytes=${size}&n=${crypto.randomUUID()}`, {
    cache: "no-store",
    credentials: "omit",
    signal
  });
  if (!response.ok || !response.body) throw new Error(`Download endpoint returned ${response.status}.`);
  onObservation?.(deliveryObservation(response, "worker"));
  await consumeDownload(response, size, onBytes);
}

export async function downloadLongStream(
  signal: AbortSignal,
  onBytes: (delta: number) => void,
  onObservation: (observation: DownloadDeliveryObservation) => void,
  onRejection: (rejection: DownloadStreamRejection) => void
): Promise<void> {
  let response: Response;
  try {
    response = await fetch(STATIC_DOWNLOAD_STREAM, {
      cache: "no-store",
      credentials: "omit",
      signal
    });
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    onRejection({
      reason: "fetch-error",
      status: 0,
      marker: null,
      logicalBytes: null,
      segmentCount: null,
      contentLength: null,
      contentRange: null,
      receivedBytes: null
    });
    await downloadWorkerChunk(WORKER_FALLBACK_BYTES, signal, onBytes, onObservation);
    return;
  }

  const rejection = inspectStreamResponse(response);
  if (rejection) {
    onRejection(rejection);
    await discardResponse(response);
    await downloadWorkerChunk(WORKER_FALLBACK_BYTES, signal, onBytes, onObservation);
    return;
  }

  const snapshot = streamSnapshot(response);
  onObservation(deliveryObservation(response, "static"));
  try {
    await consumeDownload(response, STATIC_DOWNLOAD_STREAM_BYTES, onBytes);
  } catch (error) {
    if (signal.aborted) throw new TestCancelledError();
    if (error instanceof IncompleteDownloadError) {
      onRejection({
        reason: error.streamFailed ? "stream-error" : "truncated-body",
        ...snapshot,
        receivedBytes: error.receivedBytes
      });
      await downloadWorkerChunk(WORKER_FALLBACK_BYTES, signal, onBytes, onObservation);
      return;
    }
    throw error;
  }
}

function r2Range(workerIndex: number, generation: number): { start: number; end: number } {
  const slot = (workerIndex + generation * 3) % R2_RANGE_SLOT_COUNT;
  const start = slot * R2_RANGE_STRIDE_BYTES;
  return { start, end: start + R2_DOWNLOAD_RANGE_BYTES - 1 };
}

export async function probeR2DownloadPath(signal: AbortSignal): Promise<R2DownloadProbe> {
  const timed = createTimedSignal(signal, 4_000);
  try {
    const response = await fetch(`${R2_DOWNLOAD_ORIGIN}${R2_DOWNLOAD_OBJECT_PATH}`, {
      method: "HEAD",
      credentials: "omit",
      signal: timed.signal
    });
    const contentLength = parseNonNegativeInteger(response.headers.get("Content-Length"));
    const available = response.ok && contentLength === R2_DOWNLOAD_OBJECT_BYTES;
    return {
      available,
      reason: available
        ? null
        : `R2 probe returned ${response.status} with ${contentLength ?? "unknown"} bytes.`,
      warmup: {
        ...deliveryObservation(response, "r2"),
        bytes: 0,
        cachedBytes: available ? R2_DOWNLOAD_OBJECT_BYTES : 0
      }
    };
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    return {
      available: false,
      reason: "The direct R2 hostname could not be reached with the required CORS policy.",
      warmup: {
        source: "r2",
        cacheStatus: null,
        ageSeconds: null,
        bytes: 0,
        cachedBytes: 0
      }
    };
  } finally {
    timed.dispose();
  }
}

export async function downloadR2Range(
  workerIndex: number,
  generation: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void,
  onObservation: (observation: DownloadDeliveryObservation) => void,
  onRejection: (rejection: DownloadStreamRejection) => void
): Promise<boolean> {
  const range = r2Range(workerIndex, generation);
  let response: Response;
  try {
    response = await fetch(`${R2_DOWNLOAD_ORIGIN}${R2_DOWNLOAD_OBJECT_PATH}`, {
      credentials: "omit",
      headers: { Range: `bytes=${range.start}-${range.end}` },
      signal
    });
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    onRejection({
      reason: "r2-fetch-error",
      status: 0,
      marker: null,
      logicalBytes: R2_DOWNLOAD_OBJECT_BYTES,
      segmentCount: null,
      contentLength: null,
      contentRange: null,
      receivedBytes: null
    });
    return false;
  }

  const snapshot = streamSnapshot(response);
  const parsedRange = parseContentRange(response.headers.get("Content-Range"));
  const contentLengthHeader = response.headers.get("Content-Length");
  const validLength = contentLengthHeader === null || snapshot.contentLength === R2_DOWNLOAD_RANGE_BYTES;
  if (response.status !== 206) {
    onRejection({ reason: "r2-status", ...snapshot, receivedBytes: null });
    await discardResponse(response);
    return false;
  }
  if (response.body === null) {
    onRejection({ reason: "r2-missing-body", ...snapshot, receivedBytes: null });
    return false;
  }
  if (!validLength) {
    onRejection({ reason: "r2-wrong-size", ...snapshot, receivedBytes: null });
    await discardResponse(response);
    return false;
  }
  if (
    parsedRange === null
    || parsedRange.start !== range.start
    || parsedRange.end !== range.end
    || parsedRange.total !== R2_DOWNLOAD_OBJECT_BYTES
  ) {
    onRejection({ reason: "r2-wrong-range", ...snapshot, receivedBytes: null });
    await discardResponse(response);
    return false;
  }

  onObservation(deliveryObservation(response, "r2"));
  try {
    await consumeDownload(response, R2_DOWNLOAD_RANGE_BYTES, onBytes);
    return true;
  } catch (error) {
    if (signal.aborted) throw new TestCancelledError();
    if (error instanceof IncompleteDownloadError) {
      onRejection({
        reason: error.streamFailed ? "r2-stream-error" : "r2-truncated-body",
        ...snapshot,
        receivedBytes: error.receivedBytes
      });
      return false;
    }
    throw error;
  }
}

async function warmStaticDownloadPath(signal: AbortSignal): Promise<DownloadWarmupObservation | null> {
  try {
    const response = await fetch(STATIC_DOWNLOAD_WARM, {
      method: "POST",
      cache: "no-store",
      credentials: "omit",
      signal
    });
    if (!response.ok) return null;
    const receipt = await response.json() as WarmupReceipt;
    if (
      !Number.isSafeInteger(receipt.segmentCount)
      || receipt.segmentCount !== STATIC_DOWNLOAD_SEGMENT_COUNT
      || !Number.isSafeInteger(receipt.cachedBytes)
      || receipt.cachedBytes < 0
    ) return null;
    return {
      source: "static",
      cacheStatus: receipt.status?.trim().toUpperCase() || null,
      ageSeconds: Number.isFinite(receipt.ageSeconds) ? receipt.ageSeconds : null,
      bytes: 0,
      cachedBytes: receipt.cachedBytes
    };
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    return null;
  }
}

export async function warmDownloadPath(signal: AbortSignal, concurrency: number): Promise<DownloadWarmupObservation> {
  const staticWarmup = await warmStaticDownloadPath(signal);
  if (staticWarmup) return staticWarmup;

  const streamCount = Math.max(1, Math.min(4, concurrency));
  const bytesPerStream = 2 * 1024 * 1024;
  const observations: DownloadDeliveryObservation[] = [];
  await Promise.all(
    Array.from({ length: streamCount }, () => downloadWorkerChunk(
      bytesPerStream,
      signal,
      () => undefined,
      (observation) => observations.push(observation)
    ))
  );
  const cacheStatus = observations.map((observation) => observation.cacheStatus).find((status) => status !== null) ?? null;
  const ageSeconds = observations.reduce<number | null>((maximum, observation) => {
    if (observation.ageSeconds === null) return maximum;
    return maximum === null ? observation.ageSeconds : Math.max(maximum, observation.ageSeconds);
  }, null);
  return {
    source: "worker",
    cacheStatus,
    ageSeconds,
    bytes: bytesPerStream * streamCount,
    cachedBytes: 0
  };
}

export function uploadChunk(
  size: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void
): Promise<UploadReceipt> {
  return new Promise((resolve, reject) => {
    throwIfAborted(signal);
    const xhr = new XMLHttpRequest();
    const payload = new Uint8Array(size);
    let reportedBytes = 0;

    const cleanup = () => signal.removeEventListener("abort", onAbort);
    const onAbort = () => xhr.abort();
    signal.addEventListener("abort", onAbort, { once: true });

    xhr.open("POST", `/api/upload?n=${crypto.randomUUID()}`);
    xhr.responseType = "json";
    xhr.timeout = 15_000;
    xhr.setRequestHeader("Content-Type", "application/octet-stream");
    xhr.upload.onprogress = (event) => {
      const delta = Math.max(0, event.loaded - reportedBytes);
      reportedBytes = event.loaded;
      onBytes(delta);
    };
    xhr.onload = () => {
      cleanup();
      if (xhr.status < 200 || xhr.status >= 300) {
        reject(new Error(`Upload endpoint returned ${xhr.status}.`));
        return;
      }
      if (reportedBytes < size) onBytes(size - reportedBytes);
      resolve((xhr.response ?? { bytes: size }) as UploadReceipt);
    };
    xhr.onerror = () => {
      cleanup();
      reject(new Error("The upload endpoint could not be reached."));
    };
    xhr.ontimeout = () => {
      cleanup();
      reject(new Error("The upload request timed out."));
    };
    xhr.onabort = () => {
      cleanup();
      reject(new TestCancelledError());
    };
    xhr.send(payload);
  });
}

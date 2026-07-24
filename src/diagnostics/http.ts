import type { EdgeMetadata, UploadReceipt } from "../types/api";

export const STATIC_DOWNLOAD_ASSET_BYTES = 24 * 1024 * 1024;
export const STATIC_DOWNLOAD_PATH_PREFIX = "/speed/v3/";

const STATIC_DOWNLOAD_ASSET = `${STATIC_DOWNLOAD_PATH_PREFIX}payload.bin`;
const STATIC_PAYLOAD_MARKER = "static-edge-v3";

export interface DownloadDeliveryObservation {
  source: "static" | "worker";
  cacheStatus: string | null;
  ageSeconds: number | null;
}

export interface DownloadWarmupObservation extends DownloadDeliveryObservation {
  bytes: number;
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

function isMarkedStaticPayload(response: Response): boolean {
  return response.headers.get("X-NDS-Payload") === STATIC_PAYLOAD_MARKER;
}

function parseContentRangeLength(value: string | null): number | null {
  const match = /^bytes\s+(\d+)-(\d+)\/(?:\d+|\*)$/i.exec(value ?? "");
  if (!match) return null;
  const start = Number.parseInt(match[1], 10);
  const end = Number.parseInt(match[2], 10);
  if (!Number.isSafeInteger(start) || !Number.isSafeInteger(end) || end < start) return null;
  return end - start + 1;
}

function parseNonNegativeInteger(value: string | null): number | null {
  if (value === null || !/^\d+$/.test(value)) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isSafeInteger(parsed) ? parsed : null;
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

export function staticResponseMatchesRequest(response: Response, requestedBytes: number): boolean {
  if (!isMarkedStaticPayload(response) || response.body === null) return false;

  const contentLength = Number.parseInt(response.headers.get("Content-Length") ?? "", 10);
  if (response.status === 206) {
    return parseContentRangeLength(response.headers.get("Content-Range")) === requestedBytes
      && contentLength === requestedBytes;
  }

  return response.status === 200
    && requestedBytes === STATIC_DOWNLOAD_ASSET_BYTES
    && contentLength === STATIC_DOWNLOAD_ASSET_BYTES;
}

async function discardResponse(response: Response): Promise<void> {
  try {
    await response.body?.cancel("response-not-usable-for-requested-range");
  } catch {
    // The response may already be closed. There is nothing else to clean up.
  }
}

async function consumeDownload(
  response: Response,
  expectedBytes: number,
  onBytes: (delta: number) => void
): Promise<void> {
  if (!response.body) throw new Error("Download response did not include a body.");

  let receivedBytes = 0;
  const reader = response.body.getReader();
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    receivedBytes += value.byteLength;
    onBytes(value.byteLength);
  }

  if (receivedBytes !== expectedBytes) {
    throw new Error(`Download endpoint returned ${receivedBytes} bytes; expected ${expectedBytes}.`);
  }
}

async function downloadStaticAsset(
  size: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void
): Promise<DownloadDeliveryObservation | null> {
  let response: Response;
  try {
    response = await fetch(STATIC_DOWNLOAD_ASSET, {
      credentials: "omit",
      headers: size === STATIC_DOWNLOAD_ASSET_BYTES ? undefined : {
        Range: `bytes=0-${size - 1}`
      },
      signal
    });
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    return null;
  }

  if (!staticResponseMatchesRequest(response, size)) {
    await discardResponse(response);
    return null;
  }

  const observation = deliveryObservation(response, "static");
  await consumeDownload(response, size, onBytes);
  return observation;
}

async function downloadWorkerChunk(
  size: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void
): Promise<DownloadDeliveryObservation> {
  const response = await fetch(`/api/download?bytes=${size}&n=${crypto.randomUUID()}`, {
    cache: "no-store",
    credentials: "omit",
    signal
  });
  if (!response.ok || !response.body) throw new Error(`Download endpoint returned ${response.status}.`);
  const observation = deliveryObservation(response, "worker");
  await consumeDownload(response, size, onBytes);
  return observation;
}

export async function downloadChunk(
  size: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void
): Promise<DownloadDeliveryObservation> {
  if (size <= 0 || size > STATIC_DOWNLOAD_ASSET_BYTES) {
    throw new Error(`Download chunk size must be between 1 and ${STATIC_DOWNLOAD_ASSET_BYTES} bytes.`);
  }

  const staticObservation = await downloadStaticAsset(size, signal, onBytes);
  if (staticObservation) return staticObservation;
  return downloadWorkerChunk(size, signal, onBytes);
}

async function warmStaticDownloadPath(signal: AbortSignal): Promise<DownloadWarmupObservation | null> {
  let response: Response;
  try {
    response = await fetch(STATIC_DOWNLOAD_ASSET, {
      credentials: "omit",
      signal
    });
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    return null;
  }

  if (!staticResponseMatchesRequest(response, STATIC_DOWNLOAD_ASSET_BYTES)) {
    await discardResponse(response);
    return null;
  }

  const observation = deliveryObservation(response, "static");
  await consumeDownload(response, STATIC_DOWNLOAD_ASSET_BYTES, () => undefined);
  return { ...observation, bytes: STATIC_DOWNLOAD_ASSET_BYTES };
}

export async function warmDownloadPath(signal: AbortSignal, concurrency: number): Promise<DownloadWarmupObservation> {
  const staticWarmup = await warmStaticDownloadPath(signal);
  if (staticWarmup) return staticWarmup;

  const streamCount = Math.max(1, Math.min(4, concurrency));
  const bytesPerStream = 2 * 1024 * 1024;
  const observations = await Promise.all(
    Array.from({ length: streamCount }, () => downloadWorkerChunk(bytesPerStream, signal, () => undefined))
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
    bytes: bytesPerStream * streamCount
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

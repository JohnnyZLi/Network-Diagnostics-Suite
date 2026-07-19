import type { EdgeMetadata, UploadReceipt } from "../types/api";

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

export async function downloadChunk(
  size: number,
  signal: AbortSignal,
  onBytes: (delta: number) => void
): Promise<void> {
  const response = await fetch(`/api/download?bytes=${size}&n=${crypto.randomUUID()}`, {
    cache: "no-store",
    credentials: "omit",
    signal
  });
  if (!response.ok || !response.body) throw new Error(`Download endpoint returned ${response.status}.`);

  const reader = response.body.getReader();
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    onBytes(value.byteLength);
  }
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

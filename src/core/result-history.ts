import type { DiagnosticResult } from "../types/diagnostics";

export const MAX_RECENT_RESULTS = 12;
const STORAGE_KEY = "network-diagnostics.recent-results.v1";

type ResultStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isDiagnosticResult(value: unknown): value is DiagnosticResult {
  if (!isRecord(value)) return false;
  if (typeof value.id !== "string" || typeof value.startedAt !== "string" || typeof value.completedAt !== "string") return false;
  if (!isRecord(value.download) || !isRecord(value.upload) || !isRecord(value.downloadLatency)) return false;
  return Number.isFinite(value.download.steadyMbps)
    && Number.isFinite(value.download.stabilityPercent)
    && Number.isFinite(value.upload.steadyMbps);
}

function defaultStorage(): ResultStorage | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

export function loadRecentResults(storage: ResultStorage | null = defaultStorage()): DiagnosticResult[] {
  if (!storage) return [];
  try {
    const raw = storage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter(isDiagnosticResult)
      .sort((left, right) => right.startedAt.localeCompare(left.startedAt))
      .slice(0, MAX_RECENT_RESULTS);
  } catch {
    return [];
  }
}

export function saveRecentResult(
  result: DiagnosticResult,
  storage: ResultStorage | null = defaultStorage()
): DiagnosticResult[] {
  const next = [
    result,
    ...loadRecentResults(storage).filter((candidate) => candidate.id !== result.id)
  ]
    .sort((left, right) => right.startedAt.localeCompare(left.startedAt))
    .slice(0, MAX_RECENT_RESULTS);

  if (storage) {
    try {
      storage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Browsers may deny or exhaust local storage. The current result still remains in memory.
    }
  }
  return next;
}

export function clearRecentResults(storage: ResultStorage | null = defaultStorage()): void {
  if (!storage) return;
  try {
    storage.removeItem(STORAGE_KEY);
  } catch {
    // Clearing history is best-effort when storage is unavailable.
  }
}

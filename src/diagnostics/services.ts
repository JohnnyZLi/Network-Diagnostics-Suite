import type { ServiceCheckResult } from "../types/diagnostics";
import { createTimedSignal, TestCancelledError } from "./http";

interface ServiceTarget {
  id: string;
  name: string;
  url: string;
}

const SERVICE_TARGETS: ServiceTarget[] = [
  { id: "cloudflare", name: "Cloudflare", url: "https://www.cloudflare.com/cdn-cgi/trace" },
  { id: "google", name: "Google", url: "https://www.google.com/generate_204" },
  { id: "microsoft", name: "Microsoft", url: "https://www.microsoft.com/favicon.ico" },
  { id: "github", name: "GitHub", url: "https://github.githubassets.com/favicons/favicon.svg" },
  { id: "apple", name: "Apple", url: "https://www.apple.com/library/test/success.html" },
  { id: "amazon", name: "Amazon", url: "https://www.amazon.com/favicon.ico" }
];

async function checkService(target: ServiceTarget, signal: AbortSignal): Promise<ServiceCheckResult> {
  const timed = createTimedSignal(signal, 4_000);
  const started = performance.now();
  try {
    await fetch(`${target.url}${target.url.includes("?") ? "&" : "?"}n=${crypto.randomUUID()}`, {
      mode: "no-cors",
      cache: "no-store",
      credentials: "omit",
      referrerPolicy: "no-referrer",
      signal: timed.signal
    });
    return {
      id: target.id,
      name: target.name,
      reachable: true,
      durationMs: performance.now() - started,
      note: "Opaque browser request; reachability only"
    };
  } catch {
    if (signal.aborted) throw new TestCancelledError();
    return {
      id: target.id,
      name: target.name,
      reachable: false,
      durationMs: null,
      note: "No response before the browser timeout"
    };
  } finally {
    timed.dispose();
  }
}

export function runServiceBattery(signal: AbortSignal): Promise<ServiceCheckResult[]> {
  return Promise.all(SERVICE_TARGETS.map((target) => checkService(target, signal)));
}

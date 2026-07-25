import type { ServiceCheckResult } from "../types/diagnostics";
import { createTimedSignal, TestCancelledError } from "./http";

interface ServiceTarget {
  id: string;
  name: string;
  url: string;
  requestMode: "same-origin" | "no-cors";
  successNote: string;
}

const SERVICE_TARGETS: ServiceTarget[] = [
  {
    id: "cloudflare",
    name: "Cloudflare",
    url: "/api/ping",
    requestMode: "same-origin",
    successNote: "First-party Cloudflare Worker request"
  },
  {
    id: "google",
    name: "Google",
    url: "https://www.google.com/generate_204",
    requestMode: "no-cors",
    successNote: "Opaque browser request; reachability only"
  },
  {
    id: "microsoft",
    name: "Microsoft",
    url: "https://www.microsoft.com/favicon.ico",
    requestMode: "no-cors",
    successNote: "Opaque browser request; reachability only"
  },
  {
    id: "github",
    name: "GitHub",
    url: "https://github.githubassets.com/favicons/favicon.svg",
    requestMode: "no-cors",
    successNote: "Opaque browser request; reachability only"
  },
  {
    id: "apple",
    name: "Apple",
    url: "https://www.apple.com/library/test/success.html",
    requestMode: "no-cors",
    successNote: "Opaque browser request; reachability only"
  },
  {
    id: "amazon",
    name: "Amazon",
    url: "https://www.amazon.com/favicon.ico",
    requestMode: "no-cors",
    successNote: "Opaque browser request; reachability only"
  }
];

async function checkService(target: ServiceTarget, signal: AbortSignal): Promise<ServiceCheckResult> {
  const timed = createTimedSignal(signal, 4_000);
  const started = performance.now();
  try {
    await fetch(`${target.url}${target.url.includes("?") ? "&" : "?"}n=${crypto.randomUUID()}`, {
      mode: target.requestMode,
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
      note: target.successNote
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

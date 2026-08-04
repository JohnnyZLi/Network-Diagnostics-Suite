import { afterEach, describe, expect, it, vi } from "vitest";
import { selectMeasurementEndpoint, type MeasurementEndpointDefinition } from "../src/diagnostics/endpoints";

const endpoints: MeasurementEndpointDefinition[] = [
  { id: "slow", name: "Slow", provider: "Test", origin: "https://slow.example/" },
  { id: "fast", name: "Fast", provider: "Test", origin: "https://fast.example/" }
];

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("browser endpoint preflight", () => {
  it("selects the lowest-median available endpoint and retains all evidence", async () => {
    let now = 0;
    vi.spyOn(performance, "now").mockImplementation(() => now);
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      now += String(input).includes("fast.example") ? 10 : 50;
      return new Response("ok", { status: 200 });
    }));

    const selected = await selectMeasurementEndpoint(endpoints, new AbortController().signal);

    expect(selected.endpoint.id).toBe("fast");
    expect(selected.probes).toHaveLength(2);
    expect(selected.probes.every((probe) => probe.available)).toBe(true);
  });

  it("keeps an unavailable candidate in the evidence instead of hiding it", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).includes("slow.example")) return new Response("down", { status: 503 });
      return new Response("ok", { status: 200 });
    }));

    const selected = await selectMeasurementEndpoint(endpoints, new AbortController().signal);

    expect(selected.endpoint.id).toBe("fast");
    expect(selected.probes.find((probe) => probe.id === "slow")).toMatchObject({
      available: false,
      error: "HTTP 503"
    });
  });
});

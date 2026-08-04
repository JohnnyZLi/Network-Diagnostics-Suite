import { describe, expect, it } from "vitest";
import {
  combinedReportHasDeepDiagnostics,
  isCombinedReport,
  isDeepProbeReport,
} from "../src/report-compatibility";

const baseRun = {
  id: "11111111-1111-1111-1111-111111111111",
  platform: "macOS",
  architecture: "Arm64",
  profile: "connection-check",
  transferMethod: "compare",
  startedAt: "2026-08-03T00:00:00Z",
  completedAt: "2026-08-03T00:00:15Z",
  includesLocalAddresses: false,
};

describe("report compatibility guards", () => {
  it("accepts a desktop connection-check report without deep diagnostics", () => {
    const report = {
      schemaVersion: "2.0",
      generatedAt: "2026-08-03T00:00:15Z",
      run: baseRun,
      internetTransfer: {
        origin: "https://network.johnnyli.dev/",
      },
      deepDiagnostics: null,
      futureOptionalSection: { safe: true },
    };

    expect(isCombinedReport(report)).toBe(true);
    if (!isCombinedReport(report)) throw new Error("expected combined report");
    expect(combinedReportHasDeepDiagnostics(report)).toBe(false);
  });

  it("accepts a full desktop report with embedded deep diagnostics", () => {
    const deep = {
      schemaVersion: "1.2",
      generatedAt: "2026-08-03T00:00:30Z",
      target: "1.1.1.1",
      operatingSystem: "macOS",
      architecture: "Arm64",
      includesLocalAddresses: false,
      interfaces: [],
      internetPing: { label: "Internet", statistics: { sent: 0, received: 0, lost: 0, lossPercent: 0, samples: [] } },
      traceRoute: { target: "1.1.1.1", maximumHops: 30, reachedDestination: false, hops: [] },
      dnsResolvers: [],
      pathMtu: { target: "1.1.1.1", status: "not measured" },
      serviceEndpoints: [],
    };
    const report = {
      schemaVersion: "2.0",
      generatedAt: "2026-08-03T00:00:30Z",
      run: { ...baseRun, profile: "standard" },
      deepDiagnostics: deep,
    };

    expect(isCombinedReport(report)).toBe(true);
    if (!isCombinedReport(report)) throw new Error("expected combined report");
    expect(combinedReportHasDeepDiagnostics(report)).toBe(true);
    expect(isDeepProbeReport(deep)).toBe(true);
  });

  it("rejects unsupported schemas and invalid profile identifiers", () => {
    expect(isCombinedReport({ schemaVersion: "3.0", run: baseRun })).toBe(false);
    expect(
      isCombinedReport({
        schemaVersion: "2.0",
        run: { ...baseRun, profile: "turbo" },
      }),
    ).toBe(false);
  });
});

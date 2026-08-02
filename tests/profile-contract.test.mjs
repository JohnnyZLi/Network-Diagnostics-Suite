import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { TEST_MODES } from "../src/diagnostics/config";
import { buildDiagnosticTestPlan } from "../src/diagnostics/flow-plan";

const contract = JSON.parse(
  readFileSync(new URL("../contracts/test-profiles.v1.json", import.meta.url), "utf8")
);

describe("shared test-profile contract", () => {
  it("matches the browser configuration", () => {
    expect(contract.schemaVersion).toBe("1.0");
    expect(contract.transferMethods).toEqual(["compare", "single", "aggregate"]);

    for (const profile of contract.profiles) {
      const config = TEST_MODES[profile.id];
      expect(config.name).toBe(profile.name);
      expect(Number.parseInt(config.estimatedTime, 10)).toBe(profile.estimatedSeconds);
      expect(config.idlePingCount).toBe(profile.idlePingCount);
      expect(config.pingIntervalMs).toBe(profile.pingIntervalMs);
      expect(config.downloadDurationMs).toBe(profile.downloadDurationMs);
      expect(config.downloadCapBytes).toBe(profile.downloadCapBytes);
      expect(config.downloadSamples).toBe(profile.downloadSamples);
      expect(config.uploadDurationMs).toBe(profile.uploadDurationMs);
      expect(config.uploadCapBytes).toBe(profile.uploadCapBytes);
      expect(config.concurrency).toBe(profile.aggregateDownloadConnections);
      expect(config.uploadConcurrency).toBe(profile.aggregateUploadConnections);
      expect(config.includeServices).toBe(profile.includeServices);
      expect(config.comparisonSingleDownloadDurationMs).toBe(profile.comparison.singleDownloadDurationMs);
      expect(config.comparisonSingleDownloadCapBytes).toBe(profile.comparison.singleDownloadCapBytes);
      expect(config.comparisonSingleUploadDurationMs).toBe(profile.comparison.singleUploadDurationMs);
      expect(config.comparisonSingleUploadCapBytes).toBe(profile.comparison.singleUploadCapBytes);

      for (const method of contract.transferMethods) {
        expect(buildDiagnosticTestPlan(config, method).transferCapBytes)
          .toBe(profile.downloadCapBytes + profile.uploadCapBytes);
      }
    }
  });

  it("matches the approved Stress scaling sequence", () => {
    const stress = contract.profiles.find((profile) => profile.id === "extended");
    expect(stress).toBeDefined();

    const plan = buildDiagnosticTestPlan(TEST_MODES.extended, "compare");
    expect(plan.downloads.map((stage) => ({
      connections: stage.concurrency,
      durationMs: stage.durationMs,
      capBytes: stage.capBytes
    }))).toEqual(stress?.downloadScaling);
  });
});

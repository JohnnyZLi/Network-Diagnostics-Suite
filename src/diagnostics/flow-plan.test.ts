import { describe, expect, it } from "vitest";
import { TEST_MODES } from "./config";
import { buildDiagnosticTestPlan } from "./flow-plan";

describe("buildDiagnosticTestPlan", () => {
  it("keeps single mode isolated in both directions", () => {
    const plan = buildDiagnosticTestPlan(TEST_MODES.quick, "single");

    expect(plan.downloads).toHaveLength(1);
    expect(plan.uploads).toHaveLength(1);
    expect(plan.downloads[0]?.concurrency).toBe(1);
    expect(plan.uploads[0]?.concurrency).toBe(1);
    expect(plan.transferCapBytes).toBe(728 * 1_000_000);
  });

  it("preserves the existing aggregate profile", () => {
    const plan = buildDiagnosticTestPlan(TEST_MODES.standard, "aggregate");

    expect(plan.downloads[0]).toMatchObject({ concurrency: 8, samples: 3, durationMs: 12_000 });
    expect(plan.uploads[0]).toMatchObject({ concurrency: 8, durationMs: 12_000 });
    expect(plan.transferCapBytes).toBe(1_156 * 1_000_000);
  });

  it("adds a compact single-flow download to Quick Compare without raising its cap", () => {
    const plan = buildDiagnosticTestPlan(TEST_MODES.quick, "compare");

    expect(plan.downloads.map((stage) => stage.concurrency)).toEqual([1, 6]);
    expect(plan.downloads.map((stage) => stage.capBytes)).toEqual([150, 450].map((megabytes) => megabytes * 1_000_000));
    expect(plan.uploads.map((stage) => stage.concurrency)).toEqual([6]);
    expect(plan.transferCapBytes).toBe(728 * 1_000_000);
    expect(plan.estimatedTime).toBe("25 seconds");
  });

  it("splits the Full payload budget across single and aggregate stages", () => {
    const plan = buildDiagnosticTestPlan(TEST_MODES.standard, "compare");

    expect(plan.downloads.map((stage) => stage.capBytes)).toEqual([250, 650].map((megabytes) => megabytes * 1_000_000));
    expect(plan.uploads.map((stage) => stage.capBytes)).toEqual([64, 192].map((megabytes) => megabytes * 1_000_000));
    expect(plan.transferCapBytes).toBe(1_156 * 1_000_000);
  });

  it("builds the Stress connection-scaling curve without increasing its profile cap", () => {
    const plan = buildDiagnosticTestPlan(TEST_MODES.extended, "compare");

    expect(plan.downloads.map((stage) => stage.concurrency)).toEqual([1, 2, 4, 8, 10]);
    expect(plan.downloads.reduce((sum, stage) => sum + stage.capBytes, 0)).toBe(3_000 * 1_000_000);
    expect(plan.uploads.map((stage) => stage.concurrency)).toEqual([1, 8]);
    expect(plan.uploads.map((stage) => stage.capBytes)).toEqual([128, 384].map((megabytes) => megabytes * 1_000_000));
    expect(plan.transferCapBytes).toBe(3_512 * 1_000_000);
    expect(plan.estimatedTime).toBe("65 seconds");
  });
});

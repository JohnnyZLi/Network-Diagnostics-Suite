import { describe, expect, it } from "vitest";
import {
  bufferbloatGrade,
  consecutiveJitter,
  mean,
  percentile,
  summarizeLatency,
  summarizeLoadedLatency,
  throughputFromTimeline
} from "../src/core/statistics";

describe("statistics", () => {
  it("returns null for means and percentiles without samples", () => {
    expect(mean([])).toBeNull();
    expect(percentile([], 95)).toBeNull();
  });

  it("interpolates percentiles without mutating the input", () => {
    const values = [40, 10, 30, 20];
    expect(percentile(values, 50)).toBe(25);
    expect(percentile(values, 95)).toBeCloseTo(38.5);
    expect(values).toEqual([40, 10, 30, 20]);
  });

  it("calculates consecutive-sample jitter", () => {
    expect(consecutiveJitter([10, 16, 13, 19])).toBe(5);
    expect(consecutiveJitter([10])).toBeNull();
  });

  it("keeps timed-out requests in the loss calculation", () => {
    const summary = summarizeLatency([10, null, 20, null]);
    expect(summary.sent).toBe(4);
    expect(summary.received).toBe(2);
    expect(summary.lost).toBe(2);
    expect(summary.lossPercent).toBe(50);
    expect(summary.medianMs).toBe(15);
  });

  it("reports no fabricated latency when every request is lost", () => {
    const summary = summarizeLatency([null, null]);
    expect(summary.meanMs).toBeNull();
    expect(summary.minMs).toBeNull();
    expect(summary.maxMs).toBeNull();
    expect(summary.lossPercent).toBe(100);
  });

  it("calculates loaded-latency increase from the idle median", () => {
    const summary = summarizeLoadedLatency([30, 34, 38], 10);
    expect(summary.medianMs).toBe(34);
    expect(summary.increaseMs).toBe(24);
    expect(summary.grade).toBe("B");
  });

  it.each([
    [0, "A+"],
    [5, "A+"],
    [6, "A"],
    [16, "B"],
    [31, "C"],
    [61, "D"],
    [101, "F"],
    [null, "—"]
  ] as const)("grades %s ms of loaded latency as %s", (increase, grade) => {
    expect(bufferbloatGrade(increase)).toBe(grade);
  });

  it("converts transferred bytes and elapsed time into decimal megabits", () => {
    const summary = throughputFromTimeline(125_000_000, 1_000, [
      { elapsedMs: 250, value: 900 },
      { elapsedMs: 500, value: 1_100 }
    ]);
    expect(summary.mbps).toBe(1_000);
    expect(summary.peakMbps).toBe(1_100);
    expect(summary.stabilityPercent).toBeCloseTo(90);
  });
});

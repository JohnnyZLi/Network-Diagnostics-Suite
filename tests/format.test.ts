import { describe, expect, it } from "vitest";
import { formatBytes, formatDuration, formatLatency, formatRate } from "../src/core/format";

describe("display formatting", () => {
  it("uses useful precision for speeds", () => {
    expect(formatRate(1.234)).toBe("1.23");
    expect(formatRate(12.34)).toBe("12.3");
    expect(formatRate(123.4)).toBe("123");
    expect(formatRate(null)).toBe("—");
  });

  it("formats latency and byte counts", () => {
    expect(formatLatency(9.94)).toBe("9.9");
    expect(formatLatency(101.4)).toBe("101");
    expect(formatBytes(1_500_000)).toBe("1.5 MB");
    expect(formatBytes(0)).toBe("0 B");
  });

  it("formats subsecond and multisecond durations", () => {
    expect(formatDuration(250)).toBe("250 ms");
    expect(formatDuration(1_250)).toBe("1.3 s");
  });
});

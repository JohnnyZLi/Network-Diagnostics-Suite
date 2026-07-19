import { describe, expect, it } from "vitest";
import { TEST_MODES } from "../src/diagnostics/config";

describe("test modes", () => {
  it("keeps the quick test below the full test data caps", () => {
    expect(TEST_MODES.quick.downloadCapBytes).toBeLessThan(TEST_MODES.standard.downloadCapBytes);
    expect(TEST_MODES.quick.uploadCapBytes).toBeLessThan(TEST_MODES.standard.uploadCapBytes);
  });

  it("requires an explicit stress-test selection for gigabyte downloads", () => {
    expect(TEST_MODES.quick.downloadCapBytes).toBeLessThan(1_000_000_000);
    expect(TEST_MODES.standard.downloadCapBytes).toBeLessThan(1_000_000_000);
    expect(TEST_MODES.extended.downloadCapBytes).toBe(1_000_000_000);
  });

  it("does not contact third-party service targets during the quick test", () => {
    expect(TEST_MODES.quick.includeServices).toBe(false);
    expect(TEST_MODES.standard.includeServices).toBe(true);
  });
});

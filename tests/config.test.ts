import { describe, expect, it } from "vitest";
import { TEST_MODES } from "../src/diagnostics/config";
import { UPLOAD_REQUEST_TIMEOUT_MS } from "../src/diagnostics/http";
import { uploadRequestBytesForDuration } from "../src/diagnostics/throughput";

describe("test modes", () => {
  it("keeps Connection Check below 30 MB and below the full test caps", () => {
    expect(TEST_MODES.quick.downloadCapBytes + TEST_MODES.quick.uploadCapBytes).toBeLessThanOrEqual(30_000_000);
    expect(TEST_MODES.quick.downloadCapBytes).toBeLessThan(TEST_MODES.standard.downloadCapBytes);
    expect(TEST_MODES.quick.uploadCapBytes).toBeLessThan(TEST_MODES.standard.uploadCapBytes);
  });

  it("requires an explicit stress-test selection for gigabyte downloads", () => {
    expect(TEST_MODES.quick.downloadCapBytes).toBeLessThan(1_000_000_000);
    expect(TEST_MODES.standard.downloadCapBytes).toBeLessThan(1_000_000_000);
    expect(TEST_MODES.extended.downloadCapBytes).toBeGreaterThanOrEqual(1_000_000_000);
  });

  it("lets each upload request outlive the longest configured upload phase", () => {
    expect(UPLOAD_REQUEST_TIMEOUT_MS).toBeGreaterThan(TEST_MODES.extended.uploadDurationMs);
  });

  it("keeps Stress upload concurrency within the proven Full-test level", () => {
    expect(TEST_MODES.extended.uploadConcurrency).toBeLessThanOrEqual(TEST_MODES.standard.uploadConcurrency);
    expect(TEST_MODES.extended.concurrency).toBeGreaterThan(TEST_MODES.extended.uploadConcurrency);
  });

  it("uses longer request bodies for the sustained Stress upload phase", () => {
    expect(uploadRequestBytesForDuration(TEST_MODES.quick.uploadDurationMs)).toBe(16 * 1024 * 1024);
    expect(uploadRequestBytesForDuration(TEST_MODES.standard.uploadDurationMs)).toBe(16 * 1024 * 1024);
    expect(uploadRequestBytesForDuration(TEST_MODES.extended.uploadDurationMs)).toBe(32 * 1024 * 1024);
  });

  it("does not contact third-party service targets during Connection Check", () => {
    expect(TEST_MODES.quick.includeServices).toBe(false);
    expect(TEST_MODES.standard.includeServices).toBe(true);
  });
});

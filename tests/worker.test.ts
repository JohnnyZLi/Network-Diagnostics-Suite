import { describe, expect, it } from "vitest";
import { clampDownloadSize } from "../worker";

describe("download endpoint limits", () => {
  it("uses a one-megabyte default", () => {
    expect(clampDownloadSize(null)).toBe(1024 * 1024);
    expect(clampDownloadSize("invalid")).toBe(1024 * 1024);
  });

  it("clamps downloads to safe per-request bounds", () => {
    expect(clampDownloadSize("1")).toBe(1024);
    expect(clampDownloadSize("999999999")).toBe(24 * 1024 * 1024);
    expect(clampDownloadSize("8192")).toBe(8192);
  });
});

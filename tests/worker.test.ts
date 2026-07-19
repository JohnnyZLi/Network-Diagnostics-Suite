import { describe, expect, it } from "vitest";
import { clampDownloadSize, redirectToHttps } from "../worker";

describe("transport security", () => {
  it("redirects plain HTTP requests without losing their path or query", () => {
    const response = redirectToHttps(new Request("http://network.johnnyli.dev/api/ping?n=1"));

    expect(response?.status).toBe(308);
    expect(response?.headers.get("Location")).toBe("https://network.johnnyli.dev/api/ping?n=1");
  });

  it("leaves HTTPS requests unchanged", () => {
    expect(redirectToHttps(new Request("https://network.johnnyli.dev/"))).toBeNull();
  });
});

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

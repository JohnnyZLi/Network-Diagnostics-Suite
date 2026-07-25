import { describe, expect, it } from "vitest";
import { SERVICE_TARGETS } from "../src/diagnostics/services";

describe("service reachability targets", () => {
  it("checks Cloudflare through the deployed first-party Worker", () => {
    const cloudflare = SERVICE_TARGETS.find((target) => target.id === "cloudflare");

    expect(cloudflare).toMatchObject({
      url: "/api/ping",
      requestMode: "same-origin",
      successNote: "First-party Cloudflare Worker request"
    });
  });

  it("keeps the external service checks opaque and credential-free", () => {
    const externalTargets = SERVICE_TARGETS.filter((target) => target.id !== "cloudflare");

    expect(externalTargets).toHaveLength(5);
    expect(externalTargets.every((target) => target.requestMode === "no-cors")).toBe(true);
  });
});

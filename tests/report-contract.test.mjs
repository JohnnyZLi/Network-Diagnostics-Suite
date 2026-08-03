import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const schema = JSON.parse(
  readFileSync(new URL("../contracts/report-v2.schema.json", import.meta.url), "utf8")
);
const webProfiles = JSON.parse(
  readFileSync(new URL("../contracts/test-profiles.v1.json", import.meta.url), "utf8")
);
const desktopProfiles = JSON.parse(
  readFileSync(new URL("../contracts/desktop-test-profiles.v1.json", import.meta.url), "utf8")
);

describe("shared report compatibility contract", () => {
  it("keeps existing schema 2.0 reports valid while adding optional native fields", () => {
    expect(schema.properties.schemaVersion.const).toBe("2.0");
    expect(schema.required).toEqual(["schemaVersion", "generatedAt", "run"]);
    expect(schema.required).not.toContain("producer");
    expect(schema.required).not.toContain("measurement");
    expect(schema.required).not.toContain("findings");
    expect(schema.additionalProperties).toBe(true);

    expect(schema.properties.run.properties.profile.enum).toEqual([
      "connection-check",
      "quick",
      "standard",
      "extended"
    ]);
    expect(schema.properties.measurement).toBeDefined();
    expect(schema.properties.findings).toBeDefined();
  });

  it("leaves the website profiles unchanged and gives native clients their own profile set", () => {
    expect(webProfiles.schemaVersion).toBe("1.0");
    expect(webProfiles.profiles.map((profile) => profile.name)).toEqual([
      "Quick",
      "Full",
      "Stress"
    ]);

    expect(desktopProfiles.schemaVersion).toBe("1.1");
    expect(desktopProfiles.profiles.map((profile) => profile.name)).toEqual([
      "Connection Check",
      "Quick",
      "Full",
      "Stress"
    ]);
  });
});

import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";

const appSource = await readFile(new URL("./App.tsx", import.meta.url), "utf8");
const polishSource = await readFile(new URL("./ui-polish.css", import.meta.url), "utf8");

describe("compact header contract", () => {
  it("renders Menu before Sites in DOM and focus order", () => {
    const actionsStart = appSource.indexOf('className="header-actions jl-global-header__actions"');
    const actionsEnd = appSource.indexOf("</div>", appSource.indexOf("</div>", actionsStart) + 1);
    const actions = appSource.slice(actionsStart, actionsEnd);

    expect(actionsStart).toBeGreaterThan(-1);
    expect(actions.indexOf("data-header-menu-button")).toBeGreaterThan(-1);
    expect(actions.indexOf("data-site-switcher")).toBeGreaterThan(-1);
    expect(actions.indexOf("data-header-menu-button")).toBeLessThan(actions.indexOf("data-site-switcher"));
  });

  it("does not restore the obsolete clipped mobile navigation shell", () => {
    expect(polishSource).not.toContain(".site-header .site-nav {");
    expect(polishSource).not.toContain("border-top: 0;");
    expect(polishSource).not.toContain("border-radius: 0 0 14px 14px;");
    expect(polishSource).not.toContain(".nav-toggle {");
  });
});

import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

const read = (path) => readFile(resolve(path), "utf8");
const [app, panels, resultHistory, privacy, themeBootstrap, wrangler] = await Promise.all([
  read("src/App.tsx"),
  read("src/components/InformationPanels.tsx"),
  read("src/core/result-history.ts"),
  read("docs/privacy.md"),
  read("src/design-system/theme-bootstrap.js"),
  read("wrangler.jsonc"),
]);

const fail = (message) => { throw new Error(message); };
const canonical = "https://johnnyli.dev/privacy/#network-diagnostics";

if (!app.includes(canonical) || !panels.includes(canonical)) {
  fail("Network Diagnostics must link its header/footer summary to the canonical privacy section.");
}
if (!resultHistory.includes("export const MAX_RECENT_RESULTS = 12")) {
  fail("Privacy copy assumes the reviewed 12-report browser-history limit.");
}
if (!privacy.includes("Recent browser reports and remembered high-data confirmations are stored only in that browser's local storage.")) {
  fail("Privacy documentation no longer describes browser-local report history.");
}
if (/no[^\n.]{0,80}\bcookies?\b/i.test(privacy)) {
  fail("Privacy documentation must not make a blanket no-cookies claim while the shared theme preference uses a functional cookie.");
}
for (const fragment of ['cookieName = "jl-theme"', "document.cookie", "Domain=.johnnyli.dev", "Max-Age=31536000"]) {
  if (!themeBootstrap.includes(fragment)) fail(`Shared theme cookie contract drifted: ${fragment}`);
}
for (const fragment of ["functional `jl-theme` cookie", "appearance choice", "not an analytics, advertising, or behavioral-tracking identifier"]) {
  if (!privacy.includes(fragment)) fail(`Privacy documentation is missing the shared-theme disclosure: ${fragment}`);
}
if (!/"observability"\s*:\s*\{[\s\S]*?"enabled"\s*:\s*false/.test(wrangler)) {
  fail("Privacy documentation assumes Worker observability remains disabled.");
}
if (!panels.includes("Read full privacy information")) {
  fail("The in-product privacy summary must provide the canonical detail link.");
}

console.log("Privacy contract check passed.");

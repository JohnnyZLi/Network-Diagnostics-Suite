import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const main = await readFile(resolve("src/main.tsx"), "utf8");
const app = await readFile(resolve("src/App.tsx"), "utf8");
const adapter = await readFile(resolve("src/design-system-adapter.css"), "utf8");
const identityStyles = await readFile(resolve("src/design-system/site-identity.css"), "utf8");
const version = JSON.parse(await readFile(resolve("src/design-system/version.json"), "utf8"));
const source = await readFile(resolve("src/design-system/SOURCE.md"), "utf8");

const fail = (message) => {
  throw new Error(message);
};

if (version.version !== "1.3.3") fail("Network Diagnostics must consume Web Design System v1.3.3.");
if (!source.includes("5eeb2effcffb0c11f93e683f178ab80d7456bde4")) {
  fail("Design-system source commit is not pinned.");
}

const requiredImports = [
  "./design-system/tokens.css",
  "./design-system/foundations.css",
  "./design-system/site-identity.css",
  "./styles.css",
  "./history.css",
  "./report-details.css",
  "./ui-polish.css",
  "./transfer-color.css",
  "./full-bleed-layout.css",
  "./design-system-adapter.css",
];
let previousPosition = -1;
for (const stylesheet of requiredImports) {
  const position = main.indexOf(`import \"${stylesheet}\"`);
  if (position < 0) fail(`Missing stylesheet import ${stylesheet}.`);
  if (position <= previousPosition) fail(`Stylesheet order is incorrect at ${stylesheet}.`);
  previousPosition = position;
}

for (const obsolete of ["portfolio-dots.css", "typography-accent.css"]) {
  if (main.includes(obsolete)) fail(`Obsolete override remains imported: ${obsolete}.`);
  try {
    await access(resolve("src", obsolete));
    fail(`Obsolete override file still exists: ${obsolete}.`);
  } catch (error) {
    if (error instanceof Error && error.message.startsWith("Obsolete override")) throw error;
  }
}

const menuStart = app.indexOf('id="owned-sites-menu"');
const menuEnd = app.indexOf("</ul>", menuStart);
if (menuStart < 0 || menuEnd < 0) fail("Owned-sites menu markup is missing.");
const menu = app.slice(menuStart, menuEnd);
const ownedSites = [
  ["https://johnnyli.dev", false],
  ["https://network.johnnyli.dev", true],
  ["https://rolepacket.johnnyli.dev", false],
];
for (const [url, current] of ownedSites) {
  const escaped = url.replaceAll(".", "\\.");
  const match = menu.match(new RegExp(`<a[^>]*href=\"${escaped}\"[^>]*>`, "g"));
  if (!match || match.length !== 1) fail(`Expected one site-switcher link for ${url}.`);
  if (/\btarget=/.test(match[0])) fail(`Owned-site link must stay in the same tab: ${url}.`);
  if (current !== /aria-current=\"page\"/.test(match[0])) fail(`Incorrect current-site state for ${url}.`);
}

for (const hook of [
  "Johnny Li",
  "jl-site-identity__separator",
  "jl-site-identity__product",
  "owned-sites-menu",
  "siteSwitcherRef",
  "setSitesOpen",
  "jl-site-switcher__button",
]) {
  if (!app.includes(hook)) fail(`Missing shared-header integration hook: ${hook}.`);
}

const requiredAliases = ["--bg", "--panel", "--line", "--text", "--muted", "--accent", "--radius", "--ease-out"];
for (const alias of requiredAliases) {
  if (!new RegExp(`${alias}:\\s*var\\(--jl-`).test(adapter)) {
    fail(`Network role ${alias} is not mapped to a shared token.`);
  }
}
if (!adapter.includes("var(--jl-color-canvas-dot)")) fail("Shared exact dot canvas is not active.");
if (!adapter.includes("var(--jl-color-focus-ring)")) fail("Shared focus treatment is not active.");
for (const contract of [
  "min-height: var(--jl-layout-header-height)",
  "var(--jl-layout-portfolio-max)",
  "var(--jl-layout-gutter)",
  "font-size: 1.125rem",
  ".wordmark__mark {\n  display: none;",
]) {
  if (!adapter.includes(contract)) fail(`Network header is not aligned to the shared contract: ${contract}.`);
}
if (adapter.includes("text-transform: uppercase")) {
  fail("Network must not uppercase the shared Sites control.");
}
for (const contract of [
  ".jl-global-header__inner",
  "grid-template-columns: auto minmax(0, 1fr) auto",
  "text-transform: none",
]) {
  if (!identityStyles.includes(contract)) fail(`Shared header package contract is incomplete: ${contract}.`);
}

console.log("Network Diagnostics design-system integration passed.");

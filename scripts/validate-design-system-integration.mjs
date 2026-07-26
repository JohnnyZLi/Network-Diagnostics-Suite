import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const main = await readFile(resolve("src/main.tsx"), "utf8");
const app = await readFile(resolve("src/App.tsx"), "utf8");
const testControls = await readFile(resolve("src/components/TestControls.tsx"), "utf8");
const testControlStyles = await readFile(resolve("src/test-controls.css"), "utf8");
const adapter = await readFile(resolve("src/design-system-adapter.css"), "utf8");
const identityStyles = await readFile(resolve("src/design-system/site-identity.css"), "utf8");
const version = JSON.parse(await readFile(resolve("src/design-system/version.json"), "utf8"));
const source = await readFile(resolve("src/design-system/SOURCE.md"), "utf8");

const fail = (message) => {
  throw new Error(message);
};

if (version.version !== "1.3.4") fail("Network Diagnostics must consume Web Design System v1.3.4.");
if (!source.includes("27f83fa7333903a38c2c5ca36ed0455fa71598fc")) {
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
  "./test-controls.css",
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

for (const hook of [
  'className="site-header jl-global-header"',
  'className="jl-global-header__inner"',
  'className="wordmark jl-site-identity"',
  'className="jl-site-identity__owner"',
  'className="jl-site-identity__separator"',
  'className="wordmark__product jl-site-identity__product"',
  "jl-global-header__nav",
  'className="header-actions jl-global-header__actions"',
  "owned-sites-menu",
  "siteSwitcherRef",
  "setSitesOpen",
  "jl-site-switcher__button",
]) {
  if (!app.includes(hook)) fail(`Missing shared global-header hook: ${hook}.`);
}
if (app.includes("wordmark__mark")) fail("The product icon must not alter the shared identity lockup.");

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

for (const contract of [
  "function compactEstimatedTime",
  '.replace(/^about\\s+/i, "≈")',
  '.replace(/\\s+seconds?$/i, " sec")',
  "aria-label={`${optionConfig.name}, ${optionConfig.estimatedTime}`}",
  '<small aria-hidden="true">{compactEstimatedTime(optionConfig.estimatedTime)}</small>',
]) {
  if (!testControls.includes(contract)) fail(`Test profile label contract is incomplete: ${contract}.`);
}
for (const contract of [
  "grid-template-columns: repeat(3, minmax(0, 1fr));",
  "min-height: 68px;",
  "overflow: hidden;",
  "text-overflow: ellipsis;",
  "white-space: nowrap;",
  "@media (max-width: 760px)",
  "grid-template-columns: 1fr;",
]) {
  if (!testControlStyles.includes(contract)) fail(`Test profile layout contract is incomplete: ${contract}.`);
}

const requiredAliases = ["--bg", "--panel", "--line", "--text", "--muted", "--accent", "--radius", "--ease-out"];
for (const alias of requiredAliases) {
  if (!new RegExp(`${alias}:\\s*var\\(--jl-`).test(adapter)) {
    fail(`Network role ${alias} is not mapped to a shared token.`);
  }
}
if (!adapter.includes("var(--jl-color-canvas-dot)")) fail("Shared exact dot canvas is not active.");
if (!adapter.includes("var(--jl-color-focus-ring)")) fail("Shared focus treatment is not active.");
if (!adapter.includes("display: block;") || !adapter.includes("grid-template-columns: none;")) {
  fail("Network legacy header geometry is not neutralized.");
}
for (const forbidden of ["var(--jl-layout-portfolio-max)", ".jl-site-switcher__button", "text-transform: uppercase"]) {
  if (adapter.includes(forbidden)) fail(`Network must not re-own shared header styling: ${forbidden}.`);
}
if (/^\s*@layer\b/m.test(identityStyles)) {
  fail("Shared header must remain unlayered so Network button resets cannot override it.");
}
for (const contract of [
  ".jl-global-header__inner",
  "grid-template-columns: auto minmax(0, 1fr) auto",
  "width: 88px;",
  "height: var(--jl-control-height-md);",
  "font-family: var(--jl-font-ui);",
  "font-size: 13px;",
  "font-weight: 700;",
  "line-height: 1;",
  '.jl-site-switcher__button > [aria-hidden="true"]',
  "border-right: 2px solid currentColor;",
  "border-bottom: 2px solid currentColor;",
]) {
  if (!identityStyles.includes(contract)) fail(`Shared Sites control contract is incomplete: ${contract}.`);
}

console.log("Network Diagnostics design-system integration passed.");

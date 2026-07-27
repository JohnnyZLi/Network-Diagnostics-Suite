import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const main = await readFile(resolve("src/main.tsx"), "utf8");
const app = await readFile(resolve("src/App.tsx"), "utf8");
const testControls = await readFile(resolve("src/components/TestControls.tsx"), "utf8");
const testControlStyles = await readFile(resolve("src/test-controls.css"), "utf8");
const metricCardStyles = await readFile(resolve("src/metric-card-layout.css"), "utf8");
const heroLayout = await readFile(resolve("src/hero-layout.css"), "utf8");
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
  "./metric-card-layout.css",
  "./test-controls.css",
  "./transfer-color.css",
  "./full-bleed-layout.css",
  "./hero-layout.css",
  "./design-system-adapter.css",
];
let previousPosition = -1;
for (const stylesheet of requiredImports) {
  const position = main.indexOf(`import "${stylesheet}"`);
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
  "nav-toggle",
  "mobileNav",
  "site-nav--open",
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
  'aria-label={`${optionConfig.name}, ${optionConfig.estimatedTime}`}',
  '<div><dt>Estimated time</dt><dd>{compactEstimatedTime(config.estimatedTime)}</dd></div>',
  '<div><dt>Transfer cap</dt><dd>{formatBytes(transferCap)}</dd></div>',
  "DATA_CONFIRMATION_STORAGE_KEY",
  "loadConfirmationRecord",
  "saveConfirmationRecord",
  "rememberedCap < transferCap",
  "if (!requiresConfirmation)",
  "dialog.showModal()",
  'aria-haspopup={requiresConfirmation ? "dialog" : undefined}',
  "<dialog",
  'id="data-confirmation-dialog"',
  'aria-labelledby="data-confirmation-dialog-title"',
  'aria-describedby="data-confirmation-dialog-description data-confirmation-dialog-note"',
  "Run the {config.name} test?",
  "This test may transfer up to {formatBytes(transferCap)}",
  "Remember this choice for the {config.name} profile on this browser.",
  "You’ll be asked again if this profile’s transfer cap increases.",
  "onClose",
]) {
  if (!testControls.includes(contract)) fail(`Test profile confirmation contract is incomplete: ${contract}.`);
}
for (const forbidden of [
  '<small aria-hidden="true">{compactEstimatedTime(optionConfig.estimatedTime)}</small>',
  "optionTransferCap",
  "mode-option__cap",
  "up to ${formatBytes(optionTransferCap)}",
  'className="data-confirmation-slot"',
  'className="data-confirmation-status"',
  "Quick test can start immediately",
  "disabled={requiresConfirmation",
]) {
  if (testControls.includes(forbidden)) fail(`Obsolete inline confirmation remains: ${forbidden}.`);
}
for (const contract of [
  "grid-template-columns: repeat(3, minmax(0, 1fr));",
  "min-height: 56px;",
  "overflow: hidden;",
  "text-overflow: ellipsis;",
  "white-space: nowrap;",
  ".data-confirmation-dialog",
  ".data-confirmation-dialog::backdrop",
  ".data-confirmation-dialog__remember",
  ".data-confirmation-dialog__actions",
  "inset: auto 0 12px;",
  "@media (max-width: 760px)",
  "grid-template-columns: 1fr;",
  "grid-row: auto;",
]) {
  if (!testControlStyles.includes(contract)) fail(`Test profile layout contract is incomplete: ${contract}.`);
}
for (const forbidden of [".mode-option__cap", ".data-confirmation-slot", ".data-confirmation-status"]) {
  if (testControlStyles.includes(forbidden)) fail(`Obsolete test-control styling remains: ${forbidden}.`);
}
for (const contract of [
  ".metric-card",
  "display: flex;",
  "flex-direction: column;",
  ".metric-card .sparkline",
  "position: static;",
  "margin: auto -8px -18px;",
]) {
  if (!metricCardStyles.includes(contract)) fail(`Metric card chart-separation contract is incomplete: ${contract}.`);
}
for (const contract of [
  "@media (min-width: 1101px)",
  ".hero",
  "align-items: start;",
  ".hero__copy,",
  ".test-controls",
  "align-self: start;",
  "position: sticky;",
  "top: calc(var(--jl-layout-header-height) + var(--jl-space-8));",
  "@media (max-width: 1100px)",
  "position: static;",
]) {
  if (!heroLayout.includes(contract)) fail(`Hero alignment and bounded-sticky contract is incomplete: ${contract}.`);
}

const requiredAliases = ["--bg", "--panel", "--line", "--text", "--muted", "--accent", "--radius", "--ease-out"];
for (const alias of requiredAliases) {
  if (!new RegExp(`${alias}:\\s*var\\(--jl-`).test(adapter)) {
    fail(`Network role ${alias} is not mapped to a shared token.`);
  }
}
if (!adapter.includes("var(--jl-color-canvas-dot)")) fail("Shared exact dot canvas is not active.");
if (!adapter.includes("var(--jl-color-focus-ring)")) fail("Shared focus treatment is not active.");
if (!adapter.includes("body::before,") || !adapter.includes("display: none;")) {
  fail("The legacy visible grid is not disabled.");
}
for (const compactNavContract of [
  "@media (max-width: 900px)",
  ".site-header .site-nav--open",
  "position: absolute;",
  "top: 100%;",
  "display: grid;",
  ".nav-toggle",
  "order: -1;",
  "var(--jl-shadow-high)",
]) {
  if (!adapter.includes(compactNavContract)) fail(`Compact Network navigation is incomplete: ${compactNavContract}.`);
}
for (const colorContract of [
  ".methodology-grid article:nth-child(n) h3",
  ".preview-grid article:nth-child(3) h3",
  "color: var(--text);",
]) {
  if (!adapter.includes(colorContract)) fail(`Network explanatory color hierarchy is incomplete: ${colorContract}.`);
}
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

import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const main = await readFile(resolve("src/main.tsx"), "utf8");
const app = await readFile(resolve("src/App.tsx"), "utf8");
const siteControls = await readFile(resolve("src/design-system/site-controls.js"), "utf8");
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

if (version.version !== "1.5.0") fail("Network Diagnostics must consume Web Design System v1.5.0.");
if (!source.includes("14fc1281f02d3a1fa33e6d80aae24637d93b04f7")) {
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

for (const obsolete of ["portfolio-dots.css", "typography-accent.css", "compact-navigation-escape.ts"]) {
  if (main.includes(obsolete)) fail(`Obsolete integration remains imported: ${obsolete}.`);
  try {
    await access(resolve("src", obsolete));
    fail(`Obsolete integration file still exists: ${obsolete}.`);
  } catch (error) {
    if (error instanceof Error && error.message.startsWith("Obsolete integration")) throw error;
  }
}

for (const hook of [
  'className="site-header jl-global-header"',
  'className="jl-global-header__inner"',
  'className="wordmark jl-site-identity"',
  'className="jl-site-identity__owner"',
  'className="jl-site-identity__separator"',
  'className="wordmark__product jl-site-identity__product"',
  "jl-global-header__nav jl-header-menu",
  'className="header-actions jl-global-header__actions"',
  "data-site-switcher",
  "data-site-switcher-button",
  "data-site-switcher-menu",
  "data-header-menu",
  "data-header-menu-button",
  "jl-header-menu--open",
  "jl-header-menu-toggle",
  "OWNED_SITES.map",
  'site.id === "network"',
  "installSiteSwitcher",
  "installHeaderMenu",
  "siteController.destroy()",
  "navigationController.destroy()",
  "mobileNavControllerRef.current?.close()",
  "siteController.close()",
]) {
  if (!app.includes(hook)) fail(`Missing shared global-header or controller hook: ${hook}.`);
}
if (app.includes("wordmark__mark")) fail("The product icon must not alter the shared identity lockup.");
for (const forbidden of [
  'document.addEventListener("pointerdown"',
  'document.addEventListener("keydown"',
  "site-nav--open",
  "siteSwitcherButtonRef",
  "closeMobileNav",
]) {
  if (app.includes(forbidden)) fail(`Duplicated navigation behavior remains in App: ${forbidden}.`);
}

for (const contract of [
  "export const OWNED_SITES",
  'id: "portfolio"',
  'id: "network"',
  'id: "rolepacket"',
  'href: "https://johnnyli.dev"',
  'href: "https://network.johnnyli.dev"',
  'href: "https://rolepacket.johnnyli.dev"',
  "export function installDisclosureMenu",
  "export function installSiteSwitcher",
  "export function installHeaderMenu",
  'event.key === "ArrowDown"',
  'event.key === "ArrowUp"',
  'event.key === "Home"',
  'event.key === "End"',
  'event.key === "Escape"',
  'document.addEventListener("pointerdown"',
  'closeMediaQuery: "(min-width: 901px)"',
]) {
  if (!siteControls.includes(contract)) fail(`Shared site-control contract is incomplete: ${contract}.`);
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
for (const forbidden of [
  "@media (max-width: 900px)",
  ".site-nav--open",
  ".nav-toggle",
  "var(--jl-layout-portfolio-max)",
  ".jl-site-switcher__button",
  "text-transform: uppercase",
]) {
  if (adapter.includes(forbidden)) fail(`Network adapter re-owns shared header behavior or styling: ${forbidden}.`);
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
  ".jl-header-menu-toggle",
  ".jl-global-header__nav.jl-header-menu--open",
  "right: var(--jl-layout-gutter);",
  "left: var(--jl-layout-gutter);",
  "@media (forced-colors: active)",
]) {
  if (!identityStyles.includes(contract)) fail(`Shared header and compact-menu contract is incomplete: ${contract}.`);
}

console.log("Network Diagnostics design-system integration passed.");

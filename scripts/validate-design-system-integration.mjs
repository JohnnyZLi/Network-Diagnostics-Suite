import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const read = (path) => readFile(resolve(path), "utf8");
const main = await read("src/main.tsx");
const app = await read("src/App.tsx");
const styles = await read("src/styles.css");
const polish = await read("src/ui-polish.css");
const latencyTable = await read("src/components/LatencyTable.tsx");
const siteControls = await read("src/design-system/site-controls.js");
const primitives = await read("src/design-system/content-primitives.css");
const primitiveMappings = await read("src/content-primitive-mappings.css");
const testControls = await read("src/components/TestControls.tsx");
const testControlStyles = await read("src/test-controls.css");
const metricCardStyles = await read("src/metric-card-layout.css");
const heroLayout = await read("src/hero-layout.css");
const adapter = await read("src/design-system-adapter.css");
const identityStyles = await read("src/design-system/site-identity.css");
const updater = await read("scripts/update-design-system.mjs");
const consumerRelease = await read("scripts/design-system-consumer-release.mjs");
const conformanceRunner = await read("scripts/design-system-conformance-runner.mjs");
const conformanceContract = JSON.parse(await read("scripts/design-system-conformance-contract.json"));
const conformanceManifest = JSON.parse(await read("design-system.conformance.json"));
const synchronizer = await read("scripts/check-design-system.mjs");
const syncWorkflow = await read(".github/workflows/design-system-sync.yml");
const conformanceWorkflow = await read(".github/workflows/design-system-conformance.yml");
const packageMetadata = JSON.parse(await read("package.json"));
const version = JSON.parse(await read("src/design-system/version.json"));
const lock = JSON.parse(await read("design-system.lock.json"));
const source = await read("src/design-system/SOURCE.md");

const expectedVersion = String(lock.version ?? "");
const expectedCommit = String(lock.sourceCommit ?? "");
const fail = (message) => { throw new Error(message); };
const requireFragments = (content, fragments, label) => {
  for (const fragment of fragments) if (!content.includes(fragment)) fail(`${label} is incomplete: ${fragment}.`);
};
const requireImmutableWorkflow = (content, workflow, label) => {
  const pattern = new RegExp(`uses: JohnnyZLi/Web-Design-System/\\.github/workflows/${workflow}@[0-9a-f]{40}`);
  if (!pattern.test(content)) fail(`${label} is not pinned to an immutable design-system commit.`);
};

if (lock.package !== "@johnnyzli/web-design-system") fail("Design-system lock package is invalid.");
if (!/^\d+\.\d+\.\d+$/.test(expectedVersion)) fail("Design-system lock version is invalid.");
if (!/^[0-9a-f]{40}$/.test(expectedCommit)) fail("Design-system lock source commit is invalid.");
if (version.version !== expectedVersion) fail(`Network Diagnostics must consume Web Design System v${expectedVersion}.`);
if (!source.includes(expectedCommit) || !source.includes(`Version: ${expectedVersion}`)) fail("Design-system source metadata is not pinned.");
if (conformanceContract.designSystemVersion !== expectedVersion || conformanceContract.schemaVersion !== "1.0.0") fail("Conformance contract metadata drifted.");
if (conformanceManifest.product !== "network" || conformanceManifest.schemaVersion !== "1.0.0") fail("Network conformance manifest metadata drifted.");
if (packageMetadata.scripts?.["design-system:conformance"] !== "node scripts/design-system-conformance-runner.mjs --contract scripts/design-system-conformance-contract.json") fail("Network conformance command drifted.");
for (const id of ["DS-DIST-001", "DS-DIALOG-001", "DS-DIALOG-002", "DS-RESP-001", "DS-TEST-001"]) {
  if (!conformanceManifest.rules?.[id]) fail(`Network conformance manifest is missing ${id}.`);
}
requireFragments(conformanceRunner, ["function confined(root, value, label)", "manual-pending", "report.json", "report.md", "process.exitCode = 1"], "Shared conformance runner");
if (conformanceRunner.includes("child_process") || conformanceRunner.includes("exec(")) fail("Conformance runner can execute consumer commands.");

const requiredImports = [
  "./design-system/tokens.css",
  "./design-system/foundations.css",
  "./design-system/site-identity.css",
  "./design-system/content-primitives.css",
  "./styles.css",
  "./history.css",
  "./report-details.css",
  "./ui-polish.css",
  "./metric-card-layout.css",
  "./test-controls.css",
  "./transfer-color.css",
  "./full-bleed-layout.css",
  "./hero-layout.css",
  "./editorial-panels.css",
  "./design-system-adapter.css",
  "./content-primitive-mappings.css",
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

requireFragments(app, [
  'className="site-header jl-global-header"',
  'className="jl-global-header__inner"',
  'className="wordmark jl-site-identity"',
  'className="jl-site-identity__owner"',
  'className="jl-site-identity__separator"',
  'className="wordmark__product jl-site-identity__product"',
  "jl-global-header__nav jl-header-menu",
  'className="header-actions jl-global-header__actions"',
  "data-site-switcher", "data-site-switcher-button", "data-site-switcher-menu",
  "data-header-menu", "data-header-menu-button", "jl-header-menu--open", "jl-header-menu-toggle",
  "populate: true", 'currentSite: "network"', "installSiteSwitcher", "installHeaderMenu",
  "siteController.destroy()", "navigationController.destroy()",
  "mobileNavControllerRef.current?.close()", "siteController.close()",
], "Network shared header");
if (app.includes("wordmark__mark")) fail("The product icon must not alter the shared identity lockup.");
if (app.includes("OWNED_SITES.map") || app.includes("import { OWNED_SITES")) fail("Network repeats shared Sites menu content instead of using populate.");
const headerActionsPosition = app.indexOf('className="header-actions jl-global-header__actions"');
const menuButtonPosition = app.indexOf("data-header-menu-button", headerActionsPosition);
const sitesPosition = app.indexOf("data-site-switcher", headerActionsPosition);
if (headerActionsPosition < 0 || menuButtonPosition < 0 || sitesPosition < 0 || menuButtonPosition > sitesPosition) {
  fail("Network compact controls must render Menu before Sites in DOM and focus order.");
}
for (const forbidden of [
  'document.addEventListener("pointerdown"', 'document.addEventListener("keydown"',
  "site-nav--open", "siteSwitcherButtonRef", "closeMobileNav",
]) if (app.includes(forbidden)) fail(`Duplicated navigation behavior remains in App: ${forbidden}.`);
for (const forbidden of [
  ".site-header .site-nav {", "border-top: 0;", "border-radius: 0 0 14px 14px;", ".nav-toggle {",
]) if (polish.includes(forbidden)) fail(`Legacy clipped compact navigation remains in ui-polish.css: ${forbidden}.`);

requireFragments(siteControls, [
  "export const OWNED_SITES", 'id: "portfolio"', 'id: "network"', 'id: "rolepacket"',
  'href: "https://johnnyli.dev"', 'href: "https://network.johnnyli.dev"',
  'href: "https://rolepacket.johnnyli.dev"', "export function installDisclosureMenu",
  "export function installSiteSwitcher", "export function installHeaderMenu",
  'event.key === "ArrowDown"', 'event.key === "ArrowUp"', 'event.key === "Home"',
  'event.key === "End"', 'event.key === "Escape"', 'document.addEventListener("pointerdown"',
  'closeMediaQuery: "(min-width: 901px)"',
], "Shared site-control contract");

requireFragments(primitives, [
  ".jl-actions {", "display: flex;", "flex-wrap: wrap;",
  ".jl-button {", "display: inline-flex;", "align-items: center;", "justify-content: center;",
  ".jl-button--compact", ".jl-callout--danger", ".jl-empty-state", ".jl-table-region",
  ".jl-dialog {", ".jl-dialog::backdrop", ".jl-dialog__surface", ".jl-dialog__title",
  ".jl-dialog__message", ".jl-dialog__actions", "@media (forced-colors: active)",
], "Standalone content-primitives asset");
requireFragments(app, [
  'className="error-panel jl-callout jl-callout--danger"',
  'className="jl-button jl-button--compact"',
], "Network error primitive markup");
requireFragments(latencyTable, ['className="latency-table-wrap jl-table-region"'], "Network table-region markup");

requireFragments(primitiveMappings, [
  ".error-panel {", "--jl-callout-padding: 38px;", "--jl-callout-radius: var(--radius);",
  ".error-panel .jl-button {", "--jl-button-font-weight: 400;",
  ".latency-table-wrap {", "--jl-table-region-border-width: 0;", "--jl-table-region-background: transparent;",
  ".data-confirmation-dialog.jl-dialog {", "--jl-dialog-width: min(calc(100% - 32px), 440px);",
  ".data-confirmation-dialog__content.jl-dialog__surface {", "--jl-dialog-surface-radius: 18px;",
  ".data-confirmation-dialog .jl-dialog__title {", ".data-confirmation-dialog .jl-dialog__message {",
  ".data-confirmation-dialog__actions.jl-dialog__actions {",
  ".data-confirmation-dialog__actions .jl-button {",
  ".data-confirmation-dialog__actions .jl-button--primary {",
  "@media (max-width: 520px)", "--jl-dialog-compact-inset: auto 0 12px;",
], "Network content-primitive mappings");
for (const duplicate of [
  "padding: var(--jl-callout-padding);", "border-color: var(--jl-callout-border-color);",
  "background: var(--jl-callout-background);", "box-shadow: var(--jl-callout-shadow);",
]) if (primitiveMappings.includes(duplicate)) fail(`Network mapping repeats shared structure: ${duplicate}.`);

requireFragments(testControls, [
  "function compactEstimatedTime", 'aria-label={`${optionConfig.name}, ${optionConfig.estimatedTime}`}',
  '<div><dt>Estimated time</dt><dd>{compactEstimatedTime(config.estimatedTime)}</dd></div>',
  '<div><dt>Transfer cap</dt><dd>{formatBytes(transferCap)}</dd></div>',
  "DATA_CONFIRMATION_STORAGE_KEY", "loadConfirmationRecord", "saveConfirmationRecord",
  "rememberedCap < transferCap", "if (!requiresConfirmation)", "dialog.showModal()",
  'aria-haspopup={requiresConfirmation ? "dialog" : undefined}', "<dialog",
  'id="data-confirmation-dialog"', 'aria-labelledby="data-confirmation-dialog-title"',
  'aria-describedby="data-confirmation-dialog-description data-confirmation-dialog-note"',
  "Run the {config.name} test?", "This test may transfer up to {formatBytes(transferCap)}",
  "Remember this choice for the {config.name} profile on this browser.",
  "You’ll be asked again if this profile’s transfer cap increases.", "onClose",
  "data-confirmation-dialog--${mode} jl-dialog", "data-confirmation-dialog__content jl-dialog__surface",
  'className="jl-dialog__title"', 'className="jl-dialog__message"',
  "data-confirmation-dialog__actions jl-dialog__actions jl-actions",
  "data-confirmation-dialog__button jl-button",
  "data-confirmation-dialog__button--primary jl-button jl-button--primary",
], "Test profile confirmation contract");
for (const forbidden of [
  '<small aria-hidden="true">{compactEstimatedTime(optionConfig.estimatedTime)}</small>',
  "optionTransferCap", "mode-option__cap", "up to ${formatBytes(optionTransferCap)}",
  'className="data-confirmation-slot"', 'className="data-confirmation-status"',
  "Quick test can start immediately", "disabled={requiresConfirmation",
]) if (testControls.includes(forbidden)) fail(`Obsolete inline confirmation remains: ${forbidden}.`);

requireFragments(testControlStyles, [
  "grid-template-columns: repeat(3, minmax(0, 1fr));", "min-height: 56px;",
  "overflow: hidden;", "text-overflow: ellipsis;", "white-space: nowrap;",
  ".data-confirmation-dialog__remember", ".data-confirmation-dialog .data-confirmation-dialog__note",
  "@media (max-width: 760px)", "grid-template-columns: 1fr;", "grid-row: auto;",
  "@media (max-width: 520px)",
], "Test profile layout contract");
for (const forbidden of [
  ".mode-option__cap", ".data-confirmation-slot", ".data-confirmation-status",
  ".data-confirmation-dialog::backdrop", ".data-confirmation-dialog__content {",
  ".data-confirmation-dialog__actions {", ".data-confirmation-dialog__button {",
]) if (testControlStyles.includes(forbidden)) fail(`Superseded test-control structure remains: ${forbidden}.`);

for (const forbidden of [
  ".result-actions button, .error-panel button", ".result-actions button:hover, .error-panel button:hover",
  ".latency-table-wrap { overflow-x: auto; }",
  ".error-panel { margin: 48px 3.5%; padding: 38px;",
]) if (styles.includes(forbidden)) fail(`Tested primitive fallback remains in styles.css: ${forbidden}.`);
requireFragments(styles, [
  ".result-actions button {", ".result-actions button:hover {", ".error-panel { margin: 48px 3.5%; }",
], "Network product-only styles");

requireFragments(metricCardStyles, [
  ".metric-card", "display: flex;", "flex-direction: column;", ".metric-card .sparkline",
  "position: static;", "margin: auto -8px -18px;",
], "Metric card chart-separation contract");
requireFragments(heroLayout, [
  "@media (min-width: 1101px)", ".hero", "align-items: start;", ".hero__copy,", ".test-controls",
  "align-self: start;", "position: sticky;", "top: calc(var(--jl-layout-header-height) + var(--jl-space-8));",
  "@media (max-width: 1100px)", "position: static;",
], "Hero alignment and bounded-sticky contract");

for (const alias of ["--bg", "--panel", "--line", "--text", "--muted", "--accent", "--radius", "--ease-out"]) {
  if (!new RegExp(`${alias}:\\s*var\\(--jl-`).test(adapter)) fail(`Network role ${alias} is not mapped to a shared token.`);
}
if (!adapter.includes("var(--jl-color-canvas-dot)")) fail("Shared exact dot canvas is not active.");
if (!adapter.includes("var(--jl-color-focus-ring)")) fail("Shared focus treatment is not active.");
if (!adapter.includes("body::before,") || !adapter.includes("display: none;")) fail("The legacy visible grid is not disabled.");
for (const colorContract of [
  ".methodology-grid article:nth-child(n) h3", ".preview-grid article:nth-child(3) h3", "color: var(--text);",
]) if (!adapter.includes(colorContract)) fail(`Network explanatory color hierarchy is incomplete: ${colorContract}.`);
if (!adapter.includes("display: block;") || !adapter.includes("grid-template-columns: none;")) fail("Network legacy header geometry is not neutralized.");
for (const forbidden of [
  "@media (max-width: 900px)", ".site-nav--open", ".nav-toggle", "var(--jl-layout-portfolio-max)",
  ".jl-site-switcher__button", "text-transform: uppercase",
]) if (adapter.includes(forbidden)) fail(`Network adapter re-owns shared header behavior or styling: ${forbidden}.`);

if (/^\s*@layer\b/m.test(identityStyles)) fail("Shared header must remain unlayered so Network button resets cannot override it.");
requireFragments(identityStyles, [
  ".jl-global-header__inner", "grid-template-columns: auto minmax(0, 1fr) auto",
  "height: var(--jl-control-height-md);", "font-family: var(--jl-font-ui);", "font-size: 13px;",
  "font-weight: 700;", "line-height: 1;", '.jl-site-switcher__button > [aria-hidden="true"]',
  "border-right: 2px solid currentColor;", "border-bottom: 2px solid currentColor;",
  ".jl-header-menu-toggle", ".jl-global-header__nav.jl-header-menu--open",
  "right: var(--jl-layout-gutter);", "left: var(--jl-layout-gutter);", "@media (forced-colors: active)",
], "Shared header and compact-menu contract");
const legacyHeaderGeometry = identityStyles.includes("width: 88px;")
  && identityStyles.includes("@media (max-width: 360px)")
  && identityStyles.includes("width: calc(100% - 16px);");
const fittedHeaderGeometry = identityStyles.includes("grid-template-columns: 136px var(--jl-control-height-md);")
  && identityStyles.includes("@media (max-width: 420px)")
  && identityStyles.includes("grid-template-columns: 116px 40px;")
  && identityStyles.includes("width: 116px;");
if (!legacyHeaderGeometry && !fittedHeaderGeometry) fail("Shared header geometry is neither the approved legacy nor fitted transition contract.");

requireFragments(updater, [
  'import { resolveConsumerRelease } from "./design-system-consumer-release.mjs"',
  "resolveConsumerRelease()", "release.version", "release.sourceCommit",
], "Shared design-system release resolver");
requireFragments(consumerRelease, [
  'const REPOSITORY = "JohnnyZLi/Web-Design-System"', "function localPath(value)",
  'relation.startsWith("..")', "export async function resolveConsumerRelease",
  "design-system.lock.json", "api.github.com/repos/${REPOSITORY}/commits/main",
], "Constrained consumer release helper");
if (consumerRelease.includes("child_process") || consumerRelease.includes("exec(")) fail("Consumer helper can execute arbitrary commands.");
requireFragments(synchronizer, [
  'readFile(resolve("design-system.lock.json")', 'styles/content-primitives.css',
  'scripts/consumer-release.mjs", "scripts/design-system-consumer-release.mjs',
  'scripts/conformance-runner.mjs", "scripts/design-system-conformance-runner.mjs',
  'conformance/contract.json", "scripts/design-system-conformance-contract.json',
  "versionMetadata.version !== lockedVersion", "sourceMetadata.includes(sourceCommit)",
], "Design-system synchronizer");
requireFragments(syncWorkflow, [
  "workflow_dispatch:", "schedule:", "contents: write", "pull-requests: write",
  'node-version: "24"', "npm run design-system:check", "npm run design-system:conformance", "npm test", "npm run build",
  "scripts/design-system-consumer-release.mjs", "scripts/design-system-conformance-runner.mjs", "product-name: Network Diagnostics",
], "Shared design-system update workflow caller");
requireImmutableWorkflow(syncWorkflow, "consumer-design-system-sync\\.yml", "Shared design-system update workflow caller");
if (syncWorkflow.includes("gh pr create") || syncWorkflow.includes("git push")) fail("Network workflow still duplicates shared publication behavior.");
requireFragments(conformanceWorkflow, [
  "npm run design-system:check", "npm run design-system:integration", "npm run design-system:conformance", "npm test", "npm run build",
  "network-design-system-conformance",
], "Network conformance workflow caller");
requireImmutableWorkflow(conformanceWorkflow, "consumer-conformance\\.yml", "Network conformance workflow caller");

console.log("Network Diagnostics design-system integration passed.");

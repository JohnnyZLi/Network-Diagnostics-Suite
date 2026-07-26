import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const read = (path) => readFile(resolve(path), "utf8");
const main = await read("src/main.tsx");
const app = await read("src/App.tsx");
const testControls = await read("src/components/TestControls.tsx");
const progress = await read("src/components/ProgressStage.tsx");
const metricCard = await read("src/components/MetricCard.tsx");
const recentResults = await read("src/components/RecentResultsPanel.tsx");
const information = await read("src/components/InformationPanels.tsx");
const deepProbe = await read("src/components/DeepProbePanel.tsx");
const latencyTable = await read("src/components/LatencyTable.tsx");
const serviceMatrix = await read("src/components/ServiceMatrix.tsx");
const resultDashboard = await read("src/components/ResultDashboard.tsx");
const testControlStyles = await read("src/test-controls.css");
const adapter = await read("src/design-system-adapter.css");
const contentAdapter = await read("src/content-system.css");
const identityStyles = await read("src/design-system/site-identity.css");
const contentStyles = await read("src/design-system/content.css");
const version = JSON.parse(await read("src/design-system/version.json"));
const source = await read("src/design-system/SOURCE.md");

const fail = (message) => {
  throw new Error(message);
};

if (version.version !== "1.4.0") fail("Network Diagnostics must consume Web Design System v1.4.0.");
if (!source.includes("ed00dc3897813ea049101926780a443d20dd22c5")) {
  fail("Design-system source commit is not pinned to the reviewed v1.4.0 release.");
}

const requiredImports = [
  "./design-system/tokens.css",
  "./design-system/foundations.css",
  "./design-system/site-identity.css",
  "./design-system/content.css",
  "./styles.css",
  "./history.css",
  "./report-details.css",
  "./ui-polish.css",
  "./test-controls.css",
  "./transfer-color.css",
  "./full-bleed-layout.css",
  "./design-system-adapter.css",
  "./content-system.css",
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
  "const optionTransferCap = optionConfig.downloadCapBytes + optionConfig.uploadCapBytes",
  "up to ${formatBytes(optionTransferCap)}",
  '<small aria-hidden="true">{compactEstimatedTime(optionConfig.estimatedTime)}</small>',
  '<small className="mode-option__cap" aria-hidden="true">≤ {formatBytes(optionTransferCap)}</small>',
  "jl-panel jl-responsive-region",
  "jl-page-meta",
  "jl-callout",
  "jl-button jl-button--primary",
]) {
  if (!testControls.includes(contract)) fail(`Test-profile content contract is incomplete: ${contract}.`);
}
for (const contract of [
  "grid-template-columns: repeat(3, minmax(0, 1fr));",
  "min-height: 84px;",
  ".mode-option__cap",
  "overflow: hidden;",
  "text-overflow: ellipsis;",
  "white-space: nowrap;",
  "@media (max-width: 760px)",
  "grid-template-columns: 1fr;",
]) {
  if (!testControlStyles.includes(contract)) fail(`Test-profile layout contract is incomplete: ${contract}.`);
}

const stateContracts = [
  [app, ["app-shell jl-page", "hero jl-page-hero", "jl-page-title", "jl-page-lede", "jl-page-meta", "error-panel jl-callout jl-callout--danger", "measurement-preview jl-page-section", "preview-grid jl-grid-3", "methodology jl-page-section", "methodology-grid jl-grid-4"]],
  [progress, ["progress-stage jl-panel", "progress-readings jl-metric-grid", "jl-metric__value", "jl-metric__label"]],
  [metricCard, ["metric-card--${tone} jl-panel"]],
  [recentResults, ["recent-results jl-page-section", "comparison-card jl-panel", "jl-page-meta", "jl-empty-state", "history-list jl-stack", "history-row--current jl-panel", "history-row__actions jl-actions"]],
  [information, ["information-grid jl-grid-2", "information-panel jl-panel", "jl-prose", "probe-status jl-callout jl-callout--info"]],
  [deepProbe, ["deep-probe jl-page-section", "jl-page-lede", "jl-actions", "jl-button jl-button--primary", "jl-callout jl-callout--danger", "deep-summary jl-metric-grid", "report-panel local-link-panel jl-panel", "scope-grid jl-grid-3", "deep-table-wrap jl-table-region", "report-columns jl-grid-2", "interface-grid jl-grid-3"]],
  [latencyTable, ["latency-table-wrap jl-table-region"]],
  [serviceMatrix, ["service-grid jl-grid-3", "jl-callout jl-callout--success", "jl-callout jl-callout--danger"]],
  [resultDashboard, ["metric-grid", "findings-panel", "recommendations-panel", "technical-details"]],
];
for (const [sourceText, contracts] of stateContracts) {
  for (const contract of contracts) {
    if (!sourceText.includes(contract)) fail(`Network state is not bound to shared content role: ${contract}.`);
  }
}

const requiredAliases = ["--bg", "--panel", "--line", "--text", "--muted", "--accent", "--radius", "--ease-out"];
for (const alias of requiredAliases) {
  if (!new RegExp(`${alias}:\\s*var\\(--jl-`).test(adapter)) {
    fail(`Network role ${alias} is not mapped to a shared token.`);
  }
}
if (!adapter.includes("var(--jl-color-canvas-dot)")) fail("Shared exact dot canvas is not active.");
if (!adapter.includes("var(--jl-color-focus-ring)")) fail("Shared focus treatment is not active.");
if (!adapter.includes("body::before,") || !adapter.includes("display: none;")) fail("The legacy visible grid is not disabled.");
for (const compactNavContract of [
  "Product navigation becomes a compact secondary row instead of disappearing.",
  ".site-header .site-nav--open",
  "position: absolute;",
  "top: 100%;",
  "display: flex;",
  "border-bottom: 1px solid var(--jl-color-rule);",
]) {
  if (!adapter.includes(compactNavContract)) fail(`Compact Network navigation is incomplete: ${compactNavContract}.`);
}
for (const forbidden of ["var(--jl-layout-portfolio-max)", ".jl-site-switcher__button", "text-transform: uppercase"]) {
  if (adapter.includes(forbidden)) fail(`Network must not re-own shared header styling: ${forbidden}.`);
}

for (const contract of [
  ".jl-page__inner",
  ".jl-page-hero__grid",
  ".jl-page-meta",
  ".jl-page-section__header",
  ".jl-content-grid",
  ".jl-prose",
  ".jl-panel",
  ".jl-process-list",
  ".jl-metric-grid",
  ".jl-callout--success",
  ".jl-button--primary",
  ".jl-table-region",
  ".jl-empty-state",
  "@media (max-width: 560px)",
  "@media (forced-colors: active)",
]) {
  if (!contentStyles.includes(contract)) fail(`Shared page-content contract is incomplete: ${contract}.`);
}
for (const contract of [
  ".hero__copy",
  ".test-controls",
  ".progress-stage",
  ".error-panel",
  ".results > .metric-grid",
  ".findings-panel",
  ".recommendations-panel",
  ".technical-details",
  ".history-list",
  ".information-grid",
  ".deep-probe",
  ".deep-table-wrap",
  ".latency-table-wrap",
  ".service-matrix-wrap",
  "@media (max-width: 560px)",
  "@media (forced-colors: active)",
]) {
  if (!contentAdapter.includes(contract)) fail(`Network content adapter does not cover route/state: ${contract}.`);
}
if (/#[0-9a-fA-F]{3,8}\b|\b(?:rgb|rgba|hsl|hsla)\(/.test(contentAdapter)) {
  fail("Network content adapter contains raw shared colors.");
}
if (/^\s*@layer\b/m.test(identityStyles)) fail("Shared header must remain unlayered so Network resets cannot override it.");

console.log("Web Design System v1.4.0 content integration passed for every Network Diagnostics state.");

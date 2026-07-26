import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const read = (path) => readFile(resolve(path), "utf8");
const fail = (message) => { throw new Error(message); };

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
const contentGuard = await read("src/design-system/content-guard.css");
const version = JSON.parse(await read("src/design-system/version.json"));
const source = await read("src/design-system/SOURCE.md");

if (version.version !== "1.4.0") fail("Network Diagnostics must consume Web Design System v1.4.0.");
if (!source.includes("8a223a383fe1f41000c2fbe34ac5f92c73a1e710")) {
  fail("Network is not pinned to the final reviewed v1.4.0 content source.");
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
  "./design-system/content-guard.css",
  "./design-system-adapter.css",
  "./content-system.css",
];
let previous = -1;
for (const stylesheet of requiredImports) {
  const position = main.indexOf(`import \"${stylesheet}\"`);
  if (position < 0) fail(`Missing stylesheet import ${stylesheet}.`);
  if (position <= previous) fail(`Stylesheet order is incorrect at ${stylesheet}.`);
  previous = position;
}

for (const obsolete of ["portfolio-dots.css", "typography-accent.css"]) {
  if (main.includes(obsolete)) fail(`Obsolete override remains imported: ${obsolete}.`);
  try { await access(resolve("src", obsolete)); fail(`Obsolete override file still exists: ${obsolete}.`); }
  catch (error) { if (error instanceof Error && error.message.startsWith("Obsolete override")) throw error; }
}

for (const hook of [
  'className="site-header jl-global-header"', 'className="jl-global-header__inner"',
  'className="wordmark jl-site-identity"', 'className="jl-site-identity__owner"',
  'className="wordmark__product jl-site-identity__product"', "jl-global-header__nav",
  'className="header-actions jl-global-header__actions"', "owned-sites-menu",
  "siteSwitcherRef", "jl-site-switcher__button",
]) if (!app.includes(hook)) fail(`Missing global-header hook: ${hook}.`);

for (const contract of [
  "function compactEstimatedTime",
  "const optionTransferCap = optionConfig.downloadCapBytes + optionConfig.uploadCapBytes",
  "up to ${formatBytes(optionTransferCap)}",
  '<small className="mode-option__cap" aria-hidden="true">≤ {formatBytes(optionTransferCap)}</small>',
  "jl-panel jl-responsive-region", "jl-page-meta", "jl-callout", "jl-button jl-button--primary",
]) if (!testControls.includes(contract)) fail(`Test-profile content contract is incomplete: ${contract}.`);
for (const contract of [
  "grid-template-columns: repeat(3, minmax(0, 1fr));", "min-height: 84px;",
  ".mode-option__cap", "overflow: hidden;", "@media (max-width: 760px)", "grid-template-columns: 1fr;",
]) if (!testControlStyles.includes(contract)) fail(`Test-profile layout contract is incomplete: ${contract}.`);

const stateContracts = [
  [app, ["app-shell jl-page", "hero jl-page-hero", "jl-page-title", "jl-page-lede", "jl-page-meta", "error-panel jl-callout jl-callout--danger", "measurement-preview jl-page-section", "preview-grid jl-grid-3", "methodology jl-page-section", "methodology-grid jl-grid-4"]],
  [progress, ["progress-stage jl-panel", "progress-readings jl-metric-grid", "jl-metric__value", "jl-metric__label"]],
  [metricCard, ["metric-card--${tone} jl-panel"]],
  [recentResults, ["recent-results jl-page-section", "comparison-card jl-panel", "jl-empty-state", "history-list jl-stack", "history-row__actions jl-actions"]],
  [information, ["information-grid jl-grid-2", "information-panel jl-panel", "jl-prose", "probe-status jl-callout jl-callout--info"]],
  [deepProbe, ["deep-probe jl-page-section", "jl-actions", "jl-button jl-button--primary", "jl-callout jl-callout--danger", "deep-summary jl-metric-grid", "deep-table-wrap jl-table-region", "report-columns jl-grid-2", "interface-grid jl-grid-3"]],
  [latencyTable, ["latency-table-wrap jl-table-region"]],
  [serviceMatrix, ["service-grid jl-grid-3", "jl-callout jl-callout--success", "jl-callout jl-callout--danger"]],
  [resultDashboard, ["metric-grid", "findings-panel", "recommendations-panel", "technical-details"]],
];
for (const [sourceText, contracts] of stateContracts) {
  for (const contract of contracts) if (!sourceText.includes(contract)) fail(`Network state is not bound to shared content role: ${contract}.`);
}

for (const contract of [
  ".jl-page__inner", ".jl-page-hero__grid", ".jl-page-meta", ".jl-page-section__header",
  ".jl-content-grid", ".jl-prose", ".jl-panel", ".jl-process-list", ".jl-metric-grid",
  ".jl-callout--success", ".jl-button--primary", ".jl-table-region", ".jl-empty-state",
  "@media (max-width: 560px)", "@media (forced-colors: active)",
]) if (!contentStyles.includes(contract)) fail(`Shared page-content contract is incomplete: ${contract}.`);
for (const contract of [
  ".jl-page-title", ".jl-page-lede", ".jl-prose", ".jl-editorial-lead",
  ".jl-meta-item dt", ".jl-meta-item dd", ".jl-metric__value",
  ".jl-button--primary", ".jl-code-block", ".jl-surface-inverse .jl-prose",
]) if (!contentGuard.includes(contract)) fail(`Shared content guard is incomplete: ${contract}.`);
if (/^\s*@layer\b/m.test(contentGuard)) fail("Shared content guard must remain unlayered.");

for (const contract of [
  ".hero__copy", ".test-controls", ".progress-stage", ".error-panel",
  ".results > .metric-grid", ".findings-panel", ".recommendations-panel",
  ".technical-details", ".history-list", ".information-grid", ".deep-probe",
  ".deep-table-wrap", ".latency-table-wrap", ".service-matrix-wrap",
  "@media (max-width: 560px)", "@media (forced-colors: active)",
]) if (!contentAdapter.includes(contract)) fail(`Network content adapter does not cover state: ${contract}.`);
if (/#[0-9a-fA-F]{3,8}\b|\b(?:rgb|rgba|hsl|hsla)\(/.test(contentAdapter)) fail("Network content adapter contains raw shared colors.");
if (!adapter.includes("body::before,") || !adapter.includes("display: none;")) fail("The legacy visible grid is not disabled.");
if (/^\s*@layer\b/m.test(identityStyles)) fail("Shared header must remain unlayered.");

console.log("Web Design System v1.4.0 content integration passed for every Network Diagnostics state.");

import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const lock = JSON.parse(await readFile(resolve("design-system.lock.json"), "utf8"));
const sourceCommit = String(lock.sourceCommit ?? "");
const lockedVersion = String(lock.version ?? "");
if (!/^[0-9a-f]{40}$/.test(sourceCommit)) throw new Error("design-system.lock.json has an invalid source commit.");
if (!/^\d+\.\d+\.\d+$/.test(lockedVersion)) throw new Error("design-system.lock.json has an invalid version.");

const sourceRoot = `https://raw.githubusercontent.com/JohnnyZLi/Web-Design-System/${sourceCommit}`;
const write = process.argv.includes("--write");
const files = [
  ["tokens/tokens.css", "src/design-system/tokens.css"],
  ["styles/foundations.css", "src/design-system/foundations.css"],
  ["styles/site-identity.css", "src/design-system/site-identity.css"],
  ["styles/theme-control.css", "src/design-system/theme-control.css"],
  ["styles/content-primitives.css", "src/design-system/content-primitives.css"],
  ["scripts/site-controls.js", "src/design-system/site-controls.js"],
  ["scripts/theme-bootstrap.js", "src/design-system/theme-bootstrap.js"],
  ["scripts/site-controls.d.ts", "src/design-system/site-controls.d.ts"],
  ["scripts/consumer-release.mjs", "scripts/design-system-consumer-release.mjs"],
  ["scripts/conformance-runner.mjs", "scripts/design-system-conformance-runner.mjs"],
  ["conformance/contract.json", "scripts/design-system-conformance-contract.json"],
  ["version.json", "src/design-system/version.json"],
];

const normalize = (value) => value.replaceAll("\r\n", "\n");
for (const [source, destination] of files) {
  const response = await fetch(`${sourceRoot}/${source}`);
  if (!response.ok) throw new Error(`Unable to fetch ${source}: ${response.status} ${response.statusText}`);
  const expected = normalize(await response.text());
  const destinationPath = resolve(destination);
  if (write) {
    await mkdir(dirname(destinationPath), { recursive: true });
    await writeFile(destinationPath, expected, "utf8");
    continue;
  }
  const actual = normalize(await readFile(destinationPath, "utf8"));
  if (actual !== expected) throw new Error(`${destination} drifted from Web Design System commit ${sourceCommit}.`);
}

const versionMetadata = JSON.parse(await readFile(resolve("src/design-system/version.json"), "utf8"));
if (versionMetadata.version !== lockedVersion) throw new Error("Generated design-system version does not match the lock.");
const sourceMetadata = await readFile(resolve("src/design-system/SOURCE.md"), "utf8");
if (!sourceMetadata.includes(sourceCommit) || !sourceMetadata.includes(`Version: ${lockedVersion}`)) {
  throw new Error("Design-system source metadata is not pinned to the lock.");
}
console.log(`${write ? "Synced" : "Validated"} Web Design System v${lockedVersion} at ${sourceCommit}.`);

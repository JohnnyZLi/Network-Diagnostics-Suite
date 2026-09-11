import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const PACKAGE = "@johnnyzli/web-design-system";
const RETRYABLE_STATUS = new Set([408, 425, 429, 500, 502, 503, 504]);
const MAX_ATTEMPTS = 4;
const BASE_DELAY_MS = 250;
const MAX_DELAY_MS = 4000;

const lock = JSON.parse(await readFile(resolve("design-system.lock.json"), "utf8"));
const lockedPackage = String(lock.package ?? "");
const sourceCommit = String(lock.sourceCommit ?? "");
const lockedVersion = String(lock.version ?? "");
if (lockedPackage !== PACKAGE) throw new Error("design-system.lock.json has an unexpected package.");
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
  ["scripts/conformance-runner.mjs", "scripts/design-system-conformance-runner.mjs"],
  ["conformance/contract.json", "scripts/design-system-conformance-contract.json"],
  ["version.json", "src/design-system/version.json"],
];

const normalize = (value) => value.replaceAll("\r\n", "\n");
const sleep = (milliseconds) => new Promise((resolvePromise) => setTimeout(resolvePromise, milliseconds));

function retryAfterMilliseconds(response) {
  const value = response.headers?.get?.("retry-after");
  if (!value) return null;
  if (/^\d+(?:\.\d+)?$/.test(value)) return Math.max(0, Number(value) * 1000);
  const when = Date.parse(value);
  return Number.isFinite(when) ? Math.max(0, when - Date.now()) : null;
}

function retryable(response) {
  const status = Number(response.status);
  if (RETRYABLE_STATUS.has(status) || status >= 500) return true;
  if (status !== 403) return false;
  return Boolean(response.headers?.get?.("retry-after")) || response.headers?.get?.("x-ratelimit-remaining") === "0";
}

async function fetchText(source) {
  const url = `${sourceRoot}/${source}`;
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
    let response;
    try {
      response = await fetch(url);
    } catch (error) {
      if (attempt === MAX_ATTEMPTS) {
        throw new Error(`Unable to fetch ${source} after ${attempt} attempts: ${error?.message ?? error}`);
      }
      await sleep(Math.min(BASE_DELAY_MS * (2 ** (attempt - 1)), MAX_DELAY_MS));
      continue;
    }

    if (response.ok) return normalize(await response.text());
    if (!retryable(response) || attempt === MAX_ATTEMPTS) {
      throw new Error(`Unable to fetch ${source}: ${response.status} ${response.statusText}`);
    }
    const requestedDelay = retryAfterMilliseconds(response);
    await sleep(Math.min(requestedDelay ?? BASE_DELAY_MS * (2 ** (attempt - 1)), MAX_DELAY_MS));
  }
  throw new Error(`Unable to fetch ${source}.`);
}

for (const [source, destination] of files) {
  const expected = await fetchText(source);
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

const sourceMetadataPath = resolve("src/design-system/SOURCE.md");
const expectedSourceMetadata = [
  "# Generated design-system assets",
  "",
  "Do not edit these files directly.",
  "",
  `Package: ${lockedPackage}`,
  `Version: ${lockedVersion}`,
  `Source commit: ${sourceCommit}`,
  "",
  "Validate with `npm run design-system:check`.",
  "",
].join("\n");
if (write) await writeFile(sourceMetadataPath, expectedSourceMetadata, "utf8");
const sourceMetadata = normalize(await readFile(sourceMetadataPath, "utf8"));
if (sourceMetadata !== expectedSourceMetadata) throw new Error("Design-system source metadata is not pinned exactly to the lock.");

console.log(`${write ? "Synced" : "Validated"} Web Design System v${lockedVersion} at ${sourceCommit}.`);

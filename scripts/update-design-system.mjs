import { writeFile } from "node:fs/promises";
import { resolve } from "node:path";

const repository = "JohnnyZLi/Web-Design-System";
const commitResponse = await fetch(`https://api.github.com/repos/${repository}/commits/main`, {
  headers: {
    accept: "application/vnd.github+json",
    "user-agent": "Network-Diagnostics-Design-System-Updater/1.0",
  },
});
if (!commitResponse.ok) throw new Error(`Unable to resolve design-system main: ${commitResponse.status} ${commitResponse.statusText}`);
const commit = await commitResponse.json();
const sourceCommit = String(commit.sha ?? "");
if (!/^[0-9a-f]{40}$/.test(sourceCommit)) throw new Error("Design system returned an invalid commit SHA.");

const versionResponse = await fetch(`https://raw.githubusercontent.com/${repository}/${sourceCommit}/version.json`);
if (!versionResponse.ok) throw new Error(`Unable to read design-system version: ${versionResponse.status} ${versionResponse.statusText}`);
const versionMetadata = await versionResponse.json();
const version = String(versionMetadata.version ?? "");
if (!/^\d+\.\d+\.\d+$/.test(version)) throw new Error("Design system returned an invalid version.");

await writeFile(
  resolve("design-system.lock.json"),
  `${JSON.stringify({ package: "@johnnyzli/web-design-system", version, sourceCommit }, null, 2)}\n`,
  "utf8",
);
console.log(`Locked Web Design System v${version} at ${sourceCommit}.`);

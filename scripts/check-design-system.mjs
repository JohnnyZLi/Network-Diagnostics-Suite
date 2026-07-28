import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const sourceCommit = "14fc1281f02d3a1fa33e6d80aae24637d93b04f7";
const sourceRoot = `https://raw.githubusercontent.com/JohnnyZLi/Web-Design-System/${sourceCommit}`;
const write = process.argv.includes("--write");
const files = [
  ["tokens/tokens.css", "src/design-system/tokens.css"],
  ["styles/foundations.css", "src/design-system/foundations.css"],
  ["styles/site-identity.css", "src/design-system/site-identity.css"],
  ["scripts/site-controls.js", "src/design-system/site-controls.js"],
  ["scripts/site-controls.d.ts", "src/design-system/site-controls.d.ts"],
  ["version.json", "src/design-system/version.json"],
];

const normalize = (value) => value.replaceAll("\r\n", "\n");

for (const [source, destination] of files) {
  const response = await fetch(`${sourceRoot}/${source}`);
  if (!response.ok) {
    throw new Error(`Unable to fetch ${source}: ${response.status} ${response.statusText}`);
  }

  const expected = normalize(await response.text());
  const destinationPath = resolve(destination);

  if (write) {
    await mkdir(dirname(destinationPath), { recursive: true });
    await writeFile(destinationPath, expected, "utf8");
    continue;
  }

  const actual = normalize(await readFile(destinationPath, "utf8"));
  if (actual !== expected) {
    throw new Error(`${destination} drifted from Web Design System commit ${sourceCommit}.`);
  }
}

const sourceMetadata = await readFile(resolve("src/design-system/SOURCE.md"), "utf8");
if (!sourceMetadata.includes(sourceCommit)) {
  throw new Error("Design-system source metadata is not pinned to the validated commit.");
}

console.log(`${write ? "Synced" : "Validated"} Web Design System v1.5.0 at ${sourceCommit}.`);

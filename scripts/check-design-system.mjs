import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const sourceCommit = "27f83fa7333903a38c2c5ca36ed0455fa71598fc";
const sourceRoot = `https://raw.githubusercontent.com/JohnnyZLi/Web-Design-System/${sourceCommit}`;
const write = process.argv.includes("--write");
const files = [
  ["tokens/tokens.css", "src/design-system/tokens.css"],
  ["styles/foundations.css", "src/design-system/foundations.css"],
  ["styles/site-identity.css", "src/design-system/site-identity.css"],
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

console.log(`${write ? "Synced" : "Validated"} Web Design System v1.3.4 at ${sourceCommit}.`);

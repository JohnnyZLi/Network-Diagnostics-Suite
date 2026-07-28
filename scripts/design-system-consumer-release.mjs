import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, relative, resolve } from "node:path";

const REPOSITORY = "JohnnyZLi/Web-Design-System";
const PACKAGE = "@johnnyzli/web-design-system";
const SHA = /^[0-9a-f]{40}$/;
const VERSION = /^\d+\.\d+\.\d+$/;

function localPath(value) {
  const root = resolve(".");
  const destination = resolve(String(value));
  const relation = relative(root, destination);
  if (!relation || relation.startsWith("..") || relation.includes("../")) {
    throw new Error(`Consumer path must stay inside the repository: ${value}`);
  }
  return destination;
}

async function json(path) {
  return JSON.parse(await readFile(localPath(path), "utf8"));
}

async function writeJson(path, value) {
  const destination = localPath(path);
  await mkdir(dirname(destination), { recursive: true });
  await writeFile(destination, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

async function request(url, accept = "application/vnd.github+json") {
  const response = await fetch(url, {
    headers: { accept, "user-agent": "Johnny-Li-Design-System-Consumer/1.0" },
  });
  if (!response.ok) throw new Error(`Design-system request failed: ${response.status} ${response.statusText}`);
  return response;
}

export async function resolveConsumerRelease({
  lockFile = "design-system.lock.json",
  packageJson = null,
} = {}) {
  const commit = await (await request(`https://api.github.com/repos/${REPOSITORY}/commits/main`)).json();
  const sourceCommit = String(commit.sha ?? "");
  if (!SHA.test(sourceCommit)) throw new Error("Design system returned an invalid commit SHA.");

  const metadata = await (await request(
    `https://raw.githubusercontent.com/${REPOSITORY}/${sourceCommit}/version.json`,
    "application/json",
  )).json();
  const version = String(metadata.version ?? "");
  if (!VERSION.test(version)) throw new Error("Design system returned an invalid semantic version.");

  await writeJson(lockFile, { package: PACKAGE, version, sourceCommit });

  if (packageJson) {
    const manifest = await json(packageJson);
    manifest.dependencies ??= {};
    manifest.dependencies[PACKAGE] = `github:${REPOSITORY}#${sourceCommit}`;
    await writeJson(packageJson, manifest);
  }

  return { package: PACKAGE, version, sourceCommit };
}

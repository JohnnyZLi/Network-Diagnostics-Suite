import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

const script = resolve("scripts/verify-workers-build-context.mjs");

function run(env) {
  return spawnSync(process.execPath, [script], {
    env: { ...process.env, WORKERS_CI: "", WORKERS_CI_BRANCH: "", ...env },
    encoding: "utf8",
  });
}

const cases = [
  { name: "local build", env: {}, expected: 0 },
  { name: "production Workers build", env: { WORKERS_CI: "1", WORKERS_CI_BRANCH: "main" }, expected: 0 },
  { name: "feature Workers build", env: { WORKERS_CI: "1", WORKERS_CI_BRANCH: "agent/desktop-orb-product-rebuild" }, expected: 1 },
  { name: "ambiguous Workers build", env: { WORKERS_CI: "1" }, expected: 1 },
];

for (const testCase of cases) {
  const result = run(testCase.env);
  if (result.status !== testCase.expected) {
    throw new Error(
      `${testCase.name} exited ${result.status}; expected ${testCase.expected}.\nstdout:\n${result.stdout}\nstderr:\n${result.stderr}`,
    );
  }
}

console.log("Workers production branch guard passed.");

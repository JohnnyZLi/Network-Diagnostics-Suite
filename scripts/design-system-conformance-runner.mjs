#!/usr/bin/env node
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const PRODUCTS = new Set(["portfolio", "network", "rolepacket"]);
const MANUAL_STATUSES = new Set(["manual-passed", "manual-pending", "manual-failed"]);
const scriptRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function parseArguments(argv) {
  const options = {
    manifest: "design-system.conformance.json",
    contract: resolve(scriptRoot, "conformance/contract.json"),
    root: resolve("."),
    output: null,
    strictManual: false,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--strict-manual") {
      options.strictManual = true;
      continue;
    }
    const value = argv[index + 1];
    if (!value || value.startsWith("--")) throw new Error(`Missing value for ${argument}.`);
    if (argument === "--manifest") options.manifest = value;
    else if (argument === "--contract") options.contract = value;
    else if (argument === "--root") options.root = resolve(value);
    else if (argument === "--output") options.output = value;
    else throw new Error(`Unknown argument: ${argument}`);
    index += 1;
  }
  return options;
}

function confined(root, value, label) {
  const destination = resolve(root, String(value));
  const relation = relative(root, destination);
  const separator = process.platform === "win32" ? "\\" : "/";
  if (relation === "" || (!relation.startsWith("..") && !relation.includes(`..${separator}`))) return destination;
  throw new Error(`${label} must stay inside the consumer repository: ${value}`);
}

async function readJson(path) {
  return JSON.parse(await readFile(path, "utf8"));
}

function valueAtPath(value, path) {
  if (!path) return value;
  return String(path).split(".").reduce((current, key) => {
    if (current === null || typeof current !== "object" || !(key in current)) {
      throw new Error(`JSON path does not exist: ${path}`);
    }
    return current[key];
  }, value);
}

function list(value) {
  if (Array.isArray(value)) return value.map(String);
  if (value === undefined) return [];
  return [String(value)];
}

async function evaluateEvidence(root, evidence) {
  if (!evidence || typeof evidence !== "object" || Array.isArray(evidence)) {
    throw new Error("Evidence must be an object.");
  }
  const type = String(evidence.type ?? "");
  const file = evidence.file ? confined(root, evidence.file, "Evidence file") : null;
  if (type === "file-exists") {
    try {
      await readFile(file);
      return { passed: true, detail: `${evidence.file} exists` };
    } catch {
      return { passed: false, detail: `${evidence.file} is missing` };
    }
  }
  if (!file) throw new Error(`${type} evidence requires a file.`);

  if (type === "json-equals" || type === "json-matches") {
    const actual = valueAtPath(await readJson(file), evidence.path);
    if (type === "json-equals") {
      const passed = JSON.stringify(actual) === JSON.stringify(evidence.value);
      return { passed, detail: `${evidence.file}#${evidence.path} ${passed ? "matches" : `was ${JSON.stringify(actual)}`}` };
    }
    const expression = new RegExp(String(evidence.pattern), String(evidence.flags ?? ""));
    const passed = expression.test(String(actual));
    return { passed, detail: `${evidence.file}#${evidence.path} ${passed ? "matches" : "does not match"} /${expression.source}/` };
  }

  const content = await readFile(file, "utf8");
  if (type === "contains" || type === "not-contains") {
    const values = list(evidence.values ?? evidence.value);
    if (values.length === 0) throw new Error(`${type} evidence requires value or values.`);
    const found = values.filter((fragment) => content.includes(fragment));
    const passed = type === "contains" ? found.length === values.length : found.length === 0;
    const detail = type === "contains"
      ? `${evidence.file} contains ${found.length}/${values.length} required fragments`
      : `${evidence.file} contains ${found.length}/${values.length} forbidden fragments`;
    return { passed, detail };
  }
  if (type === "matches") {
    const expression = new RegExp(String(evidence.pattern), String(evidence.flags ?? ""));
    const passed = expression.test(content);
    return { passed, detail: `${evidence.file} ${passed ? "matches" : "does not match"} /${expression.source}/` };
  }
  throw new Error(`Unsupported evidence type: ${type}`);
}

function validateContract(contract) {
  if (!/^\d+\.\d+\.\d+$/.test(String(contract.schemaVersion ?? ""))) throw new Error("Contract schemaVersion is invalid.");
  if (!/^\d+\.\d+\.\d+$/.test(String(contract.designSystemVersion ?? ""))) throw new Error("Contract designSystemVersion is invalid.");
  if (!Array.isArray(contract.rules) || contract.rules.length === 0) throw new Error("Contract has no rules.");
  const ids = new Set();
  for (const rule of contract.rules) {
    if (!/^DS-[A-Z0-9]+-\d{3}$/.test(String(rule.id ?? ""))) throw new Error(`Invalid rule ID: ${rule.id}`);
    if (ids.has(rule.id)) throw new Error(`Duplicate rule ID: ${rule.id}`);
    ids.add(rule.id);
    if (!Array.isArray(rule.appliesTo) || rule.appliesTo.some((product) => !PRODUCTS.has(product))) {
      throw new Error(`Rule ${rule.id} has invalid products.`);
    }
    if (!["required", "advisory", "manual"].includes(rule.severity)) throw new Error(`Rule ${rule.id} has invalid severity.`);
  }
}

function validateManifest(manifest) {
  if (manifest.schemaVersion !== "1.0.0") throw new Error("Consumer manifest schemaVersion must be 1.0.0.");
  if (!PRODUCTS.has(manifest.product)) throw new Error(`Unknown product: ${manifest.product}`);
  if (!manifest.rules || typeof manifest.rules !== "object" || Array.isArray(manifest.rules)) throw new Error("Consumer manifest rules must be an object.");
}

function markdown(report) {
  const rows = report.results.map((result) => `| ${result.id} | ${result.severity} | ${result.status} | ${String(result.detail ?? "").replaceAll("|", "\\|")} |`);
  return [
    `# Design-system conformance: ${report.product}`,
    "",
    `- Contract: ${report.contractVersion}`,
    `- Design system: ${report.designSystemVersion}`,
    `- Generated: ${report.generatedAt}`,
    `- Blocking failures: ${report.summary.blockingFailures}`,
    `- Manual pending: ${report.summary.manualPending}`,
    "",
    "| Rule | Severity | Status | Evidence |",
    "| --- | --- | --- | --- |",
    ...rows,
    "",
  ].join("\n");
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const root = resolve(options.root);
  const manifestPath = confined(root, options.manifest, "Manifest");
  const contractPath = resolve(options.contract);
  const [contract, manifest] = await Promise.all([readJson(contractPath), readJson(manifestPath)]);
  validateContract(contract);
  validateManifest(manifest);

  const declaredIds = new Set(Object.keys(manifest.rules));
  const knownIds = new Set(contract.rules.map((rule) => rule.id));
  for (const id of declaredIds) if (!knownIds.has(id)) throw new Error(`Manifest declares unknown rule: ${id}`);

  const results = [];
  for (const rule of contract.rules) {
    if (!rule.appliesTo.includes(manifest.product)) {
      results.push({ id: rule.id, severity: rule.severity, status: "not-applicable", detail: "Rule does not apply to this product." });
      continue;
    }
    const declaration = manifest.rules[rule.id];
    if (!declaration) {
      results.push({ id: rule.id, severity: rule.severity, status: "failed", detail: "No conformance declaration was provided." });
      continue;
    }
    if (MANUAL_STATUSES.has(declaration.status)) {
      results.push({
        id: rule.id,
        severity: rule.severity,
        status: declaration.status,
        detail: String(declaration.reason ?? "Manual review status declared."),
      });
      continue;
    }
    if (declaration.status === "not-applicable") {
      results.push({ id: rule.id, severity: rule.severity, status: "failed", detail: "Applicable rules cannot be marked not-applicable." });
      continue;
    }
    const evidence = Array.isArray(declaration.evidence) ? declaration.evidence : [];
    if (evidence.length === 0) {
      results.push({ id: rule.id, severity: rule.severity, status: "failed", detail: "No automated evidence was provided." });
      continue;
    }
    const checks = [];
    for (const item of evidence) {
      try {
        checks.push(await evaluateEvidence(root, item));
      } catch (error) {
        checks.push({ passed: false, detail: error instanceof Error ? error.message : String(error) });
      }
    }
    const passed = checks.every((check) => check.passed);
    results.push({
      id: rule.id,
      severity: rule.severity,
      status: passed ? "passed" : "failed",
      detail: checks.map((check) => `${check.passed ? "PASS" : "FAIL"}: ${check.detail}`).join("; "),
    });
  }

  const blockingFailures = results.filter((result) => result.status === "failed" && result.severity === "required").length;
  const advisoryFailures = results.filter((result) => result.status === "failed" && result.severity === "advisory").length;
  const manualPending = results.filter((result) => result.status === "manual-pending").length;
  const manualFailed = results.filter((result) => result.status === "manual-failed").length;
  const outputDirectory = confined(root, options.output ?? manifest.outputDirectory ?? "design-system-conformance", "Output directory");
  const report = {
    schemaVersion: "1.0.0",
    product: manifest.product,
    contractVersion: contract.schemaVersion,
    designSystemVersion: contract.designSystemVersion,
    generatedAt: new Date().toISOString(),
    summary: { blockingFailures, advisoryFailures, manualPending, manualFailed },
    results,
  };
  await mkdir(outputDirectory, { recursive: true });
  await Promise.all([
    writeFile(resolve(outputDirectory, "report.json"), `${JSON.stringify(report, null, 2)}\n`, "utf8"),
    writeFile(resolve(outputDirectory, "report.md"), markdown(report), "utf8"),
  ]);
  console.log(`Design-system conformance: ${manifest.product}: ${blockingFailures} blocking failure(s), ${manualPending} manual item(s) pending.`);
  if (blockingFailures > 0 || manualFailed > 0 || (options.strictManual && manualPending > 0)) process.exitCode = 1;
}

main().catch((error) => {
  console.error(`Conformance error: ${error instanceof Error ? error.message : String(error)}`);
  process.exitCode = 1;
});

#!/usr/bin/env node
import { createHash } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { gzipSync } from "node:zlib";
import { mkdir, readFile, readdir, stat, writeFile } from "node:fs/promises";
import { relative, resolve } from "node:path";
import { chromium } from "playwright";

const root = resolve(".");
const assetRoot = resolve(root, "dist");
const outputDirectory = resolve(root, "performance-baseline");
const baseUrl = process.env.PERFORMANCE_BASE_URL ?? "http://127.0.0.1:4173";
const runs = Number.parseInt(process.env.PERFORMANCE_RUNS ?? "3", 10);
const npx = process.platform === "win32" ? "npx.cmd" : "npx";

async function walk(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) files.push(...await walk(path));
    else if (entry.isFile()) files.push(path);
  }
  return files;
}

function median(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

function round(value) {
  return Math.round(value * 100) / 100;
}

async function waitForPreview(preview) {
  for (let attempt = 1; attempt <= 60; attempt += 1) {
    if (preview.exitCode !== null) throw new Error(`Vite preview exited before becoming ready with status ${preview.exitCode}.`);
    try {
      const response = await fetch(baseUrl);
      if (response.ok) return;
    } catch {
      // Preview is still starting.
    }
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 500));
  }
  throw new Error(`Timed out waiting for ${baseUrl}.`);
}

async function stopPreview(preview) {
  if (!preview || preview.exitCode !== null || preview.killed) return;
  if (process.platform === "win32") spawnSync("taskkill", ["/pid", String(preview.pid), "/t", "/f"], { stdio: "ignore" });
  else preview.kill("SIGTERM");
  await new Promise((resolvePromise) => setTimeout(resolvePromise, 250));
}

async function measure(browser, viewport) {
  const context = await browser.newContext({ viewport, reducedMotion: "reduce" });
  const page = await context.newPage();
  await page.addInitScript(() => {
    window.__networkPerformance = { lcp: 0, cls: 0, longTasks: 0 };
    new PerformanceObserver((list) => {
      const last = list.getEntries().at(-1);
      if (last) window.__networkPerformance.lcp = last.startTime;
    }).observe({ type: "largest-contentful-paint", buffered: true });
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) if (!entry.hadRecentInput) window.__networkPerformance.cls += entry.value;
    }).observe({ type: "layout-shift", buffered: true });
    new PerformanceObserver((list) => {
      window.__networkPerformance.longTasks += list.getEntries().length;
    }).observe({ type: "longtask", buffered: true });
  });
  await page.goto(baseUrl, { waitUntil: "networkidle" });
  await page.locator("#root").waitFor({ state: "attached" });
  await page.waitForTimeout(500);
  const result = await page.evaluate(() => {
    const navigation = performance.getEntriesByType("navigation")[0];
    const paints = Object.fromEntries(performance.getEntriesByType("paint").map((entry) => [entry.name, entry.startTime]));
    const resources = performance.getEntriesByType("resource");
    return {
      domContentLoadedMs: navigation.domContentLoadedEventEnd,
      loadMs: navigation.loadEventEnd,
      firstContentfulPaintMs: paints["first-contentful-paint"] ?? 0,
      largestContentfulPaintMs: window.__networkPerformance.lcp,
      cumulativeLayoutShift: window.__networkPerformance.cls,
      longTasks: window.__networkPerformance.longTasks,
      transferBytes: resources.reduce((sum, entry) => sum + entry.transferSize, 0),
      decodedBodyBytes: resources.reduce((sum, entry) => sum + entry.decodedBodySize, 0),
      resourceCount: resources.length,
      domNodes: document.getElementsByTagName("*").length,
    };
  });
  await context.close();
  return result;
}

await stat(assetRoot).catch(() => { throw new Error("dist is missing. Run the production build before recording a baseline."); });
if (!Number.isInteger(runs) || runs < 1 || runs > 10) throw new Error("PERFORMANCE_RUNS must be an integer from 1 through 10.");
const files = await walk(assetRoot);
const assets = [];
for (const path of files) {
  const body = await readFile(path);
  assets.push({ path: relative(assetRoot, path), bytes: body.byteLength, gzipBytes: gzipSync(body, { level: 9 }).byteLength, sha256: createHash("sha256").update(body).digest("hex") });
}
assets.sort((a, b) => b.gzipBytes - a.gzipBytes);
await mkdir(outputDirectory, { recursive: true });
const managedPreview = !process.env.PERFORMANCE_BASE_URL;
const preview = managedPreview ? spawn(npx, ["--no-install", "vite", "preview", "--host", "127.0.0.1", "--port", "4173"], { cwd: root, env: process.env, stdio: "inherit" }) : null;
let browser;
const viewports = { desktop: { width: 1440, height: 1000 }, mobile: { width: 390, height: 844 } };
const samples = {};
try {
  if (preview) await waitForPreview(preview);
  browser = await chromium.launch({ headless: true });
  for (const [name, viewport] of Object.entries(viewports)) {
    samples[name] = [];
    for (let index = 0; index < runs; index += 1) samples[name].push(await measure(browser, viewport));
  }
} finally {
  await browser?.close();
  await stopPreview(preview);
}
const metrics = Object.fromEntries(Object.entries(samples).map(([name, results]) => [name, Object.fromEntries(Object.keys(results[0]).map((key) => [key, round(median(results.map((result) => result[key])))]))]));
const totals = assets.reduce((value, asset) => ({ bytes: value.bytes + asset.bytes, gzipBytes: value.gzipBytes + asset.gzipBytes }), { bytes: 0, gzipBytes: 0 });
const report = {
  schemaVersion: "1.0.0",
  product: "network",
  commit: process.env.GITHUB_SHA ?? null,
  recordedAt: new Date().toISOString(),
  environment: { platform: process.platform, architecture: process.arch, node: process.version, runs },
  methodology: "Median local production-preview measurements at desktop and mobile widths with reduced motion and the idle application state.",
  assets: { totals, largestByGzip: assets.slice(0, 25) },
  metrics,
};
await writeFile(resolve(outputDirectory, "report.json"), `${JSON.stringify(report, null, 2)}\n`);
const rows = Object.entries(metrics).flatMap(([viewport, values]) => Object.entries(values).map(([metric, value]) => `| ${viewport} | ${metric} | ${value} |`));
const markdown = [
  "# Network Diagnostics performance baseline",
  "",
  `Recorded: ${report.recordedAt}`,
  `Commit: ${report.commit ?? "local working tree"}`,
  `Environment: ${process.platform} ${process.arch}, ${process.version}, ${runs} runs`,
  "",
  `Production assets: ${totals.bytes} bytes raw; ${totals.gzipBytes} bytes gzip.`,
  "",
  "| Viewport | Metric | Median |",
  "| --- | --- | ---: |",
  ...rows,
  "",
].join("\n");
await writeFile(resolve(outputDirectory, "report.md"), markdown);
console.log(markdown);

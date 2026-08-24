#!/usr/bin/env node
import { mkdir, writeFile } from "node:fs/promises";
import { chromium } from "playwright";

const baseUrl = process.env.THEME_AUDIT_BASE_URL ?? "http://127.0.0.1:4173";
const output = "theme-visual-audit";

const parseRgb = (value) => {
  const channels = value.match(/[\d.]+/g)?.slice(0, 3).map(Number);
  return channels?.length === 3 ? channels : null;
};

const luminance = (channels) => channels
  .map((channel) => channel / 255)
  .map((channel) => (channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4))
  .reduce((sum, channel, index) => sum + channel * [0.2126, 0.7152, 0.0722][index], 0);

const contrast = (foreground, background) => {
  const fg = parseRgb(foreground);
  const bg = parseRgb(background);
  if (!fg || !bg) return null;
  const [lighter, darker] = [luminance(fg), luminance(bg)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
};

await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, colorScheme: "dark", reducedMotion: "reduce" });
await context.addInitScript(() => localStorage.setItem("jl-theme", "dark"));
const page = await context.newPage();
const problems = [];

try {
  const response = await page.goto(baseUrl, { waitUntil: "networkidle" });
  if (!response?.ok()) problems.push(`HTTP ${response?.status() ?? "no response"}`);

  const disclosure = page.locator(".advanced-path");
  await disclosure.waitFor({ state: "visible" });
  await disclosure.locator("summary").click();
  await page.waitForFunction(() => document.querySelector(".advanced-path")?.hasAttribute("open"));

  const state = await disclosure.evaluate((element) => {
    const summary = element.querySelector("summary");
    const selector = element.querySelector(".path-selector");
    const active = element.querySelector(".mode-option--active");
    const style = (target) => target ? getComputedStyle(target) : null;
    const root = document.documentElement;
    return {
      theme: root.dataset.theme,
      documentWidth: root.scrollWidth,
      innerWidth: window.innerWidth,
      surface: style(element)?.backgroundColor ?? "",
      border: style(element)?.borderColor ?? "",
      summaryColor: style(summary)?.color ?? "",
      selectorSurface: style(selector)?.backgroundColor ?? "",
      activeSurface: style(active)?.backgroundColor ?? "",
      activeColor: style(active)?.color ?? "",
    };
  });

  const surfaceRgb = parseRgb(state.surface);
  const summaryContrast = contrast(state.summaryColor, state.surface);
  const activeContrast = contrast(state.activeColor, state.activeSurface);

  if (state.theme !== "dark") problems.push(`resolved theme is ${state.theme}`);
  if (state.documentWidth > state.innerWidth + 1) problems.push("advanced path introduced document overflow");
  if (!surfaceRgb || luminance(surfaceRgb) > 0.12) problems.push(`advanced path surface is too light: ${state.surface}`);
  if (summaryContrast === null || summaryContrast < 4.5) problems.push(`advanced path summary contrast is ${summaryContrast?.toFixed(2) ?? "unreadable"}`);
  if (activeContrast === null || activeContrast < 4.5) problems.push(`active path option contrast is ${activeContrast?.toFixed(2) ?? "unreadable"}`);
  if (state.surface === state.selectorSurface) problems.push("advanced path shell and inset selector lost surface hierarchy");

  await disclosure.screenshot({ path: `${output}/network-advanced-path-desktop-dark.png` });
  await writeFile(`${output}/advanced-path-dark-report.json`, JSON.stringify({ state, summaryContrast, activeContrast, problems }, null, 2));
} finally {
  await context.close();
  await browser.close();
}

if (problems.length) {
  console.error("Advanced download path dark-mode audit failures:", problems);
  process.exitCode = 1;
} else {
  console.log("Advanced download path dark-mode audit passed.");
}

#!/usr/bin/env node
import { mkdir, writeFile } from "node:fs/promises";
import { chromium } from "playwright";

const baseUrl = process.env.THEME_AUDIT_BASE_URL ?? "http://127.0.0.1:4173";
const output = "theme-visual-audit";
const viewports = [
  ["desktop", { width: 1440, height: 1000 }],
  ["mobile", { width: 390, height: 844 }],
  ["minimum", { width: 320, height: 700 }],
];

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
const results = [];

try {
  for (const [viewportName, viewport] of viewports) {
    for (const theme of ["light", "dark"]) {
      const context = await browser.newContext({ viewport, colorScheme: "light", reducedMotion: "reduce" });
      await context.addInitScript((value) => localStorage.setItem("jl-theme", value), theme);
      const page = await context.newPage();
      const response = await page.goto(baseUrl, { waitUntil: "networkidle" });
      await page.locator("[data-site-switcher-button]").waitFor({ state: "visible" });
      await page.screenshot({ path: `${output}/network-${viewportName}-${theme}.png`, fullPage: true });

      const state = await page.evaluate(() => {
        const root = document.documentElement;
        const bodyStyle = getComputedStyle(document.body);
        const rootStyle = getComputedStyle(root);
        return {
          preference: root.dataset.themePreference,
          theme: root.dataset.theme,
          colorScheme: rootStyle.colorScheme,
          canvas: rootStyle.getPropertyValue("--jl-color-canvas").trim(),
          ink: rootStyle.getPropertyValue("--jl-color-ink").trim(),
          bodyColor: bodyStyle.color,
          bodyBackground: bodyStyle.backgroundColor,
          innerWidth: window.innerWidth,
          documentWidth: root.scrollWidth,
        };
      });
      const problems = [];
      if (!response?.ok()) problems.push(`HTTP ${response?.status() ?? "no response"}`);
      if (state.preference !== theme || state.theme !== theme) problems.push(`resolved ${state.preference}/${state.theme}, expected ${theme}`);
      if (!state.colorScheme.includes(theme)) problems.push(`color-scheme is ${state.colorScheme}`);
      if (state.documentWidth > state.innerWidth + 1) problems.push("horizontal overflow");
      const bodyContrast = contrast(state.bodyColor, state.bodyBackground);
      if (bodyContrast === null || bodyContrast < 4.5) problems.push(`body contrast is ${bodyContrast?.toFixed(2) ?? "unreadable"}`);

      const switcher = page.locator("[data-site-switcher-button]").first();
      await switcher.click();
      const menu = page.locator("[data-site-switcher-menu]").first();
      await menu.waitFor({ state: "visible" });
      const links = await menu.locator("a[href]").count();
      const themeButtons = await menu.locator("button[data-theme-preference]").count();
      const selected = await menu.locator('button[data-theme-preference][aria-pressed="true"]').getAttribute("data-theme-preference");
      if (links !== 3) problems.push(`Sites menu has ${links} links`);
      if (themeButtons !== 3) problems.push(`Appearance has ${themeButtons} options`);
      if (selected !== theme) problems.push(`selected appearance is ${selected}`);
      await menu.screenshot({ path: `${output}/network-menu-${viewportName}-${theme}.png` });

      const selectedButton = menu.locator(`button[data-theme-preference="${theme}"]`);
      const previousPreference = theme === "dark" ? "light" : "system";
      await menu.locator(`button[data-theme-preference="${previousPreference}"]`).focus();
      await page.keyboard.press("Tab");
      const focus = await selectedButton.evaluate((element) => {
        const style = getComputedStyle(element);
        return {
          active: document.activeElement === element,
          focusVisible: element.matches(":focus-visible"),
          outlineStyle: style.outlineStyle,
          outlineWidth: style.outlineWidth,
          boxShadow: style.boxShadow,
        };
      });
      if (!focus.active || !focus.focusVisible || ((focus.outlineStyle === "none" || focus.outlineWidth === "0px") && focus.boxShadow === "none")) {
        problems.push("Appearance control has no keyboard-visible focus treatment");
      }

      const opposite = theme === "dark" ? "light" : "dark";
      await menu.locator(`button[data-theme-preference="${opposite}"]`).click();
      await page.waitForFunction((value) => document.documentElement.dataset.theme === value, opposite);
      const changed = await page.evaluate(() => ({
        preference: window.JLTheme?.getPreference(),
        theme: window.JLTheme?.getTheme(),
        stored: localStorage.getItem("jl-theme"),
        cookie: document.cookie,
      }));
      if (changed.preference !== opposite || changed.theme !== opposite || changed.stored !== opposite) problems.push("Appearance selection did not synchronize state");
      if (!changed.cookie.includes(`jl-theme=${opposite}`)) problems.push("Appearance preference cookie missing");

      const preview = page.locator(".measurement-preview").first();
      if (await preview.count()) {
        await preview.scrollIntoViewIfNeeded();
        await preview.screenshot({ path: `${output}/network-measurement-preview-${viewportName}-${theme}.png` });
      }

      results.push({ viewportName, theme, state, bodyContrast, changed, problems });
      await context.close();
    }
  }
} finally {
  await browser.close();
}

for (const [viewportName] of viewports) {
  const light = results.find((entry) => entry.viewportName === viewportName && entry.theme === "light");
  const dark = results.find((entry) => entry.viewportName === viewportName && entry.theme === "dark");
  if (light && dark && light.state.canvas === dark.state.canvas) {
    light.problems.push("light and dark canvas tokens are identical");
    dark.problems.push("light and dark canvas tokens are identical");
  }
  if (light && dark && light.state.ink === dark.state.ink) {
    light.problems.push("light and dark ink tokens are identical");
    dark.problems.push("light and dark ink tokens are identical");
  }
}

await writeFile(`${output}/report.json`, JSON.stringify(results, null, 2));
const failures = results.filter((entry) => entry.problems.length > 0);
if (failures.length) {
  console.error("Network theme audit failures:", failures);
  process.exitCode = 1;
} else {
  console.log("Network light/dark theme audit passed.");
}

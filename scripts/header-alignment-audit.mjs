#!/usr/bin/env node
import { chromium } from "playwright";

const baseUrl = process.env.THEME_AUDIT_BASE_URL ?? "http://127.0.0.1:4173";
const viewports = [
  ["desktop", { width: 1440, height: 1000 }],
  ["mobile", { width: 390, height: 844 }],
  ["minimum", { width: 320, height: 700 }],
];
const tolerance = 2;
const browser = await chromium.launch({ headless: true });
const problems = [];

try {
  for (const [name, viewport] of viewports) {
    const context = await browser.newContext({ viewport, colorScheme: "light", reducedMotion: "reduce" });
    const page = await context.newPage();
    const response = await page.goto(baseUrl, { waitUntil: "networkidle" });
    if (!response?.ok()) {
      problems.push(`${name}: HTTP ${response?.status() ?? "no response"}`);
      await context.close();
      continue;
    }
    await page.locator("[data-settings-button]").waitFor({ state: "visible" });

    const geometry = await page.evaluate(() => {
      const header = document.querySelector(".jl-global-header__inner");
      const identity = document.querySelector(".jl-site-identity");
      const owner = document.querySelector(".jl-site-identity__owner");
      const separator = document.querySelector(".jl-site-identity__separator");
      const product = document.querySelector(".jl-site-identity__product");
      const actions = document.querySelector(".jl-global-header__actions");
      if (![header, identity, owner, separator, product, actions].every((element) => element instanceof HTMLElement)) return null;

      const rect = (element) => element.getBoundingClientRect();
      const centerY = (element) => {
        const box = rect(element);
        return box.top + box.height / 2;
      };
      return {
        headerCenter: centerY(header),
        identityCenter: centerY(identity),
        ownerCenter: centerY(owner),
        separatorCenter: centerY(separator),
        productCenter: centerY(product),
        actionsCenter: centerY(actions),
        identityAlignItems: getComputedStyle(identity).alignItems,
      };
    });

    if (!geometry) {
      problems.push(`${name}: header geometry could not be measured.`);
      await context.close();
      continue;
    }

    if (geometry.identityAlignItems !== "center") problems.push(`${name}: Network identity uses ${geometry.identityAlignItems} instead of centered alignment.`);
    for (const [label, center] of [
      ["Johnny Li", geometry.ownerCenter],
      ["separator", geometry.separatorCenter],
      ["Network Diagnostics", geometry.productCenter],
    ]) {
      if (Math.abs(center - geometry.actionsCenter) > tolerance) {
        problems.push(`${name}: ${label} is vertically offset from the header controls by ${Math.abs(center - geometry.actionsCenter).toFixed(2)}px.`);
      }
    }
    if (Math.abs(geometry.identityCenter - geometry.headerCenter) > tolerance) {
      problems.push(`${name}: identity container is not centered in the shared header row.`);
    }

    await context.close();
  }

  if (problems.length) throw new Error(problems.join(" "));
  console.log("Network header identity alignment audit passed.");
} finally {
  await browser.close();
}

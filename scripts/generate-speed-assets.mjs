import { createCipheriv, createHash } from "node:crypto";
import { mkdir, stat, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const SPEED_ASSET_BYTES = 24 * 1024 * 1024;
export const SPEED_ASSET_NAME = "payload.bin";
export const SPEED_ASSET_VERSION = "v2";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDirectory, "..");
const outputDirectory = resolve(projectRoot, "public", "speed", SPEED_ASSET_VERSION);
const key = createHash("sha256").update("network-diagnostics-static-speed-asset-v2").digest();

async function hasExpectedSize(path) {
  try {
    return (await stat(path)).size === SPEED_ASSET_BYTES;
  } catch {
    return false;
  }
}

function createPayload() {
  const iv = Buffer.alloc(16);
  iv.writeUInt32BE(2, 12);
  const cipher = createCipheriv("aes-256-ctr", key, iv);
  const zeros = Buffer.alloc(SPEED_ASSET_BYTES);
  return Buffer.concat([cipher.update(zeros), cipher.final()]);
}

export async function generateSpeedAssets() {
  await mkdir(outputDirectory, { recursive: true });
  const path = resolve(outputDirectory, SPEED_ASSET_NAME);
  if (await hasExpectedSize(path)) return;
  await writeFile(path, createPayload());
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await generateSpeedAssets();
}

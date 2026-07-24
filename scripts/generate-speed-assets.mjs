import { createCipheriv, createHash } from "node:crypto";
import { mkdir, stat, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const SPEED_SEGMENT_BYTES = 24 * 1024 * 1024;
export const SPEED_SEGMENT_COUNT = 4;
export const SPEED_ASSET_VERSION = "v4";
export const SPEED_ASSET_NAMES = Array.from(
  { length: SPEED_SEGMENT_COUNT },
  (_, index) => `segment-${index}.bin`
);

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDirectory, "..");
const outputDirectory = resolve(projectRoot, "public", "speed", SPEED_ASSET_VERSION);
const key = createHash("sha256").update("network-diagnostics-static-speed-assets-v4").digest();

async function hasExpectedSize(path) {
  try {
    return (await stat(path)).size === SPEED_SEGMENT_BYTES;
  } catch {
    return false;
  }
}

function createPayload(index) {
  const iv = Buffer.alloc(16);
  iv.writeUInt32BE(index + 1, 12);
  const cipher = createCipheriv("aes-256-ctr", key, iv);
  const zeros = Buffer.alloc(SPEED_SEGMENT_BYTES);
  return Buffer.concat([cipher.update(zeros), cipher.final()]);
}

export async function generateSpeedAssets() {
  await mkdir(outputDirectory, { recursive: true });
  for (const [index, name] of SPEED_ASSET_NAMES.entries()) {
    const path = resolve(outputDirectory, name);
    if (await hasExpectedSize(path)) continue;
    await writeFile(path, createPayload(index));
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await generateSpeedAssets();
}

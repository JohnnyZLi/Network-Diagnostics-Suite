import { createCipheriv, createHash } from "node:crypto";
import { once } from "node:events";
import { createWriteStream } from "node:fs";
import { mkdir, stat } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const R2_SPEED_OBJECT_BYTES = 256 * 1024 * 1024;
export const R2_SPEED_OBJECT_NAME = "network-diagnostics-speed-v1.bin";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDirectory, "..");
const outputDirectory = resolve(projectRoot, ".r2-speed");
const outputPath = resolve(outputDirectory, R2_SPEED_OBJECT_NAME);
const key = createHash("sha256").update("network-diagnostics-r2-speed-object-v1").digest();
const zeroChunk = Buffer.alloc(1024 * 1024);

async function hasExpectedSize(path) {
  try {
    return (await stat(path)).size === R2_SPEED_OBJECT_BYTES;
  } catch {
    return false;
  }
}

export async function generateR2SpeedObject() {
  await mkdir(outputDirectory, { recursive: true });
  if (await hasExpectedSize(outputPath)) return outputPath;

  const iv = Buffer.alloc(16);
  iv.writeUInt32BE(1, 12);
  const cipher = createCipheriv("aes-256-ctr", key, iv);
  const output = createWriteStream(outputPath, { flags: "w" });

  try {
    let remaining = R2_SPEED_OBJECT_BYTES;
    while (remaining > 0) {
      const length = Math.min(remaining, zeroChunk.byteLength);
      const encrypted = cipher.update(length === zeroChunk.byteLength ? zeroChunk : zeroChunk.subarray(0, length));
      if (!output.write(encrypted)) await once(output, "drain");
      remaining -= length;
    }
    const final = cipher.final();
    if (final.byteLength > 0 && !output.write(final)) await once(output, "drain");
    output.end();
    await once(output, "finish");
  } catch (error) {
    output.destroy();
    throw error;
  }

  if (!await hasExpectedSize(outputPath)) {
    throw new Error(`Generated R2 speed object is not ${R2_SPEED_OBJECT_BYTES} bytes.`);
  }
  return outputPath;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const path = await generateR2SpeedObject();
  console.log(path);
}

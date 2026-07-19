import { measurePing, sleep, TestCancelledError } from "./http";

export async function collectLatencySamples(
  count: number,
  intervalMs: number,
  signal: AbortSignal,
  onSample?: (sample: number | null) => void
): Promise<Array<number | null>> {
  const samples: Array<number | null> = [];
  for (let index = 0; index < count; index += 1) {
    const sample = await measurePing(signal);
    samples.push(sample);
    onSample?.(sample);
    if (index < count - 1) await sleep(intervalMs, signal);
  }
  return samples;
}

export async function collectLatencyUntilStopped(
  signal: AbortSignal,
  intervalMs: number,
  onSample?: (sample: number | null) => void
): Promise<Array<number | null>> {
  const samples: Array<number | null> = [];
  while (!signal.aborted) {
    try {
      const sample = await measurePing(signal);
      if (signal.aborted) break;
      samples.push(sample);
      onSample?.(sample);
      await sleep(intervalMs, signal);
    } catch (error) {
      if (error instanceof TestCancelledError && signal.aborted) break;
      throw error;
    }
  }
  return samples;
}

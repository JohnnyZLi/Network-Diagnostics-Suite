import { measurePing, sleep, TestCancelledError } from "./http";
import { PRIMARY_MEASUREMENT_ENDPOINT, type MeasurementEndpointDefinition } from "./endpoints";

const RUN_STARTUP_CANCELLATION_WINDOW_MS = 250;

export async function collectLatencySamples(
  count: number,
  intervalMs: number,
  signal: AbortSignal,
  onSample?: (sample: number | null) => void,
  endpoint: MeasurementEndpointDefinition = PRIMARY_MEASUREMENT_ENDPOINT
): Promise<Array<number | null>> {
  // Keep the newly rendered running state cancellable before the first network
  // request can fail or complete on a very fast path.
  await sleep(RUN_STARTUP_CANCELLATION_WINDOW_MS, signal);

  const samples: Array<number | null> = [];
  for (let index = 0; index < count; index += 1) {
    const sample = await measurePing(signal, 1_500, endpoint);
    samples.push(sample);
    onSample?.(sample);
    if (index < count - 1) await sleep(intervalMs, signal);
  }
  return samples;
}

export async function collectLatencyUntilStopped(
  signal: AbortSignal,
  intervalMs: number,
  onSample?: (sample: number | null) => void,
  endpoint: MeasurementEndpointDefinition = PRIMARY_MEASUREMENT_ENDPOINT
): Promise<Array<number | null>> {
  const samples: Array<number | null> = [];
  while (!signal.aborted) {
    try {
      const sample = await measurePing(signal, 1_500, endpoint);
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

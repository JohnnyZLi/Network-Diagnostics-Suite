import type { LatencySummary, LoadedLatencySummary, TimedSample, ThroughputSummary } from "../types/diagnostics";

export function mean(values: number[]): number | null {
  if (values.length === 0) return null;
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

export function percentile(values: number[], percentileValue: number): number | null {
  if (values.length === 0) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const bounded = Math.min(100, Math.max(0, percentileValue));
  const index = (bounded / 100) * (sorted.length - 1);
  const lower = Math.floor(index);
  const upper = Math.ceil(index);
  if (lower === upper) return sorted[lower] ?? null;
  const weight = index - lower;
  return (sorted[lower] ?? 0) * (1 - weight) + (sorted[upper] ?? 0) * weight;
}

export function consecutiveJitter(values: number[]): number | null {
  if (values.length < 2) return null;
  const differences: number[] = [];
  for (let index = 1; index < values.length; index += 1) {
    differences.push(Math.abs((values[index] ?? 0) - (values[index - 1] ?? 0)));
  }
  return mean(differences);
}

export function summarizeLatency(samples: Array<number | null>): LatencySummary {
  const valid = samples.filter((sample): sample is number => Number.isFinite(sample));
  const sent = samples.length;
  const received = valid.length;
  const lost = sent - received;

  return {
    sent,
    received,
    lost,
    lossPercent: sent === 0 ? 0 : (lost / sent) * 100,
    minMs: valid.length > 0 ? Math.min(...valid) : null,
    maxMs: valid.length > 0 ? Math.max(...valid) : null,
    meanMs: mean(valid),
    medianMs: percentile(valid, 50),
    p95Ms: percentile(valid, 95),
    jitterMs: consecutiveJitter(valid),
    samples
  };
}

export function bufferbloatGrade(increaseMs: number | null): LoadedLatencySummary["grade"] {
  if (increaseMs === null || !Number.isFinite(increaseMs)) return "—";
  if (increaseMs <= 5) return "A+";
  if (increaseMs <= 15) return "A";
  if (increaseMs <= 30) return "B";
  if (increaseMs <= 60) return "C";
  if (increaseMs <= 100) return "D";
  return "F";
}

export function summarizeLoadedLatency(
  samples: Array<number | null>,
  idleMedianMs: number | null
): LoadedLatencySummary {
  const summary = summarizeLatency(samples);
  const increaseMs = summary.medianMs === null || idleMedianMs === null
    ? null
    : Math.max(0, summary.medianMs - idleMedianMs);

  return {
    ...summary,
    increaseMs,
    grade: bufferbloatGrade(increaseMs)
  };
}

export function throughputFromTimeline(bytes: number, durationMs: number, timeline: TimedSample[]): ThroughputSummary {
  const seconds = Math.max(durationMs / 1000, 0.001);
  const mbps = (bytes * 8) / seconds / 1_000_000;
  const values = timeline.map((sample) => sample.value).filter(Number.isFinite);
  const average = mean(values) ?? 0;
  const variance = values.length === 0
    ? 0
    : values.reduce((sum, value) => sum + (value - average) ** 2, 0) / values.length;
  const coefficientOfVariation = average <= 0 ? 1 : Math.sqrt(variance) / average;

  return {
    mbps,
    bytes,
    durationMs,
    peakMbps: values.length > 0 ? Math.max(...values) : mbps,
    stabilityPercent: Math.max(0, Math.min(100, 100 - coefficientOfVariation * 100)),
    timeline
  };
}

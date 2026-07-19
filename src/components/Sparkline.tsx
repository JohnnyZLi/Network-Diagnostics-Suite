import type { TimedSample } from "../types/diagnostics";

interface SparklineProps {
  samples: TimedSample[];
  label: string;
  color?: string;
}

export function Sparkline({ samples, label, color = "var(--accent)" }: SparklineProps) {
  if (samples.length < 2) {
    return <div className="sparkline sparkline--empty" aria-label={`${label}: insufficient samples`} />;
  }

  const width = 360;
  const height = 92;
  const maximum = Math.max(...samples.map((sample) => sample.value), 1);
  const maximumTime = Math.max(...samples.map((sample) => sample.elapsedMs), 1);
  const points = samples.map((sample) => {
    const x = (sample.elapsedMs / maximumTime) * width;
    const y = height - (sample.value / maximum) * (height - 12) - 6;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  }).join(" ");

  return (
    <svg className="sparkline" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={label} preserveAspectRatio="none">
      <defs>
        <linearGradient id={`fill-${label.replaceAll(" ", "-")}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.28" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path className="sparkline__grid" d="M0 23H360M0 46H360M0 69H360" />
      <polygon points={`0,${height} ${points} ${width},${height}`} fill={`url(#fill-${label.replaceAll(" ", "-")})`} />
      <polyline points={points} fill="none" stroke={color} strokeWidth="2.5" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}

import type { ReactNode } from "react";

interface MetricCardProps {
  label: string;
  value: string;
  unit?: string;
  detail?: ReactNode;
  tone?: "blue" | "violet" | "green" | "neutral";
  children?: ReactNode;
}

export function MetricCard({ label, value, unit, detail, tone = "neutral", children }: MetricCardProps) {
  return (
    <article className={`metric-card metric-card--${tone}`}>
      <div className="metric-card__header">
        <span>{label}</span>
        <span className="metric-card__dot" aria-hidden="true" />
      </div>
      <div className="metric-card__reading">
        <strong>{value}</strong>
        {unit && <span>{unit}</span>}
      </div>
      {detail && <div className="metric-card__detail">{detail}</div>}
      {children}
    </article>
  );
}

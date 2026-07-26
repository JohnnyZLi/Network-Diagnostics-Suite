import { formatLatency } from "../core/format";
import type { ServiceCheckResult } from "../types/diagnostics";

export function ServiceMatrix({ services }: { services: ServiceCheckResult[] }) {
  if (services.length === 0) return null;
  return (
    <div className="service-grid jl-grid-3 jl-responsive-region">
      {services.map((service) => (
        <article className={service.reachable ? "service-item jl-callout jl-callout--success" : "service-item jl-callout jl-callout--danger"} key={service.id}>
          <span className={service.reachable ? "status-dot status-dot--up" : "status-dot status-dot--down"} aria-hidden="true" />
          <div>
            <strong>{service.name}</strong>
            <span>{service.reachable ? "Responded" : "No response"}</span>
          </div>
          <time>{service.durationMs === null ? "—" : `${formatLatency(service.durationMs)} ms`}</time>
        </article>
      ))}
    </div>
  );
}

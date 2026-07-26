import { formatLatency } from "../core/format";
import type { ServiceCheckResult } from "../types/diagnostics";

export function ServiceMatrix({ services }: { services: ServiceCheckResult[] }) {
  if (services.length === 0) return null;
  return (
    <div className="service-grid">
      {services.map((service) => (
        <article className="service-item" key={service.id}>
          <span className={service.reachable ? "status-dot status-dot--up" : "status-dot status-dot--down"} />
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

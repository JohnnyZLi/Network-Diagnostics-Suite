import { formatLatency, formatRate } from "../core/format";
import type { DiagnosticResult, FlowMeasurement } from "../types/diagnostics";

function findMeasurement(result: DiagnosticResult, strategy: FlowMeasurement["strategy"]): FlowMeasurement | undefined {
  return result.flowMeasurements?.find((measurement) => measurement.strategy === strategy);
}

function interpretation(singleMbps: number, aggregateMbps: number): string {
  if (singleMbps <= 0 || aggregateMbps <= 0) return "The transfer results were not large enough to compare reliably.";
  const ratio = singleMbps / aggregateMbps;
  if (ratio < 0.7) {
    return "Parallel transfers used materially more of the path. Individual downloads may be limited by single-flow transport behavior or the remote service even when total capacity is higher.";
  }
  if (ratio < 0.9) {
    return "Parallel transfers improved throughput, but one connection still captured most of the available path.";
  }
  return "One connection captured nearly all of the measured aggregate capacity on this path.";
}

function MeasurementCard({ title, measurement }: { title: string; measurement: FlowMeasurement }) {
  return (
    <article className="flow-comparison-card">
      <div className="flow-comparison-card__heading">
        <span>{title}</span>
        <small>{measurement.concurrency} {measurement.concurrency === 1 ? "connection" : "connections"}</small>
      </div>
      <dl>
        <div>
          <dt>Download</dt>
          <dd>{measurement.download ? `${formatRate(measurement.download.steadyMbps)} Mbps` : "Not sampled"}</dd>
        </div>
        <div>
          <dt>Upload</dt>
          <dd>{measurement.upload ? `${formatRate(measurement.upload.steadyMbps)} Mbps` : "Not sampled"}</dd>
        </div>
        <div>
          <dt>Loaded delay</dt>
          <dd>
            {measurement.downloadLatency
              ? `+${formatLatency(measurement.downloadLatency.increaseMs)} ms down`
              : "Not sampled"}
          </dd>
        </div>
      </dl>
    </article>
  );
}

export function FlowComparisonPanel({ result }: { result: DiagnosticResult }) {
  const single = findMeasurement(result, "single");
  const aggregate = findMeasurement(result, "aggregate");
  const scaling = result.downloadScaling ?? [];
  const canCompare = Boolean(single?.download && aggregate?.download);
  if (!canCompare && scaling.length < 2) return null;

  const singleMbps = single?.download?.steadyMbps ?? 0;
  const aggregateMbps = aggregate?.download?.steadyMbps ?? 0;
  const parallelGain = singleMbps > 0 ? ((aggregateMbps / singleMbps) - 1) * 100 : null;
  const singleShare = aggregateMbps > 0 ? (singleMbps / aggregateMbps) * 100 : null;
  const maxScalingMbps = Math.max(1, ...scaling.map((point) => point.download.steadyMbps));

  return (
    <section className="flow-comparison" aria-labelledby="flow-comparison-title">
      <div className="section-heading">
        <span className="eyebrow">Connection scaling</span>
        <h2 id="flow-comparison-title">Single flow versus <em className="text-blue">aggregate capacity.</em></h2>
        <p>The phases use the same selected endpoint and profile. They are measured separately so the single connection is not competing with the parallel group.</p>
      </div>

      {single && aggregate && (
        <div className="flow-comparison-grid">
          <MeasurementCard title="Single connection" measurement={single} />
          <MeasurementCard title="Aggregate" measurement={aggregate} />
          <article className="flow-comparison-card flow-comparison-card--interpretation">
            <div className="flow-comparison-card__heading">
              <span>Difference</span>
              <small>{parallelGain === null ? "Unavailable" : `${parallelGain >= 0 ? "+" : ""}${parallelGain.toFixed(0)}%`}</small>
            </div>
            <strong>{singleShare === null ? "—" : `${singleShare.toFixed(0)}%`}</strong>
            <p>of aggregate download capacity reached by one connection.</p>
            {canCompare && <p>{interpretation(singleMbps, aggregateMbps)}</p>}
          </article>
        </div>
      )}

      {scaling.length > 2 && (
        <div className="flow-scaling-panel">
          <div className="flow-scaling-panel__heading">
            <div>
              <span className="eyebrow">Stress profile</span>
              <h3>Download scaling by connection count</h3>
            </div>
            <small>Independent timed stages</small>
          </div>
          <ol className="flow-scaling-list">
            {scaling.map((point) => (
              <li key={point.concurrency}>
                <span>{point.concurrency}×</span>
                <div className="flow-scaling-track" aria-hidden="true">
                  <i style={{ width: `${Math.max(2, (point.download.steadyMbps / maxScalingMbps) * 100)}%` }} />
                </div>
                <strong>{formatRate(point.download.steadyMbps)} Mbps</strong>
                <small>+{formatLatency(point.downloadLatency.increaseMs)} ms</small>
              </li>
            ))}
          </ol>
        </div>
      )}
    </section>
  );
}

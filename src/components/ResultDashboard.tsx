import { formatBytes, formatLatency, formatRate } from "../core/format";
import type {
  DiagnosticResult,
  DownloadDeliverySummary,
  LoadedLatencySummary,
  ThroughputSummary,
  UploadDeliverySummary
} from "../types/diagnostics";
import { LatencyTable } from "./LatencyTable";
import { MetricCard } from "./MetricCard";
import { DiagnosticFindings } from "./DiagnosticFindings";
import { ServiceMatrix } from "./ServiceMatrix";
import { Sparkline } from "./Sparkline";

interface GenerationSummary {
  generation: number;
  requests: number;
  bytes: number;
}

function worstGrade(...summaries: LoadedLatencySummary[]): LoadedLatencySummary["grade"] {
  const rank: Record<LoadedLatencySummary["grade"], number> = { "—": -1, "A+": 0, A: 1, B: 2, C: 3, D: 4, F: 5 };
  return summaries.reduce((worst, current) => rank[current.grade] > rank[worst] ? current.grade : worst, "—" as LoadedLatencySummary["grade"]);
}

function qualificationLabel(summary: ThroughputSummary): string {
  switch (summary.qualification) {
    case "cap-limited": return "Profile cap reached early";
    case "still-ramping": return "Still ramping at finish";
    case "declining": return "Declined during sample";
    case "unstable": return "Variable sample";
    default: return "Qualified duration sample";
  }
}

function qualificationTone(summary: ThroughputSummary): string {
  return summary.qualification === "qualified" ? "scope-status scope-status--good" : "scope-status scope-status--warn";
}

function downloadMetricDetail(summary: ThroughputSummary): string {
  const sampleCount = summary.samples?.length ?? 0;
  if (summary.aggregation === "median" && sampleCount > 1) {
    return `${formatRate(summary.mbps)} median whole-sample · ${sampleCount} samples · ${summary.stabilityPercent.toFixed(0)}% median stability · ${formatBytes(summary.bytes)}`;
  }
  return `${formatRate(summary.mbps)} whole-phase · ${summary.stabilityPercent.toFixed(0)}% stability · ${formatBytes(summary.bytes)}`;
}

function DownloadSampleCards({ summary }: { summary: ThroughputSummary }) {
  if (!summary.samples || summary.samples.length === 0) {
    return <p className="transfer-detail__empty">Single sustained sample</p>;
  }

  return (
    <ul className="transfer-card-grid transfer-card-grid--samples" aria-label="Download sample results">
      {summary.samples.map((sample) => (
        <li className="transfer-card" key={sample.sample}>
          <span>Sample {sample.sample}</span>
          <strong>{formatRate(sample.steadyMbps)} <small>Mbps</small></strong>
          <p>{sample.stabilityPercent.toFixed(0)}% stability</p>
        </li>
      ))}
    </ul>
  );
}

function LifecycleCards({
  label,
  started,
  completed,
  interrupted
}: {
  label: string;
  started: number;
  completed: number;
  interrupted: number;
}) {
  return (
    <ul className="transfer-card-grid transfer-card-grid--lifecycle" aria-label={`${label} request lifecycle`}>
      <li className="transfer-card">
        <span>Started</span>
        <strong>{started}</strong>
      </li>
      <li className="transfer-card">
        <span>Completed</span>
        <strong>{completed}</strong>
      </li>
      <li className="transfer-card">
        <span>Stopped at phase end</span>
        <strong>{interrupted}</strong>
      </li>
    </ul>
  );
}

function GenerationCards({ generations }: { generations: GenerationSummary[] }) {
  if (generations.length === 0) {
    return <p className="transfer-detail__empty">Unavailable</p>;
  }

  return (
    <ul className="transfer-card-grid transfer-card-grid--generations" aria-label="Request generations">
      {generations.map((generation) => (
        <li className="transfer-card" key={generation.generation}>
          <span>Generation {generation.generation}</span>
          <strong>{generation.requests} {generation.requests === 1 ? "request" : "requests"}</strong>
          <p>{formatBytes(generation.bytes)}</p>
        </li>
      ))}
    </ul>
  );
}

function cacheBreakdown(delivery: DownloadDeliverySummary | undefined): string {
  if (!delivery) return "Unavailable";
  const statuses = Object.entries(delivery.cacheStatusCounts)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([status, count]) => `${status} ${count}`);
  const percentage = delivery.edgeCacheServedPercent === null
    ? "cache status unavailable"
    : `${delivery.edgeCacheServedPercent.toFixed(0)}% edge-served`;
  const detail = statuses.length > 0 ? ` · ${statuses.join(" · ")}` : "";
  return `${percentage}${detail}`;
}

function warmupDescription(delivery: DownloadDeliverySummary | undefined): string {
  if (!delivery) return "Unavailable";
  const status = delivery.warmupCacheStatus ? ` · ${delivery.warmupCacheStatus}` : "";
  if (delivery.warmupSource === "r2") {
    return `${formatBytes(delivery.r2ObjectBytes)} availability probe${status}`;
  }
  if (delivery.warmupSource === "static") {
    return `${formatBytes(delivery.warmupCachedBytes)} primed inside edge${status}`;
  }
  return `${formatBytes(delivery.warmupBytes)} browser fallback${status}`;
}

function pathDescription(delivery: DownloadDeliverySummary | undefined): string {
  if (!delivery) return "Unavailable";
  return delivery.selectedPath === "r2-direct-v1" ? "R2 custom domain · direct" : "Worker-composed stream";
}

function rejectionDescription(delivery: DownloadDeliverySummary | undefined): string {
  if (!delivery) return "Unavailable";
  if (delivery.rejectedStaticRequests === 0) return "0";
  const counts = new Map<string, number>();
  for (const rejection of delivery.streamRejections) {
    counts.set(rejection.reason, (counts.get(rejection.reason) ?? 0) + 1);
  }
  const details = [...counts.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([reason, count]) => `${reason} ${count}`)
    .join(" · ");
  return `${delivery.rejectedStaticRequests} · ${details}`;
}

interface ResultDashboardProps {
  result: DiagnosticResult;
  onExport: () => void;
  onCopy: () => void;
  copyLabel: string;
}

export function ResultDashboard({ result, onExport, onCopy, copyLabel }: ResultDashboardProps) {
  const grade = worstGrade(result.downloadLatency, result.uploadLatency);
  const delivery = result.download.delivery;
  const uploadDelivery = result.upload.uploadDelivery;

  return (
    <section className="results" aria-labelledby="results-title">
      <div className="section-heading section-heading--actions">
        <div>
          <span className="eyebrow">Completed {new Date(result.completedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
          <h2 id="results-title">Connection <em className="text-accent">report</em></h2>
        </div>
        <div className="result-actions">
          <button type="button" onClick={onCopy}>{copyLabel}</button>
          <button type="button" onClick={onExport}>Export JSON</button>
        </div>
      </div>

      <div className="metric-grid">
        <MetricCard label="Download" value={formatRate(result.download.steadyMbps)} unit="Mbps" detail={downloadMetricDetail(result.download)} tone="blue">
          <Sparkline samples={result.download.timeline} label="Download throughput" color="var(--blue)" />
        </MetricCard>
        <MetricCard label="Upload" value={formatRate(result.upload.steadyMbps)} unit="Mbps" detail={`${formatRate(result.upload.mbps)} whole-phase · ${result.upload.stabilityPercent.toFixed(0)}% stability · ${formatBytes(result.upload.bytes)}`} tone="violet">
          <Sparkline samples={result.upload.timeline} label="Upload throughput" color="var(--violet)" />
        </MetricCard>
        <MetricCard label="Idle latency" value={formatLatency(result.idleLatency.medianMs)} unit="ms" detail={`${formatLatency(result.idleLatency.minMs)} min · ${formatLatency(result.idleLatency.maxMs)} max`} tone="green" />
        <MetricCard label="Loaded-latency grade" value={grade} detail={`+${formatLatency(Math.max(result.downloadLatency.increaseMs ?? 0, result.uploadLatency.increaseMs ?? 0))} ms worst case`} tone="neutral" />
      </div>

      <DiagnosticFindings result={result} />

      <section className="report-panel latency-panel">
        <div className="report-panel__heading">
          <div><span className="eyebrow">Distribution</span><h3><span className="text-violet">Latency</span> under each condition</h3></div>
          <p>Request loss is a browser-level timeout rate, not raw Internet Protocol packet loss.</p>
        </div>
        <LatencyTable idle={result.idleLatency} download={result.downloadLatency} upload={result.uploadLatency} />
      </section>

      <details className="technical-details">
        <summary>
          <span>
            <span className="eyebrow">Advanced report</span>
            <strong><span className="text-blue">Technical measurement details</span></strong>
            <small>Scope, edge path, cache behavior, request lifecycle, and service reachability.</small>
          </span>
          <span className="technical-details__action" aria-hidden="true">Expand</span>
        </summary>

        <div className="technical-details__content">
          <section className="report-panel scope-panel">
            <div className="report-panel__heading">
              <div><span className="eyebrow">Measurement scope</span><h3><span className="text-amber">Internet path</span>, not an isolated access-line benchmark</h3></div>
              <p>The browser result includes this device, the local link, router, Internet service provider, route, and the selected measurement endpoint.</p>
            </div>
            <div className="scope-grid">
              <article><span>Path</span><strong>{pathDescription(delivery)}</strong><p>Remote endpoint and route remain part of the result.</p></article>
              <article><span>Download sample</span><strong className={qualificationTone(result.download)}>{qualificationLabel(result.download)}</strong><p>{result.download.aggregation === "median" ? `Median of ${result.download.samples?.length ?? 0} samples.` : result.download.capReached ? "The configured byte ceiling was reached." : "The configured duration ended the transfer."}</p></article>
              <article><span>Upload sample</span><strong className={qualificationTone(result.upload)}>{qualificationLabel(result.upload)}</strong><p>{result.upload.capReached ? "The configured byte ceiling was reached." : "The configured duration ended the transfer."}</p></article>
            </div>
            <p className="scope-panel__note">To remove the public server and ISP from the equation, run the native probe’s LAN server on a second wired machine and test it with <code>--lan-target</code>.</p>
          </section>

          <section className="report-panel edge-panel">
            <span className="eyebrow">Test path</span>
            <h3 className="text-blue">Edge session</h3>
            <dl>
              <div><dt>Scope</dt><dd>Internet path · endpoint included</dd></div>
              <div><dt>Measurement engine</dt><dd>{result.measurement ? `${result.measurement.engine} · ${result.measurement.engineVersion}` : "Legacy browser report"}</dd></div>
              <div><dt>Selected endpoint</dt><dd>{result.measurement?.selectedEndpoint.name ?? "Network Diagnostics primary"}</dd></div>
              <div><dt>Endpoint provider</dt><dd>{result.measurement?.selectedEndpoint.provider ?? "Cloudflare"}</dd></div>
              <div><dt>Network</dt><dd>{result.edge?.network ?? "Unavailable"}{result.edge?.asn ? ` · AS${result.edge.asn}` : ""}</dd></div>
              <div><dt>Worker edge</dt><dd>{result.edge?.edge ?? "Unavailable"}</dd></div>
              <div><dt>IP path</dt><dd>{result.edge?.ipVersion ?? "Unknown"}</dd></div>
              <div><dt>Metadata protocol</dt><dd>{result.edge?.protocol ?? "Unknown"}</dd></div>
              <div><dt>Requested download path</dt><dd>{delivery?.requestedPath ?? "Unavailable"}</dd></div>
              <div><dt>Selected download path</dt><dd>{pathDescription(delivery)}</dd></div>
              <div><dt>Download protocol</dt><dd>{delivery?.protocols.length ? delivery.protocols.join(" · ") : "Unavailable"}</dd></div>
              <div><dt>Edge cache</dt><dd>{cacheBreakdown(delivery)}</dd></div>
              <div><dt>Path probe</dt><dd>{warmupDescription(delivery)}</dd></div>
            </dl>
          </section>

          <section className="report-panel transfer-panel">
            <div className="report-panel__heading">
              <div><span className="eyebrow">Transfer details</span><h3>How the <span className="text-violet">sustained requests</span> ran</h3></div>
              <p>Requests shown as stopped at phase end were still transferring when the timed measurement ended. That is expected during a sustained test.</p>
            </div>

            <div className="transfer-summary-grid">
              <article>
                <span>Total data</span>
                <strong>{formatBytes(result.dataUsedBytes)}</strong>
              </article>
              <article>
                <span>Download response size</span>
                <strong>{delivery ? formatBytes(delivery.logicalStreamBytes) : "Unavailable"}</strong>
              </article>
              <article>
                <span>Upload request size</span>
                <strong>{uploadDelivery ? formatBytes(uploadDelivery.requestSizeBytes) : "Unavailable"}</strong>
              </article>
            </div>

            <div className="transfer-direction-grid">
              <section className="transfer-direction" aria-labelledby="download-transfer-title">
                <header>
                  <div>
                    <span className="transfer-direction__eyebrow">Download</span>
                    <h4 id="download-transfer-title">Sample and request activity</h4>
                  </div>
                  <small>{result.download.aggregation === "median" ? `Median of ${result.download.samples?.length ?? 0}` : "Single sample"}</small>
                </header>

                <div className="transfer-detail">
                  <h5>Sample results</h5>
                  <DownloadSampleCards summary={result.download} />
                </div>

                <div className="transfer-detail">
                  <h5>Request lifecycle</h5>
                  {delivery ? (
                    <LifecycleCards
                      label="Download"
                      started={delivery.startedRequests}
                      completed={delivery.completedRequests}
                      interrupted={delivery.interruptedRequests}
                    />
                  ) : <p className="transfer-detail__empty">Unavailable</p>}
                </div>

                <div className="transfer-detail">
                  <h5>Request generations</h5>
                  <GenerationCards generations={delivery?.requestGenerations ?? []} />
                </div>

                <dl className="transfer-fact-list">
                  <div><dt>R2 requests</dt><dd>{delivery?.r2Requests ?? "Unavailable"}</dd></div>
                  <div><dt>Worker stream requests</dt><dd>{delivery?.staticRequests ?? "Unavailable"}</dd></div>
                  <div><dt>Rejected responses</dt><dd>{rejectionDescription(delivery)}</dd></div>
                  <div><dt>Replacement requests</dt><dd>{delivery?.replacementRequests ?? "Unavailable"}</dd></div>
                  <div><dt>Dynamic fallbacks</dt><dd>{delivery?.workerFallbackRequests ?? "Unavailable"}</dd></div>
                </dl>
              </section>

              <section className="transfer-direction" aria-labelledby="upload-transfer-title">
                <header>
                  <div>
                    <span className="transfer-direction__eyebrow">Upload</span>
                    <h4 id="upload-transfer-title">Request activity</h4>
                  </div>
                  <small>{formatRate(result.upload.steadyMbps)} Mbps steady</small>
                </header>

                <div className="transfer-detail">
                  <h5>Request lifecycle</h5>
                  {uploadDelivery ? (
                    <LifecycleCards
                      label="Upload"
                      started={uploadDelivery.startedRequests}
                      completed={uploadDelivery.completedRequests}
                      interrupted={uploadDelivery.interruptedRequests}
                    />
                  ) : <p className="transfer-detail__empty">Unavailable</p>}
                </div>

                <div className="transfer-detail">
                  <h5>Request generations</h5>
                  <GenerationCards generations={uploadDelivery?.requestGenerations ?? []} />
                </div>

                <dl className="transfer-fact-list">
                  <div><dt>Request size</dt><dd>{uploadDelivery ? formatBytes(uploadDelivery.requestSizeBytes) : "Unavailable"}</dd></div>
                  <div><dt>Initial stagger</dt><dd>{uploadDelivery ? `${uploadDelivery.initialStaggerMs} ms` : "Unavailable"}</dd></div>
                  <div><dt>Replacement requests</dt><dd>{uploadDelivery?.replacementRequests ?? "Unavailable"}</dd></div>
                  <div><dt>Transferred</dt><dd>{formatBytes(result.upload.bytes)}</dd></div>
                </dl>
              </section>
            </div>
          </section>

          {result.services.length > 0 && (
            <section className="report-panel">
              <div className="report-panel__heading">
                <div><span className="eyebrow">Full battery</span><h3><span className="text-green">Common-service</span> reachability</h3></div>
                <p>Each service receives one ordinary, cache-bypassed request and may process it under its own privacy policy.</p>
              </div>
              <ServiceMatrix services={result.services} />
            </section>
          )}
        </div>
      </details>
    </section>
  );
}

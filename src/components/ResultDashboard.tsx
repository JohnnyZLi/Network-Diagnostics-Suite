import { formatBytes, formatLatency, formatRate } from "../core/format";
import type { DiagnosticResult, LoadedLatencySummary, ThroughputSummary } from "../types/diagnostics";
import { LatencyTable } from "./LatencyTable";
import { MetricCard } from "./MetricCard";
import { ServiceMatrix } from "./ServiceMatrix";
import { Sparkline } from "./Sparkline";

function worstGrade(...summaries: LoadedLatencySummary[]): LoadedLatencySummary["grade"] {
  const rank: Record<LoadedLatencySummary["grade"], number> = { "—": -1, "A+": 0, A: 1, B: 2, C: 3, D: 4, F: 5 };
  return summaries.reduce((worst, current) => rank[current.grade] > rank[worst] ? current.grade : worst, "—" as LoadedLatencySummary["grade"]);
}


function qualificationLabel(summary: ThroughputSummary): string {
  switch (summary.qualification) {
    case "cap-limited": return "Profile cap reached early";
    case "still-ramping": return "Still ramping at finish";
    case "unstable": return "Variable sample";
    default: return "Qualified duration sample";
  }
}

function qualificationTone(summary: ThroughputSummary): string {
  return summary.qualification === "qualified" ? "scope-status scope-status--good" : "scope-status scope-status--warn";
}

function buildFindings(result: DiagnosticResult): string[] {
  const findings: string[] = [];
  const worstLoadedIncrease = Math.max(result.downloadLatency.increaseMs ?? 0, result.uploadLatency.increaseMs ?? 0);
  if (result.idleLatency.lossPercent > 0) findings.push("One or more application requests timed out while the connection was idle.");
  if ((result.idleLatency.jitterMs ?? 0) > 20) findings.push("Idle latency varied enough to affect calls, games, or remote sessions.");
  if (worstLoadedIncrease > 30) findings.push("Latency rises materially under load, which suggests queueing or bufferbloat.");
  if (result.download.qualification === "cap-limited") findings.push("The download reached this profile’s data cap early; use Full or Stress for a longer high-speed sample.");
  if (result.upload.qualification === "cap-limited") findings.push("The upload reached this profile’s data cap early; use Full or Stress for a longer high-speed sample.");
  if (result.download.qualification === "still-ramping" || result.upload.qualification === "still-ramping") findings.push("At least one transfer was still accelerating when the measurement ended.");
  if (result.services.some((service) => !service.reachable)) findings.push("At least one common service did not answer the browser reachability check.");
  if (findings.length === 0) findings.push("No obvious instability appeared in this browser test.");
  return findings;
}

interface ResultDashboardProps {
  result: DiagnosticResult;
  onExport: () => void;
  onCopy: () => void;
  copyLabel: string;
}

export function ResultDashboard({ result, onExport, onCopy, copyLabel }: ResultDashboardProps) {
  const grade = worstGrade(result.downloadLatency, result.uploadLatency);
  const findings = buildFindings(result);
  return (
    <section className="results" aria-labelledby="results-title">
      <div className="section-heading section-heading--actions">
        <div>
          <span className="eyebrow">Completed {new Date(result.completedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
          <h2 id="results-title">Connection report</h2>
        </div>
        <div className="result-actions">
          <button type="button" onClick={onCopy}>{copyLabel}</button>
          <button type="button" onClick={onExport}>Export JSON</button>
        </div>
      </div>

      <div className="metric-grid">
        <MetricCard
          label="Download"
          value={formatRate(result.download.steadyMbps)}
          unit="Mbps"
          detail={`${formatRate(result.download.mbps)} whole-phase · ${result.download.stabilityPercent.toFixed(0)}% stability · ${formatBytes(result.download.bytes)}`}
          tone="blue"
        >
          <Sparkline samples={result.download.timeline} label="Download throughput" color="var(--blue)" />
        </MetricCard>
        <MetricCard
          label="Upload"
          value={formatRate(result.upload.steadyMbps)}
          unit="Mbps"
          detail={`${formatRate(result.upload.mbps)} whole-phase · ${result.upload.stabilityPercent.toFixed(0)}% stability · ${formatBytes(result.upload.bytes)}`}
          tone="violet"
        >
          <Sparkline samples={result.upload.timeline} label="Upload throughput" color="var(--violet)" />
        </MetricCard>
        <MetricCard
          label="Idle latency"
          value={formatLatency(result.idleLatency.medianMs)}
          unit="ms"
          detail={`${formatLatency(result.idleLatency.minMs)} min · ${formatLatency(result.idleLatency.maxMs)} max`}
          tone="green"
        />
        <MetricCard
          label="Loaded-latency grade"
          value={grade}
          detail={`+${formatLatency(Math.max(result.downloadLatency.increaseMs ?? 0, result.uploadLatency.increaseMs ?? 0))} ms worst case`}
          tone="neutral"
        />
      </div>

      <section className="report-panel scope-panel">
        <div className="report-panel__heading">
          <div><span className="eyebrow">Measurement scope</span><h3>Internet path, not an isolated access-line benchmark</h3></div>
          <p>The browser result includes this device, the local link, router, Internet service provider, route, and the Cloudflare test edge.</p>
        </div>
        <div className="scope-grid">
          <article><span>Path</span><strong>Internet end to end</strong><p>Remote endpoint and route remain part of the result.</p></article>
          <article><span>Download sample</span><strong className={qualificationTone(result.download)}>{qualificationLabel(result.download)}</strong><p>{result.download.capReached ? "The configured byte ceiling was reached." : "The configured duration ended the transfer."}</p></article>
          <article><span>Upload sample</span><strong className={qualificationTone(result.upload)}>{qualificationLabel(result.upload)}</strong><p>{result.upload.capReached ? "The configured byte ceiling was reached." : "The configured duration ended the transfer."}</p></article>
        </div>
        <p className="scope-panel__note">To remove the public server and ISP from the equation, run the native probe’s LAN server on a second wired machine and test it with <code>--lan-target</code>.</p>
      </section>

      <section className="report-panel">
        <div className="report-panel__heading">
          <div><span className="eyebrow">Distribution</span><h3>Latency under each condition</h3></div>
          <p>Request loss is a browser-level timeout rate, not raw Internet Protocol packet loss.</p>
        </div>
        <LatencyTable idle={result.idleLatency} download={result.downloadLatency} upload={result.uploadLatency} />
      </section>

      <div className="report-columns">
        <section className="report-panel findings-panel">
          <span className="eyebrow">Interpretation</span>
          <h3>What stood out</h3>
          <ul>{findings.map((finding) => <li key={finding}>{finding}</li>)}</ul>
        </section>
        <section className="report-panel edge-panel">
          <span className="eyebrow">Test path</span>
          <h3>Edge session</h3>
          <dl>
            <div><dt>Scope</dt><dd>Internet path · endpoint included</dd></div>
            <div><dt>Network</dt><dd>{result.edge?.network ?? "Unavailable"}{result.edge?.asn ? ` · AS${result.edge.asn}` : ""}</dd></div>
            <div><dt>Edge</dt><dd>{result.edge?.edge ?? "Unavailable"}</dd></div>
            <div><dt>IP path</dt><dd>{result.edge?.ipVersion ?? "Unknown"}</dd></div>
            <div><dt>Protocol</dt><dd>{result.edge?.protocol ?? "Unknown"}</dd></div>
            <div><dt>Data transferred</dt><dd>{formatBytes(result.dataUsedBytes)}</dd></div>
          </dl>
        </section>
      </div>

      {result.services.length > 0 && (
        <section className="report-panel">
          <div className="report-panel__heading">
            <div><span className="eyebrow">Full battery</span><h3>Common-service reachability</h3></div>
            <p>Each service receives one ordinary, cache-bypassed request and may process it under its own privacy policy.</p>
          </div>
          <ServiceMatrix services={result.services} />
        </section>
      )}
    </section>
  );
}

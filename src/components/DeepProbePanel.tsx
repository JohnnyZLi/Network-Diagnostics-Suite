import { useRef, useState } from "react";
import { formatLatency } from "../core/format";
import type { DeepProbeReport } from "../types/deep-probe";

function isDeepProbeReport(value: unknown): value is DeepProbeReport {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Partial<DeepProbeReport>;
  return candidate.schemaVersion === "1.0"
    && typeof candidate.target === "string"
    && Array.isArray(candidate.interfaces)
    && Array.isArray(candidate.dnsResolvers)
    && Array.isArray(candidate.serviceEndpoints)
    && Array.isArray(candidate.traceRoute?.hops)
    && typeof candidate.internetPing?.statistics === "object";
}

function fastestResolver(report: DeepProbeReport) {
  return report.dnsResolvers
    .filter((resolver) => resolver.medianMs !== undefined)
    .sort((left, right) => (left.medianMs ?? Number.POSITIVE_INFINITY) - (right.medianMs ?? Number.POSITIVE_INFINITY))[0];
}

function sampleText(samples: Array<number | null>): string {
  return samples.map((sample) => sample === null ? "*" : formatLatency(sample)).join(" / ");
}

export function DeepProbePanel() {
  const [report, setReport] = useState<DeepProbeReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const importFile = async (file: File | undefined) => {
    if (!file) return;
    setError(null);
    if (file.size > 5 * 1024 * 1024) {
      setError("Probe reports must be smaller than 5 MB.");
      return;
    }
    try {
      const parsed: unknown = JSON.parse(await file.text());
      if (!isDeepProbeReport(parsed)) throw new Error("This is not a Network Deep Probe 1.0 report.");
      setReport(parsed);
    } catch (caught) {
      setReport(null);
      setError(caught instanceof Error ? caught.message : "The report could not be read.");
    } finally {
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  if (!report) {
    return (
      <section className="deep-probe" id="deep-probe">
        <div className="deep-probe__intro">
          <span className="eyebrow">Optional local diagnostics</span>
          <h2>Bring the operating-system layer into the report.</h2>
          <p>Run the Windows 11 deep probe, then open its JSON report here. Parsing happens in this tab; the selected file is never transmitted.</p>
        </div>
        <div className="deep-probe__actions">
          <input
            ref={inputRef}
            type="file"
            accept="application/json,.json"
            onChange={(event) => void importFile(event.target.files?.[0])}
            id="probe-report"
          />
          <label htmlFor="probe-report">Import probe report <span aria-hidden="true">↗</span></label>
          <small>Schema 1.0 · maximum 5 MB · processed locally</small>
          {error && <p role="alert">{error}</p>}
        </div>
      </section>
    );
  }

  const fastestDns = fastestResolver(report);
  return (
    <section className="deep-report" id="deep-probe">
      <div className="section-heading section-heading--actions">
        <div>
          <span className="eyebrow">Local report · {new Date(report.generatedAt).toLocaleString()}</span>
          <h2>Deep network path</h2>
        </div>
        <button type="button" onClick={() => setReport(null)}>Close report</button>
      </div>

      <div className="deep-summary">
        <article><span>ICMP packet loss</span><strong>{report.internetPing.statistics.lossPercent.toFixed(1)}<small>%</small></strong><p>{report.internetPing.statistics.received} of {report.internetPing.statistics.sent} replies</p></article>
        <article><span>Internet latency</span><strong>{formatLatency(report.internetPing.statistics.medianMs)}<small>ms</small></strong><p>{formatLatency(report.internetPing.statistics.jitterMs)} ms jitter</p></article>
        <article><span>Route</span><strong>{report.traceRoute.hops.length}<small>hops</small></strong><p>{report.traceRoute.reachedDestination ? "Destination reached" : "Partial path"}</p></article>
        <article><span>Path MTU</span><strong>{report.pathMtu.estimatedIpv4Mtu ?? "—"}<small>bytes</small></strong><p>{report.pathMtu.status}</p></article>
        <article><span>Fastest DNS</span><strong>{formatLatency(fastestDns?.medianMs)}<small>ms</small></strong><p>{fastestDns?.name ?? "No resolver answered"}</p></article>
      </div>

      <section className="report-panel">
        <div className="report-panel__heading">
          <div><span className="eyebrow">Internet Control Message Protocol</span><h3>Traceroute to {report.traceRoute.target}</h3></div>
          <p>Three probes per time-to-live value. An asterisk is a timed-out reply, not necessarily a broken hop.</p>
        </div>
        <div className="deep-table-wrap">
          <table className="deep-table trace-table">
            <thead><tr><th>Hop</th><th>Address</th><th>Reverse DNS</th><th>Round trips (ms)</th><th>Status</th></tr></thead>
            <tbody>
              {report.traceRoute.hops.map((hop) => (
                <tr key={hop.hop}>
                  <td>{hop.hop.toString().padStart(2, "0")}</td>
                  <td>{hop.addressRedacted ? "Private hop" : hop.address ?? "*"}</td>
                  <td>{hop.hostname ?? "—"}</td>
                  <td>{sampleText(hop.roundTripsMs)}</td>
                  <td>{hop.reachedDestination ? "Destination" : hop.addressRedacted ? "Address hidden" : hop.address ? "Transit" : "No reply"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <div className="report-columns">
        <section className="report-panel">
          <span className="eyebrow">UDP port 53</span><h3>DNS resolver timing</h3>
          <div className="deep-table-wrap">
            <table className="deep-table">
              <thead><tr><th>Resolver</th><th>Success</th><th>Median</th><th>95th pct.</th></tr></thead>
              <tbody>{report.dnsResolvers.map((resolver) => (
                <tr key={`${resolver.name}-${resolver.address}`}><td>{resolver.name}<small>{resolver.address}</small></td><td>{resolver.successful}/{resolver.attempts}</td><td>{formatLatency(resolver.medianMs)} ms</td><td>{formatLatency(resolver.p95Ms)} ms</td></tr>
              ))}</tbody>
            </table>
          </div>
        </section>
        <section className="report-panel">
          <span className="eyebrow">Transport Layer Security</span><h3>Service connection phases</h3>
          <div className="deep-table-wrap">
            <table className="deep-table">
              <thead><tr><th>Service</th><th>DNS</th><th>TCP</th><th>TLS</th></tr></thead>
              <tbody>{report.serviceEndpoints.map((endpoint) => (
                <tr key={endpoint.host}><td>{endpoint.name}<small>{endpoint.applicationProtocol ?? endpoint.error ?? endpoint.host}</small></td><td>{formatLatency(endpoint.dnsMs)}</td><td>{formatLatency(endpoint.tcpMs)}</td><td>{formatLatency(endpoint.tlsMs)}</td></tr>
              ))}</tbody>
            </table>
          </div>
        </section>
      </div>

      <section className="report-panel interface-panel">
        <div className="report-panel__heading"><div><span className="eyebrow">Local link</span><h3>Active interfaces</h3></div><p>{report.includesLocalAddresses ? "This report includes local addresses by explicit request." : "Local addresses, public IP, MAC address, hostname, and SSID were omitted."}</p></div>
        <div className="interface-grid">{report.interfaces.map((network) => (
          <article key={`${network.name}-${network.description}`}><strong>{network.name}</strong><span>{network.description}</span><dl><div><dt>Type</dt><dd>{network.type}</dd></div><div><dt>Link speed</dt><dd>{network.linkSpeedMbps ? `${network.linkSpeedMbps} Mbps` : "—"}</dd></div><div><dt>IPv4 MTU</dt><dd>{network.ipv4Mtu ?? "—"}</dd></div><div><dt>IP support</dt><dd>{[network.supportsIpv4 && "v4", network.supportsIpv6 && "v6"].filter(Boolean).join(" + ")}</dd></div></dl></article>
        ))}</div>
      </section>
    </section>
  );
}

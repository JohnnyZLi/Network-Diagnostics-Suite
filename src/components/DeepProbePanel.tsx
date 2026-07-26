import { useRef, useState } from "react";
import { formatBytes, formatLatency, formatRate } from "../core/format";
import type { DeepProbeReport } from "../types/deep-probe";

function isDeepProbeReport(value: unknown): value is DeepProbeReport {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Partial<DeepProbeReport>;
  return (candidate.schemaVersion === "1.0" || candidate.schemaVersion === "1.1")
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
      if (!isDeepProbeReport(parsed)) throw new Error("This is not a supported Network Deep Probe report.");
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
      <section className="deep-probe jl-page-section" id="deep-probe">
        <div className="deep-probe__intro jl-stack">
          <span className="eyebrow jl-eyebrow">Optional local diagnostics</span>
          <h2>Bring the operating-system layer into the report.</h2>
          <p className="jl-page-lede">Run the native probe on Windows 11, macOS, or Linux, then open its JSON report here. Parsing happens in this tab; the selected file is never transmitted.</p>
        </div>
        <div className="deep-probe__actions jl-actions">
          <input
            ref={inputRef}
            type="file"
            accept="application/json,.json"
            onChange={(event) => void importFile(event.target.files?.[0])}
            id="probe-report"
          />
          <label className="jl-button jl-button--primary" htmlFor="probe-report">Import probe report <span aria-hidden="true">⇧</span></label>
          <a className="jl-button" href="https://github.com/JohnnyZLi/Network-Diagnostics-Suite#native-deep-probe" target="_blank" rel="noreferrer">Get a native build <span aria-hidden="true">↗</span></a>
          <small>Schema 1.0–1.1 · maximum 5 MB · processed locally</small>
          {error && <p className="jl-callout jl-callout--danger" role="alert">{error}</p>}
        </div>
      </section>
    );
  }

  const fastestDns = fastestResolver(report);
  return (
    <section className="deep-report jl-page-section" id="deep-probe">
      <div className="section-heading section-heading--actions jl-page-section__header">
        <div>
          <span className="eyebrow jl-eyebrow">Local report · {report.architecture} · {new Date(report.generatedAt).toLocaleString()}</span>
          <h2>Deep network path</h2>
          <p className="report-platform">{report.operatingSystem}</p>
        </div>
        <button className="jl-button" type="button" onClick={() => setReport(null)}>Close report</button>
      </div>

      <div className="deep-summary jl-metric-grid jl-responsive-region">
        <article className="jl-metric"><span className="jl-metric__label">ICMP packet loss</span><strong className="jl-metric__value">{report.internetPing.statistics.lossPercent.toFixed(1)}<small>%</small></strong><p>{report.internetPing.statistics.received} of {report.internetPing.statistics.sent} replies</p></article>
        <article className="jl-metric"><span className="jl-metric__label">Internet latency</span><strong className="jl-metric__value">{formatLatency(report.internetPing.statistics.medianMs)}<small>ms</small></strong><p>{formatLatency(report.internetPing.statistics.jitterMs)} ms jitter</p></article>
        {report.localLink && <article className="jl-metric"><span className="jl-metric__label">LAN download</span><strong className="jl-metric__value">{formatRate(report.localLink.downloadMbps)}<small>Mbps</small></strong><p>{report.localLink.concurrency} parallel streams</p></article>}
        {report.localLink && <article className="jl-metric"><span className="jl-metric__label">LAN upload</span><strong className="jl-metric__value">{formatRate(report.localLink.uploadMbps)}<small>Mbps</small></strong><p>{formatLatency(report.localLink.latency.medianMs)} ms server response</p></article>}
        <article className="jl-metric"><span className="jl-metric__label">Route</span><strong className="jl-metric__value">{report.traceRoute.hops.length}<small>hops</small></strong><p>{report.traceRoute.reachedDestination ? "Destination reached" : "Partial path"}</p></article>
        <article className="jl-metric"><span className="jl-metric__label">Path MTU</span><strong className="jl-metric__value">{report.pathMtu.estimatedIpv4Mtu ?? "—"}<small>bytes</small></strong><p>{report.pathMtu.status}</p></article>
        <article className="jl-metric"><span className="jl-metric__label">Fastest DNS</span><strong className="jl-metric__value">{formatLatency(fastestDns?.medianMs)}<small>ms</small></strong><p>{fastestDns?.name ?? "No resolver answered"}</p></article>
      </div>

      {report.localLink && (
        <section className="report-panel local-link-panel jl-panel">
          <div className="report-panel__heading">
            <div><span className="eyebrow jl-eyebrow">Server-side isolation</span><h3>Local-link throughput</h3></div>
            <p>This result stayed on the local network between this client and a user-hosted native probe server.</p>
          </div>
          <div className="scope-grid jl-grid-3">
            <article className="jl-panel jl-panel--muted"><span>Download</span><strong>{formatRate(report.localLink.downloadMbps)} Mbps</strong><p>{formatBytes(report.localLink.downloadBytes)} transferred over {report.localLink.durationMs / 1000} seconds.</p></article>
            <article className="jl-panel jl-panel--muted"><span>Upload</span><strong>{formatRate(report.localLink.uploadMbps)} Mbps</strong><p>{formatBytes(report.localLink.uploadBytes)} transferred over {report.localLink.durationMs / 1000} seconds.</p></article>
            <article className="jl-panel jl-panel--muted"><span>LAN endpoint</span><strong>{report.localLink.target}:{report.localLink.port}</strong><p>{report.localLink.resolvedAddress ?? "Address unavailable"} · {report.localLink.concurrency} streams · {formatLatency(report.localLink.latency.medianMs)} ms median response.</p></article>
          </div>
        </section>
      )}

      <section className="report-panel jl-panel">
        <div className="report-panel__heading">
          <div><span className="eyebrow jl-eyebrow">Internet Control Message Protocol</span><h3>Traceroute to {report.traceRoute.target}</h3></div>
          <p>Three probes per time-to-live value. An asterisk is a timed-out reply, not necessarily a broken hop.</p>
        </div>
        <div className="deep-table-wrap jl-table-region">
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

      <div className="report-columns jl-grid-2 jl-responsive-region">
        <section className="report-panel jl-panel">
          <span className="eyebrow jl-eyebrow">UDP port 53</span><h3>DNS resolver timing</h3>
          <div className="deep-table-wrap jl-table-region">
            <table className="deep-table">
              <thead><tr><th>Resolver</th><th>Success</th><th>Median</th><th>95th pct.</th></tr></thead>
              <tbody>{report.dnsResolvers.map((resolver) => (
                <tr key={`${resolver.name}-${resolver.address}`}><td>{resolver.name}<small>{resolver.address}</small></td><td>{resolver.successful}/{resolver.attempts}</td><td>{formatLatency(resolver.medianMs)} ms</td><td>{formatLatency(resolver.p95Ms)} ms</td></tr>
              ))}</tbody>
            </table>
          </div>
        </section>
        <section className="report-panel jl-panel">
          <span className="eyebrow jl-eyebrow">Transport Layer Security</span><h3>Service connection phases</h3>
          <div className="deep-table-wrap jl-table-region">
            <table className="deep-table">
              <thead><tr><th>Service</th><th>DNS</th><th>TCP</th><th>TLS</th></tr></thead>
              <tbody>{report.serviceEndpoints.map((endpoint) => (
                <tr key={endpoint.host}><td>{endpoint.name}<small>{endpoint.applicationProtocol ?? endpoint.error ?? endpoint.host}</small></td><td>{formatLatency(endpoint.dnsMs)}</td><td>{formatLatency(endpoint.tcpMs)}</td><td>{formatLatency(endpoint.tlsMs)}</td></tr>
              ))}</tbody>
            </table>
          </div>
        </section>
      </div>

      <section className="report-panel interface-panel jl-panel">
        <div className="report-panel__heading"><div><span className="eyebrow jl-eyebrow">Local link</span><h3>Active interfaces</h3></div><p>{report.includesLocalAddresses ? "This report includes local addresses by explicit request." : "Local addresses, public IP, MAC address, hostname, and SSID were omitted."}</p></div>
        <div className="interface-grid jl-grid-3 jl-responsive-region">{report.interfaces.map((network) => (
          <article className="jl-panel jl-panel--muted" key={`${network.name}-${network.description}`}><strong>{network.name}</strong><span>{network.description}</span><dl><div><dt>Type</dt><dd>{network.type}</dd></div><div><dt>Link speed</dt><dd>{network.linkSpeedMbps ? `${network.linkSpeedMbps} Mbps` : "—"}</dd></div><div><dt>IPv4 MTU</dt><dd>{network.ipv4Mtu ?? "—"}</dd></div><div><dt>IP support</dt><dd>{[network.supportsIpv4 && "v4", network.supportsIpv6 && "v6"].filter(Boolean).join(" + ")}</dd></div></dl></article>
        ))}</div>
      </section>
    </section>
  );
}

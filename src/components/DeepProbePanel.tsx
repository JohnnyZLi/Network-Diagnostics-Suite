import { useRef, useState } from "react";
import { formatBytes, formatLatency, formatRate } from "../core/format";
import type { DeepProbeReport, NativeCombinedReport } from "../types/deep-probe";
import { DiagnosticFindingList } from "./DiagnosticFindings";

type DisplayProbeReport = DeepProbeReport & { combined?: NativeCombinedReport };

function isDeepProbeReport(value: unknown): value is DeepProbeReport {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Partial<DeepProbeReport>;
  return (candidate.schemaVersion === "1.0" || candidate.schemaVersion === "1.1" || candidate.schemaVersion === "1.2")
    && typeof candidate.target === "string"
    && Array.isArray(candidate.interfaces)
    && Array.isArray(candidate.dnsResolvers)
    && Array.isArray(candidate.serviceEndpoints)
    && Array.isArray(candidate.traceRoute?.hops)
    && typeof candidate.internetPing?.statistics === "object";
}

function isCombinedReport(value: unknown): value is NativeCombinedReport {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Partial<NativeCombinedReport>;
  return candidate.schemaVersion === "2.0"
    && typeof candidate.run === "object"
    && typeof candidate.transferPlan === "object"
    && isDeepProbeReport(candidate.deepDiagnostics);
}

function fastestResolver(report: DeepProbeReport) {
  return report.dnsResolvers
    .filter((resolver) => resolver.medianMs !== undefined)
    .sort((left, right) => (left.medianMs ?? Number.POSITIVE_INFINITY) - (right.medianMs ?? Number.POSITIVE_INFINITY))[0];
}

function sampleText(samples: Array<number | null>): string {
  return samples.map((sample) => sample === null ? "*" : formatLatency(sample)).join(" / ");
}

function wifiSummary(report: DeepProbeReport): string {
  const wifi = report.wifi;
  if (!wifi || wifi.status === "unavailable") return wifi?.error ?? "Wi-Fi details unavailable";
  if (wifi.status === "not-connected") return "Wireless interface not connected";
  return [
    wifi.ssid,
    wifi.interfaceName,
    wifi.signalPercent === undefined ? undefined : `${wifi.signalPercent}% signal`,
    wifi.rssiDbm === undefined ? undefined : `${wifi.rssiDbm} dBm`,
    wifi.band,
    wifi.channel === undefined ? undefined : `channel ${wifi.channel}`,
    wifi.protocol
  ].filter(Boolean).join(" · ");
}

export function DeepProbePanel() {
  const [report, setReport] = useState<DisplayProbeReport | null>(null);
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
      if (isDeepProbeReport(parsed)) {
        setReport(parsed);
      } else if (isCombinedReport(parsed)) {
        setReport({ ...parsed.deepDiagnostics, combined: parsed });
      } else {
        throw new Error("This is not a supported Network Deep Probe report.");
      }
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
          <p>Run the native probe on Windows 11, macOS, or Linux, then open its JSON report here. Parsing happens in this tab; the selected file is never transmitted.</p>
        </div>
        <div className="deep-probe__actions">
          <input
            ref={inputRef}
            type="file"
            accept="application/json,.json"
            onChange={(event) => void importFile(event.target.files?.[0])}
            id="probe-report"
          />
          <label htmlFor="probe-report">Import probe report <span aria-hidden="true">⇧</span></label>
          <a href="https://github.com/JohnnyZLi/Network-Diagnostics-Suite#native-deep-probe" target="_blank" rel="noreferrer">Get a native build <span aria-hidden="true">↗</span></a>
          <small>Schema 1.0–2.0 · maximum 5 MB · processed locally</small>
          {error && <p role="alert">{error}</p>}
        </div>
      </section>
    );
  }

  const fastestDns = fastestResolver(report);
  const combined = report.combined;
  const transfer = combined?.internetTransfer;
  const defaultRoute = report.routing?.entries.find((entry) => entry.isDefault);
  return (
    <section className="deep-report" id="deep-probe">
      <div className="section-heading section-heading--actions">
        <div>
          <span className="eyebrow">Local report · {report.architecture} · {new Date(report.generatedAt).toLocaleString()}</span>
          <h2>Deep network path</h2>
          <p className="report-platform">{report.operatingSystem}</p>
        </div>
        <button type="button" onClick={() => setReport(null)}>Close report</button>
      </div>

      <div className="deep-summary">
        {transfer && <article><span>Internet download</span><strong>{formatRate(transfer.download.steadyMbps)}<small>Mbps</small></strong><p>{transfer.download.qualification}</p></article>}
        {transfer && <article><span>Internet upload</span><strong>{formatRate(transfer.upload.steadyMbps)}<small>Mbps</small></strong><p>{transfer.upload.qualification}</p></article>}
        <article><span>ICMP packet loss</span><strong>{report.internetPing.statistics.lossPercent.toFixed(1)}<small>%</small></strong><p>{report.internetPing.statistics.received} of {report.internetPing.statistics.sent} replies</p></article>
        <article><span>Internet latency</span><strong>{formatLatency(report.internetPing.statistics.medianMs)}<small>ms</small></strong><p>{formatLatency(report.internetPing.statistics.jitterMs)} ms jitter</p></article>
        {report.localLink && <article><span>LAN download</span><strong>{formatRate(report.localLink.downloadMbps)}<small>Mbps</small></strong><p>{report.localLink.concurrency} parallel streams</p></article>}
        {report.localLink && <article><span>LAN upload</span><strong>{formatRate(report.localLink.uploadMbps)}<small>Mbps</small></strong><p>{formatLatency(report.localLink.latency.medianMs)} ms server response</p></article>}
        <article><span>Route</span><strong>{report.traceRoute.hops.length}<small>hops</small></strong><p>{report.traceRoute.reachedDestination ? "Destination reached" : "Partial path"}</p></article>
        <article><span>Path MTU</span><strong>{report.pathMtu.estimatedIpv4Mtu ?? "—"}<small>bytes</small></strong><p>{report.pathMtu.status}</p></article>
        <article><span>Fastest DNS</span><strong>{formatLatency(fastestDns?.medianMs)}<small>ms</small></strong><p>{fastestDns?.name ?? "No resolver answered"}</p></article>
      </div>

      {combined?.findings && combined.findings.length > 0 && (
        <DiagnosticFindingList
          findings={combined.findings}
          context={combined.measurement
            ? `${combined.measurement.engine} engine · ${combined.measurement.selectedEndpoint.name}`
            : "Imported native report · endpoint context unavailable"}
        />
      )}

      {combined && transfer && (
        <section className="report-panel native-transfer-panel">
          <div className="report-panel__heading">
            <div><span className="eyebrow">Native Internet transfer</span><h3>{combined.transferPlan.profileName} · {combined.run.transferMethod}</h3></div>
            <p>First-party transfer stages ran against {transfer.origin} before the operating-system diagnostics.</p>
          </div>
          <div className="scope-grid">
            <article><span>Transfer cap</span><strong>{formatBytes(combined.transferPlan.transferCapBytes)}</strong><p>{combined.transferPlan.estimatedSeconds} second transfer estimate.</p></article>
            <article><span>Loaded download delay</span><strong>+{formatLatency(transfer.downloadLatency.increaseMs)} ms</strong><p>Grade {transfer.downloadLatency.grade} during the primary download stage.</p></article>
            <article><span>Loaded upload delay</span><strong>+{formatLatency(transfer.uploadLatency.increaseMs)} ms</strong><p>Grade {transfer.uploadLatency.grade} during the primary upload stage.</p></article>
            <article><span>Measured data</span><strong>{formatBytes(transfer.dataUsedBytes)}</strong><p>Payload bytes counted by the native transfer engine.</p></article>
          </div>

          {transfer.flowMeasurements.length > 0 && (
            <div className="deep-table-wrap">
              <table className="deep-table">
                <thead><tr><th>Method</th><th>Connections</th><th>Download</th><th>Upload</th><th>Loaded delay</th></tr></thead>
                <tbody>{transfer.flowMeasurements.map((measurement) => (
                  <tr key={measurement.strategy}>
                    <td>{measurement.strategy}</td>
                    <td>{measurement.connections}</td>
                    <td>{measurement.download ? `${formatRate(measurement.download.steadyMbps)} Mbps` : "—"}</td>
                    <td>{measurement.upload ? `${formatRate(measurement.upload.steadyMbps)} Mbps` : "—"}</td>
                    <td>{measurement.downloadLatency ? `+${formatLatency(measurement.downloadLatency.increaseMs)} ms down` : "—"}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}

          {transfer.downloadScaling.length > 2 && (
            <div className="deep-table-wrap">
              <table className="deep-table">
                <thead><tr><th>Download connections</th><th>Steady rate</th><th>Whole phase</th><th>Loaded delay</th><th>Quality</th></tr></thead>
                <tbody>{transfer.downloadScaling.map((point) => (
                  <tr key={point.connections}>
                    <td>{point.connections}</td>
                    <td>{formatRate(point.download.steadyMbps)} Mbps</td>
                    <td>{formatRate(point.download.mbps)} Mbps</td>
                    <td>+{formatLatency(point.downloadLatency.increaseMs)} ms</td>
                    <td>{point.download.qualification}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {(report.wifi || report.routing) && (
        <section className="report-panel platform-network-panel">
          <div className="report-panel__heading">
            <div><span className="eyebrow">Operating-system network details</span><h3>Wi-Fi and routing</h3></div>
            <p>Unsupported commands and restricted fields remain explicitly unavailable rather than being inferred.</p>
          </div>
          <div className="scope-grid">
            <article>
              <span>Wi-Fi</span>
              <strong>{report.wifi?.status === "available" ? `${report.wifi.signalPercent ?? "—"}%` : report.wifi?.status ?? "Unavailable"}</strong>
              <p>{wifiSummary(report)}</p>
            </article>
            <article>
              <span>Route table</span>
              <strong>{report.routing?.status === "available" ? report.routing.entries.length : "—"}<small>routes</small></strong>
              <p>{defaultRoute ? `Default through ${defaultRoute.interfaceName ?? "unknown interface"}${defaultRoute.gateway ? ` via ${defaultRoute.gateway}` : ""}.` : report.routing?.error ?? "No default route identified."}</p>
            </article>
          </div>
        </section>
      )}

      {report.localLink && (
        <section className="report-panel local-link-panel">
          <div className="report-panel__heading">
            <div><span className="eyebrow">Server-side isolation</span><h3>Local-link throughput</h3></div>
            <p>This result stayed on the local network between this client and a user-hosted native probe server.</p>
          </div>
          <div className="scope-grid">
            <article><span>Download</span><strong>{formatRate(report.localLink.downloadMbps)} Mbps</strong><p>{formatBytes(report.localLink.downloadBytes)} transferred over {report.localLink.durationMs / 1000} seconds.</p></article>
            <article><span>Upload</span><strong>{formatRate(report.localLink.uploadMbps)} Mbps</strong><p>{formatBytes(report.localLink.uploadBytes)} transferred over {report.localLink.durationMs / 1000} seconds.</p></article>
            <article><span>LAN endpoint</span><strong>{report.localLink.target}:{report.localLink.port}</strong><p>{report.localLink.resolvedAddress ?? "Address unavailable"} · {report.localLink.concurrency} streams · {formatLatency(report.localLink.latency.medianMs)} ms median response.</p></article>
          </div>
        </section>
      )}

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

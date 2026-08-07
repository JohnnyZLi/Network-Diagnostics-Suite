import { useState } from 'react';
import { desktopBridge } from './bridge';
import './monitor.css';
import './workbench.css';

export type MonitorWindow = '1m' | '5m' | '1h' | '24h' | '7d';

type MonitorMetric = { label: string; value: string };
type MonitorComponent = {
  title: string;
  score?: number | null;
  band: string;
  status: string;
  summary: string;
  metrics: MonitorMetric[];
};
type MonitorTimelineSample = {
  timestamp: string;
  state: 'responsive' | 'laggy' | 'unresponsive' | 'inactive';
  latencyMs?: number | null;
  jitterMs?: number | null;
  packetLossPercent: number;
};
type MonitorAlert = {
  id: string;
  timestamp: string;
  kind: string;
  severity: string;
  title: string;
  detail: string;
  isRead: boolean;
};

type MonitorExportResult = {
  cancelled: boolean;
  fileName?: string | null;
  includesLocalIdentifiers?: boolean;
  window?: MonitorWindow;
};

export type MonitorSnapshot = {
  enabled: boolean;
  running: boolean;
  window: MonitorWindow;
  score?: number | null;
  band: string;
  status: string;
  summary: string;
  deviceName: string;
  interfaceName: string;
  lastUpdated: string;
  unreadAlertCount: number;
  responsiveness: MonitorComponent;
  reliability: MonitorComponent;
  speed: MonitorComponent;
  timeline: MonitorTimelineSample[];
  alerts: MonitorAlert[];
};

const windows: Array<{ id: MonitorWindow; label: string }> = [
  { id: '1m', label: '1 min' },
  { id: '5m', label: '5 min' },
  { id: '1h', label: '1 hr' },
  { id: '24h', label: '24 hr' },
  { id: '7d', label: '7 days' },
];

export function ContinuousDiagnostics({
  snapshot,
  loading,
  error,
  onUpdate,
  onError,
  onMeasureCapacity,
}: {
  snapshot: MonitorSnapshot | null;
  loading: boolean;
  error: string | null;
  onUpdate: (snapshot: MonitorSnapshot) => void;
  onError: (message: string | null) => void;
  onMeasureCapacity: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [clearConfirm, setClearConfirm] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  async function request(method: string, payload: Record<string, unknown> = {}) {
    setBusy(true);
    setNotice(null);
    onError(null);
    try {
      const next = await desktopBridge.request<MonitorSnapshot>(method, payload);
      onUpdate(next);
      return next;
    } catch (value) {
      onError(value instanceof Error ? value.message : 'Live network health could not be updated.');
      return null;
    } finally {
      setBusy(false);
    }
  }

  async function clearAlerts() {
    if (!clearConfirm) {
      setClearConfirm(true);
      return;
    }
    await request('monitor.clearAlerts');
    setClearConfirm(false);
  }

  async function copySummary() {
    if (!snapshot) return;
    setBusy(true);
    setNotice(null);
    onError(null);
    try {
      await writeClipboard(monitorSummary(snapshot));
      setNotice('Network summary copied.');
    } catch (value) {
      onError(value instanceof Error ? value.message : 'The network summary could not be copied.');
    } finally {
      setBusy(false);
    }
  }

  async function exportSnapshot() {
    setBusy(true);
    setNotice(null);
    onError(null);
    try {
      const result = await desktopBridge.request<MonitorExportResult>('monitor.exportSnapshot');
      if (!result.cancelled) setNotice(`Snapshot exported as ${result.fileName ?? 'HTML'}.`);
    } catch (value) {
      onError(value instanceof Error ? value.message : 'The network snapshot could not be exported.');
    } finally {
      setBusy(false);
    }
  }

  async function exportHistory() {
    setBusy(true);
    setNotice(null);
    onError(null);
    try {
      const result = await desktopBridge.request<MonitorExportResult>('monitor.exportHistory');
      if (!result.cancelled) {
        const privacy = result.includesLocalIdentifiers ? 'with enabled local identifiers' : 'with local identifiers redacted';
        setNotice(`History exported as ${result.fileName ?? 'CSV'} ${privacy}.`);
      }
    } catch (value) {
      onError(value instanceof Error ? value.message : 'Monitoring history could not be exported.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section id="live-network-health" className="workbench-section live-health-section" aria-labelledby="live-health-title">
      <div className="workbench-section-header live-health-header">
        <div>
          <span className="section-kicker">Live network health</span>
          <h1 id="live-health-title">Your network right now</h1>
          <p>Passive monitoring answers whether the connection is healthy before you decide to run a controlled diagnostic.</p>
        </div>
        {snapshot && (
          <div className="live-health-actions">
            <span className={`monitor-state ${snapshot.running ? 'running' : ''}`}><i />{snapshot.running ? 'Monitoring active' : 'Monitoring paused'}</span>
            <button type="button" className="monitor-toggle compact" disabled={busy} onClick={() => void request('monitor.setEnabled', { enabled: !snapshot.enabled })}>
              {snapshot.enabled ? 'Pause' : 'Resume'}
            </button>
            <details className="monitor-share-menu">
              <summary>Share & export</summary>
              <div>
                <button type="button" disabled={busy} onClick={() => void copySummary()}>Copy summary</button>
                <button type="button" disabled={busy} onClick={() => void exportSnapshot()}>Snapshot HTML</button>
                <button type="button" disabled={busy} onClick={() => void exportHistory()}>History CSV</button>
                <small>CSV redacts local identifiers unless Advanced diagnostics explicitly enables them.</small>
              </div>
            </details>
          </div>
        )}
      </div>

      {(loading || !snapshot) && !error ? (
        <div className="workbench-loading"><span className="monitor-loader" /><strong>Reading local network history</strong><p>Heartbeat samples and alerts stay on this computer.</p></div>
      ) : snapshot ? (
        <>
          {error && <div className="monitor-error" role="alert">{error}</div>}
          {notice && <div className="monitor-notice" role="status">{notice}</div>}

          <div className="live-health-hero">
            <div className={`health-score-orb ${snapshot.band}`} aria-label={snapshot.score == null ? 'Building network score' : `Network health score ${snapshot.score}`}>
              <strong>{snapshot.score ?? '—'}</strong>
              <span>{snapshot.score == null ? 'Building baseline' : snapshot.status}</span>
              <small>Network health</small>
            </div>

            <div className="live-health-summary">
              <span className={`monitor-state ${snapshot.running ? 'running' : ''}`}><i />{snapshot.running ? `${snapshot.interfaceName} · ${snapshot.lastUpdated}` : 'Passive monitoring is paused'}</span>
              <h2>{healthHeadline(snapshot)}</h2>
              <p>{snapshot.summary}</p>
              <div className="live-health-metrics" aria-label="Current passive network measurements">
                <LiveMetric label="Latency" value={componentMetric(snapshot.responsiveness, 'Typical latency')} />
                <LiveMetric label="Jitter" value={componentMetric(snapshot.responsiveness, 'Typical jitter')} />
                <LiveMetric label="Loss" value={latestLoss(snapshot)} />
                <LiveMetric label="Availability" value={componentMetric(snapshot.reliability, 'Availability')} />
              </div>
            </div>
          </div>

          <div className="live-timeline-section">
            <div className="live-timeline-heading">
              <div><strong>Connection timeline</strong><span>{snapshot.timeline.length} samples · response state</span></div>
              <div className="monitor-window-control" aria-label="Monitoring history window">
                {windows.map((item) => (
                  <button
                    type="button"
                    key={item.id}
                    className={snapshot.window === item.id ? 'active' : ''}
                    disabled={busy}
                    onClick={() => void request('monitor.setWindow', { window: item.id })}
                  >{item.label}</button>
                ))}
              </div>
            </div>
            {snapshot.timeline.length > 0 ? (
              <div className="monitor-timeline" aria-label="Connection state timeline">
                {snapshot.timeline.map((sample, index) => (
                  <span
                    key={`${sample.timestamp}-${index}`}
                    className={sample.state}
                    title={`${formatTime(sample.timestamp)} · ${sample.state}${sample.latencyMs == null ? '' : ` · ${formatNumber(sample.latencyMs)} ms`}`}
                  />
                ))}
              </div>
            ) : <p className="monitor-muted">The first heartbeat sample will appear after the native monitor reaches its endpoint.</p>}
          </div>

          <div className="monitor-components live-health-components">
            <MonitorComponentCard component={snapshot.responsiveness} />
            <MonitorComponentCard component={snapshot.reliability} />
            <CapacityCard component={snapshot.speed} onAction={onMeasureCapacity} />
          </div>

          {snapshot.alerts.length === 0 ? (
            <NoAlertSummary snapshot={snapshot} />
          ) : (
            <section className="monitor-alert-section live-alert-section">
              <div className="monitor-section-heading monitor-alert-heading">
                <div><strong>Issues & alerts</strong><span>{snapshot.unreadAlertCount > 0 ? `${snapshot.unreadAlertCount} unread` : `${snapshot.alerts.length} recorded`}</span></div>
                <div className="monitor-alert-actions">
                  {snapshot.unreadAlertCount > 0 && <button type="button" disabled={busy} onClick={() => void request('monitor.markAlertsRead')}>Mark read</button>}
                  <button type="button" disabled={busy} className={clearConfirm ? 'confirm' : ''} onClick={() => void clearAlerts()}>{clearConfirm ? 'Confirm clear' : 'Clear'}</button>
                </div>
              </div>
              <div className="monitor-alert-list compact">
                {snapshot.alerts.slice(0, 3).map((alert) => (
                  <article key={alert.id} className={`monitor-alert ${alert.severity} ${alert.isRead ? 'read' : ''}`}>
                    <span>{formatTime(alert.timestamp)} · {labelize(alert.kind)}</span>
                    <strong>{alert.title}</strong>
                    <p>{alert.detail}</p>
                  </article>
                ))}
              </div>
            </section>
          )}
        </>
      ) : (
        <div className="workbench-loading monitor-error"><strong>Live network health unavailable</strong><p>{error}</p></div>
      )}
    </section>
  );
}

function NoAlertSummary({ snapshot }: { snapshot: MonitorSnapshot }) {
  const healthy = snapshot.band === 'excellent' || snapshot.band === 'good';
  return (
    <div className={`monitor-healthy-strip ${healthy ? '' : 'neutral'}`}>
      <span aria-hidden="true">{healthy ? '✓' : '•'}</span>
      <div>
        <strong>{healthy ? 'No issues detected' : 'No discrete alerts recorded'}</strong>
        <small>{healthy
          ? 'No outages, network changes, or meaningful degradation are recorded in this window.'
          : `No outage or network-change event is recorded, but current measurements still rate the connection ${snapshot.status.toLowerCase()}.`}</small>
      </div>
    </div>
  );
}

function LiveMetric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function MonitorComponentCard({ component }: { component: MonitorComponent }) {
  return (
    <article className="monitor-component-card">
      <div className="monitor-component-top"><div><span>{component.title}</span><strong>{component.status}</strong></div><b>{component.score ?? '—'}</b></div>
      <p>{component.summary}</p>
      {component.metrics.length > 0 && (
        <div className="monitor-component-metrics">
          {component.metrics.slice(0, 4).map((metric) => <div key={metric.label}><span>{metric.label}</span><strong>{metric.value}</strong></div>)}
        </div>
      )}
    </article>
  );
}

function CapacityCard({ component, onAction }: { component: MonitorComponent; onAction: () => void }) {
  const download = componentMetric(component, 'Content download');
  const upload = componentMetric(component, 'Content upload');
  const expectedDownload = componentMetric(component, 'Expected download');
  const expectedUpload = componentMetric(component, 'Expected upload');
  const measured = component.score != null && (download !== '—' || upload !== '—');

  return (
    <article className={`monitor-component-card capacity-card ${measured ? 'measured' : 'unmeasured'}`}>
      <div className="capacity-card-heading">
        <div><span>Capacity</span><strong>{measured ? 'Recent content measurement' : 'Not measured yet'}</strong></div>
        {measured && <small className={`capacity-rating ${component.band}`}>{component.status}</small>}
      </div>
      {measured ? (
        <>
          <div className="capacity-values" aria-label="Last measured content capacity">
            <div><span>Download</span><strong>{download}</strong></div>
            <div><span>Upload</span><strong>{upload}</strong></div>
          </div>
          <p>{component.summary}</p>
          <div className="capacity-expectations">
            <span>Expected</span>
            <strong>{expectedDownload} ↓ · {expectedUpload} ↑</strong>
          </div>
        </>
      ) : (
        <p>Run a lightweight content-speed measurement to establish a download and upload baseline. Passive monitoring does not continuously load the connection.</p>
      )}
      <button type="button" className="monitor-card-action" onClick={onAction}>{measured ? 'Measure again' : 'Measure capacity'}</button>
    </article>
  );
}

function healthHeadline(snapshot: MonitorSnapshot): string {
  if (snapshot.score == null) return 'Building a baseline for this connection';
  switch (snapshot.band) {
    case 'excellent': return 'Connection is healthy';
    case 'good': return 'Connection is performing well';
    case 'fair': return 'Connection needs attention';
    case 'degraded': return 'Connection is degraded';
    case 'poor': return 'Connection is having problems';
    default: return snapshot.status;
  }
}

function componentMetric(component: MonitorComponent, label: string): string {
  return component.metrics.find((metric) => metric.label.toLowerCase() === label.toLowerCase())?.value ?? '—';
}

function latestLoss(snapshot: MonitorSnapshot): string {
  const sample = snapshot.timeline.at(-1);
  return sample ? `${formatNumber(sample.packetLossPercent)}%` : '—';
}

function monitorSummary(snapshot: MonitorSnapshot): string {
  const capacity = snapshot.speed.score == null
    ? 'Not measured'
    : `${componentMetric(snapshot.speed, 'Content download')} down · ${componentMetric(snapshot.speed, 'Content upload')} up · ${snapshot.speed.status}`;
  return [
    `Network score: ${snapshot.score ?? 'Not enough data'} · ${snapshot.status}`,
    snapshot.summary,
    `Responsiveness: ${snapshot.responsiveness.score ?? '—'} · Reliability: ${snapshot.reliability.score ?? '—'} · Capacity: ${capacity}`,
    `Window: ${snapshot.window} · Updated: ${snapshot.lastUpdated}`,
  ].join('\n');
}

async function writeClipboard(value: string): Promise<void> {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return;
  }
  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.setAttribute('readonly', '');
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.select();
  const copied = document.execCommand('copy');
  textarea.remove();
  if (!copied) throw new Error('Clipboard access is not available.');
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value);
}

function formatTime(value: string): string {
  return new Date(value).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function labelize(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/-/g, ' ');
}
import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import './monitor.css';

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

export function MonitorPanel({
  open,
  snapshot,
  loading,
  error,
  diagnosticRunning,
  onClose,
  onUpdate,
  onError,
  onRunContentSpeed,
  onRunPeakSpeed,
}: {
  open: boolean;
  snapshot: MonitorSnapshot | null;
  loading: boolean;
  error: string | null;
  diagnosticRunning: boolean;
  onClose: () => void;
  onUpdate: (snapshot: MonitorSnapshot) => void;
  onError: (message: string | null) => void;
  onRunContentSpeed: () => void;
  onRunPeakSpeed: () => void;
}) {
  const panelRef = useRef<HTMLElement>(null);
  const closeRef = useRef(onClose);
  const [busy, setBusy] = useState(false);
  const [clearConfirm, setClearConfirm] = useState(false);
  const [peakConfirm, setPeakConfirm] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  closeRef.current = onClose;

  useEffect(() => {
    if (!open) return;
    setClearConfirm(false);
    setPeakConfirm(false);
    setNotice(null);
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const frame = window.requestAnimationFrame(() => focusableElements(panelRef.current)[0]?.focus());

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeRef.current();
        return;
      }
      if (event.key !== 'Tab') return;
      const elements = focusableElements(panelRef.current);
      if (elements.length === 0) return;
      const first = elements[0];
      const last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = previousOverflow;
      previousFocus?.focus();
    };
  }, [open]);

  if (!open) return null;

  async function request(method: string, payload: Record<string, unknown> = {}) {
    setBusy(true);
    setNotice(null);
    onError(null);
    try {
      const next = await desktopBridge.request<MonitorSnapshot>(method, payload);
      onUpdate(next);
      return next;
    } catch (value) {
      onError(value instanceof Error ? value.message : 'The network monitor could not be updated.');
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
      setNotice('Network summary copied to the clipboard.');
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

  function runPeakSpeed() {
    if (!peakConfirm) {
      setPeakConfirm(true);
      setNotice('Peak uses the Stress profile and the largest transfer budget. Select Run peak again to continue.');
      return;
    }
    setPeakConfirm(false);
    setNotice(null);
    onRunPeakSpeed();
  }

  return (
    <div className="monitor-layer" role="presentation">
      <button className="monitor-backdrop" type="button" aria-label="Close network monitor" onClick={onClose} tabIndex={-1} />
      <aside ref={panelRef} className="monitor-panel" role="dialog" aria-modal="true" aria-labelledby="monitor-title">
        <header className="monitor-header">
          <div>
            <span className="monitor-kicker">Continuous diagnostics</span>
            <h2 id="monitor-title">Network monitor</h2>
            <p>{snapshot ? `${snapshot.interfaceName} · ${snapshot.lastUpdated}` : 'Loading local network history…'}</p>
          </div>
          <button type="button" className="monitor-icon-button" onClick={onClose} aria-label="Close network monitor">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18" /></svg>
          </button>
        </header>

        <div className="monitor-body">
          {(loading || !snapshot) && !error ? (
            <div className="monitor-empty"><span className="monitor-loader" /><strong>Reading monitor history</strong><p>Samples and alerts are stored locally on this computer.</p></div>
          ) : snapshot ? (
            <>
              {error && <div className="monitor-error" role="alert">{error}</div>}
              {notice && <div className="monitor-notice" role="status">{notice}</div>}
              <section className="monitor-hero">
                <div className={`monitor-score ${snapshot.band}`}>
                  <strong>{snapshot.score ?? '—'}</strong>
                  <span>{snapshot.score == null ? 'No score yet' : 'Network score'}</span>
                </div>
                <div className="monitor-hero-copy">
                  <span className={`monitor-state ${snapshot.running ? 'running' : ''}`}><i />{snapshot.running ? 'Monitoring active' : 'Monitoring paused'}</span>
                  <h3>{snapshot.status}</h3>
                  <p>{snapshot.summary}</p>
                  <button
                    type="button"
                    className="monitor-toggle"
                    disabled={busy}
                    onClick={() => void request('monitor.setEnabled', { enabled: !snapshot.enabled })}
                  >
                    {snapshot.enabled ? 'Pause monitoring' : 'Resume monitoring'}
                  </button>
                </div>
              </section>

              <section className="monitor-window-section">
                <div className="monitor-section-heading"><strong>History window</strong><span>{snapshot.timeline.length} samples shown</span></div>
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
              </section>

              <section className="monitor-timeline-section">
                <div className="monitor-section-heading"><strong>Connection timeline</strong><span>Response state</span></div>
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
              </section>

              <section className="monitor-components">
                <MonitorComponentCard component={snapshot.responsiveness} />
                <MonitorComponentCard component={snapshot.reliability} />
                <MonitorComponentCard component={snapshot.speed} />
              </section>

              <section className="monitor-utility-section">
                <div className="monitor-section-heading"><strong>Speed checks</strong><span>Feed Speed history</span></div>
                <div className="monitor-speed-actions">
                  <button type="button" disabled={busy || diagnosticRunning} onClick={onRunContentSpeed}>
                    <span className="monitor-utility-icon"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 17 9 13l3 3 7-8" /><path d="M14 8h5v5" /></svg></span>
                    <span><strong>Content</strong><small>Low-data aggregate check</small></span>
                    <b>Run</b>
                  </button>
                  <button type="button" className={peakConfirm ? 'confirm' : ''} disabled={busy || diagnosticRunning} onClick={runPeakSpeed}>
                    <span className="monitor-utility-icon"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 16c2.2-4.7 5-7.3 8.3-7.8 2.7-.4 5.2.7 7.7 3.3" /><path d="M15 7h5v5" /></svg></span>
                    <span><strong>Peak</strong><small>Stress · aggregate capacity</small></span>
                    <b>{peakConfirm ? 'Run peak' : 'Run'}</b>
                  </button>
                </div>
              </section>

              <section className="monitor-export-section">
                <div className="monitor-section-heading"><strong>Share & export</strong><span>{snapshot.window} window</span></div>
                <div className="monitor-export-actions">
                  <button type="button" disabled={busy} onClick={() => void copySummary()}>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="8" y="8" width="10" height="10" rx="2" /><path d="M15 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2" /></svg>
                    <span>Copy summary</span>
                  </button>
                  <button type="button" disabled={busy} onClick={() => void exportSnapshot()}>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 15V4" /><path d="m8 8 4-4 4 4" /><path d="M5 13v5a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-5" /></svg>
                    <span>Snapshot HTML</span>
                  </button>
                  <button type="button" disabled={busy} onClick={() => void exportHistory()}>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4v11" /><path d="m8 11 4 4 4-4" /><path d="M5 18h14" /></svg>
                    <span>History CSV</span>
                  </button>
                </div>
                <p className="monitor-export-copy">CSV exports redact local interface and network identifiers unless you explicitly enabled them in Advanced diagnostics.</p>
              </section>

              <section className="monitor-alert-section">
                <div className="monitor-section-heading monitor-alert-heading">
                  <div><strong>Alerts</strong><span>{snapshot.unreadAlertCount > 0 ? `${snapshot.unreadAlertCount} unread` : 'No unread alerts'}</span></div>
                  {snapshot.alerts.length > 0 && (
                    <div className="monitor-alert-actions">
                      {snapshot.unreadAlertCount > 0 && <button type="button" disabled={busy} onClick={() => void request('monitor.markAlertsRead')}>Mark read</button>}
                      <button type="button" disabled={busy} className={clearConfirm ? 'confirm' : ''} onClick={() => void clearAlerts()}>{clearConfirm ? 'Confirm clear' : 'Clear'}</button>
                    </div>
                  )}
                </div>
                {snapshot.alerts.length === 0 ? (
                  <div className="monitor-alert-empty"><strong>No alerts in this window</strong><p>Outages, recoveries, network changes, and meaningful degradation will appear here.</p></div>
                ) : (
                  <div className="monitor-alert-list">
                    {snapshot.alerts.map((alert) => (
                      <article key={alert.id} className={`monitor-alert ${alert.severity} ${alert.isRead ? 'read' : ''}`}>
                        <span>{formatTime(alert.timestamp)} · {labelize(alert.kind)}</span>
                        <strong>{alert.title}</strong>
                        <p>{alert.detail}</p>
                      </article>
                    ))}
                  </div>
                )}
              </section>
            </>
          ) : (
            <div className="monitor-empty monitor-error"><strong>Network monitor unavailable</strong><p>{error}</p></div>
          )}
        </div>
      </aside>
    </div>
  );
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

function focusableElements(root: HTMLElement | null): HTMLElement[] {
  if (!root) return [];
  return Array.from(root.querySelectorAll<HTMLElement>('button:not([disabled]), [href], input:not([disabled]), [tabindex]:not([tabindex="-1"])'));
}

function monitorSummary(snapshot: MonitorSnapshot): string {
  return [
    `Network score: ${snapshot.score ?? 'Not enough data'} · ${snapshot.status}`,
    snapshot.summary,
    `Responsiveness: ${snapshot.responsiveness.score ?? '—'} · Reliability: ${snapshot.reliability.score ?? '—'} · Speed: ${snapshot.speed.score ?? '—'}`,
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

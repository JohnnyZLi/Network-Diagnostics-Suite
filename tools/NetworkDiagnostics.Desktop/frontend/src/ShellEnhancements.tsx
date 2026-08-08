import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { desktopBridge } from './bridge';
import type { MonitorSnapshot, MonitorWindow } from './ContinuousDiagnostics';

type TimelineMetric = 'latency' | 'jitter' | 'loss';
type TimelineSample = MonitorSnapshot['timeline'][number];

type PortalTargets = {
  actions: Element | null;
  timeline: Element | null;
};

const timelineMetrics: Array<{ id: TimelineMetric; label: string }> = [
  { id: 'latency', label: 'Latency' },
  { id: 'jitter', label: 'Jitter' },
  { id: 'loss', label: 'Loss' },
];

const windowDurationMs: Record<MonitorWindow, number> = {
  '1m': 60_000,
  '5m': 5 * 60_000,
  '1h': 60 * 60_000,
  '24h': 24 * 60 * 60_000,
  '7d': 7 * 24 * 60 * 60_000,
};

export function ShellEnhancements() {
  const [snapshot, setSnapshot] = useState<MonitorSnapshot | null>(null);
  const [targets, setTargets] = useState<PortalTargets>({ actions: null, timeline: null });
  const [alertsOpen, setAlertsOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [clearConfirm, setClearConfirm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    document.documentElement.dataset.shellEnhancements = 'true';
    const refreshTargets = () => {
      setTargets((current) => {
        const next = {
          actions: document.querySelector('.product-actions'),
          timeline: document.querySelector('.live-timeline-section'),
        };
        return current.actions === next.actions && current.timeline === next.timeline ? current : next;
      });
    };
    refreshTargets();
    const observer = new MutationObserver(refreshTargets);
    observer.observe(document.body, { childList: true, subtree: true });
    return () => {
      observer.disconnect();
      delete document.documentElement.dataset.shellEnhancements;
    };
  }, []);

  useEffect(() => {
    if (!desktopBridge.available) return;
    let active = true;
    void desktopBridge.request<MonitorSnapshot>('monitor.get')
      .then((next) => { if (active) setSnapshot(next); })
      .catch(() => undefined);
    const remove = desktopBridge.on<MonitorSnapshot>('monitor.snapshot', (next) => setSnapshot(next));
    return () => {
      active = false;
      remove();
    };
  }, []);

  useEffect(() => {
    if (!alertsOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setAlertsOpen(false);
    };
    const onToolbarClick = (event: MouseEvent) => {
      const target = event.target;
      if (!(target instanceof Element)) return;
      if (target.closest('.history-trigger, .settings-trigger')) setAlertsOpen(false);
    };
    window.addEventListener('keydown', onKeyDown);
    document.addEventListener('click', onToolbarClick, true);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('click', onToolbarClick, true);
    };
  }, [alertsOpen]);

  async function requestMonitor(method: string) {
    if (!desktopBridge.available) return;
    setBusy(true);
    setError(null);
    try {
      const next = await desktopBridge.request<MonitorSnapshot>(method);
      setSnapshot(next);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Alerts could not be updated.');
    } finally {
      setBusy(false);
    }
  }

  function toggleAlerts() {
    if (!alertsOpen) {
      const activeHistory = document.querySelector<HTMLButtonElement>('.history-trigger.active');
      if (activeHistory) activeHistory.click();
      const activeSettings = document.querySelector<HTMLButtonElement>('.settings-trigger.active');
      if (activeSettings) activeSettings.click();
      setClearConfirm(false);
      setError(null);
    }
    setAlertsOpen((current) => !current);
  }

  async function clearAlerts() {
    if (!clearConfirm) {
      setClearConfirm(true);
      return;
    }
    await requestMonitor('monitor.clearAlerts');
    setClearConfirm(false);
  }

  const alertsButton = targets.actions ? createPortal(
    <button
      type="button"
      className={`alerts-trigger ${alertsOpen ? 'active' : ''}`}
      aria-label={snapshot?.unreadAlertCount ? `Alerts, ${snapshot.unreadAlertCount} unread` : 'Alerts'}
      aria-expanded={alertsOpen}
      onClick={toggleAlerts}
      disabled={!desktopBridge.available}
    >
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M6.8 9.6a5.2 5.2 0 0 1 10.4 0c0 5.2 2.1 5.7 2.1 5.7H4.7s2.1-.5 2.1-5.7Z" />
        <path d="M10 18.2h4" />
      </svg>
      <span>Alerts</span>
      {!!snapshot?.unreadAlertCount && <b>{snapshot.unreadAlertCount > 99 ? '99+' : snapshot.unreadAlertCount}</b>}
    </button>,
    targets.actions,
  ) : null;

  const timeline = targets.timeline && snapshot?.timeline.length ? createPortal(
    <ResponsiveTimeline snapshot={snapshot} />,
    targets.timeline,
  ) : null;

  return (
    <>
      {alertsButton}
      {timeline}
      {alertsOpen && (
        <div className="alerts-layer" role="presentation">
          <button type="button" className="alerts-backdrop" aria-label="Close alerts" onClick={() => setAlertsOpen(false)} />
          <aside className="alerts-panel" role="dialog" aria-modal="true" aria-labelledby="alerts-title">
            <header className="alerts-header">
              <div>
                <span>NETWORK EVENTS</span>
                <h2 id="alerts-title">Issues & alerts</h2>
                <p>{snapshot ? alertSummary(snapshot) : 'Reading local monitoring history…'}</p>
              </div>
              <button type="button" className="alerts-close" aria-label="Close alerts" onClick={() => setAlertsOpen(false)}>
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 7 10 10M17 7 7 17" /></svg>
              </button>
            </header>

            <div className="alerts-actions">
              <button type="button" disabled={busy || !snapshot?.unreadAlertCount} onClick={() => void requestMonitor('monitor.markAlertsRead')}>Mark all read</button>
              <button type="button" className={clearConfirm ? 'confirm' : ''} disabled={busy || !snapshot?.alerts.length} onClick={() => void clearAlerts()}>{clearConfirm ? 'Confirm clear' : 'Clear history'}</button>
            </div>

            {error && <div className="alerts-error" role="alert">{error}</div>}

            <div className="alerts-list">
              {!snapshot ? (
                <div className="alerts-empty"><strong>Loading alerts</strong><span>Monitoring events stay on this computer.</span></div>
              ) : snapshot.alerts.length === 0 ? (
                <div className="alerts-empty"><strong>No recorded issues</strong><span>Outages, recoveries, network changes, and meaningful degradation will appear here.</span></div>
              ) : snapshot.alerts.map((alert) => (
                <article key={alert.id} className={`alerts-item ${alert.severity} ${alert.isRead ? 'read' : 'unread'}`}>
                  <div className="alerts-item-meta"><span>{formatAlertTime(alert.timestamp)}</span><span>{labelize(alert.kind)}</span>{!alert.isRead && <i>Unread</i>}</div>
                  <strong>{alert.title}</strong>
                  <p>{alert.detail}</p>
                </article>
              ))}
            </div>
          </aside>
        </div>
      )}
    </>
  );
}

function ResponsiveTimeline({ snapshot }: { snapshot: MonitorSnapshot }) {
  const [metric, setMetric] = useState<TimelineMetric>('latency');
  const frameRef = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(1000);

  useEffect(() => {
    const frame = frameRef.current;
    if (!frame) return;
    const update = () => setWidth(Math.max(560, Math.round(frame.clientWidth || 1000)));
    update();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', update);
      return () => window.removeEventListener('resize', update);
    }
    const observer = new ResizeObserver(update);
    observer.observe(frame);
    return () => observer.disconnect();
  }, []);

  const samples = snapshot.timeline;
  const latest = latestMetricValue(samples, metric);
  const height = width < 760 ? 220 : 250;
  const left = width < 760 ? 52 : 58;
  const right = 18;
  const top = 18;
  const plotBottom = height - 86;
  const stateTop = height - 57;
  const stateHeight = 12;
  const timeY = height - 17;
  const plotWidth = Math.max(1, width - left - right);
  const plotHeight = plotBottom - top;
  const timestamps = samples.map((sample) => Date.parse(sample.timestamp));
  const validTimes = timestamps.filter(Number.isFinite);
  const latestTime = validTimes.length ? Math.max(...validTimes) : Date.now();
  const duration = windowDurationMs[snapshot.window];
  const startTime = latestTime - duration;
  const sparse = samples.length < 8;
  const values = samples.map((sample) => timelineMetricValue(sample, metric));
  const finiteValues = values.filter((value): value is number => value != null && Number.isFinite(value));
  const scaleMax = timelineScaleMax(metric, finiteValues);

  const xFor = (index: number) => {
    if (sparse) {
      if (samples.length <= 1) return left + plotWidth;
      return left + (index / (samples.length - 1)) * plotWidth;
    }
    const timestamp = timestamps[index];
    if (!Number.isFinite(timestamp)) return left + (index / Math.max(1, samples.length - 1)) * plotWidth;
    const ratio = Math.max(0, Math.min(1, (timestamp - startTime) / duration));
    return left + ratio * plotWidth;
  };

  const yFor = (value: number) => top + (1 - Math.max(0, Math.min(1, value / scaleMax))) * plotHeight;
  let path = '';
  let penDown = false;
  const points = samples.map((sample, index) => {
    const value = values[index];
    const x = xFor(index);
    const y = value == null ? null : yFor(value);
    if (y == null) {
      penDown = false;
    } else {
      path += `${penDown ? ' L' : ' M'} ${x.toFixed(2)} ${y.toFixed(2)}`;
      penDown = true;
    }
    return { sample, value, x, y };
  });

  const stateBlockWidth = samples.length <= 1 ? 14 : Math.max(5, Math.min(18, (plotWidth / Math.max(1, samples.length)) * .58));
  const yTicks = [scaleMax, scaleMax * .67, scaleMax * .33, 0];
  const timeTickCount = width >= 1200 ? 5 : 3;
  const timeTicks = sparse
    ? sparseTicks(samples, timestamps, left, plotWidth)
    : Array.from({ length: timeTickCount }, (_, index) => {
        const ratio = index / (timeTickCount - 1);
        return { x: left + plotWidth * ratio, value: startTime + duration * ratio };
      });

  return (
    <div className="enhanced-timeline-analysis">
      <div className="timeline-chart-toolbar enhanced">
        <div className="timeline-metric-control" aria-label="Timeline metric">
          {timelineMetrics.map((item) => (
            <button type="button" key={item.id} className={metric === item.id ? 'active' : ''} aria-pressed={metric === item.id} onClick={() => setMetric(item.id)}>{item.label}</button>
          ))}
        </div>
        <div className="timeline-chart-reading"><span>Latest {timelineMetricLabel(metric).toLowerCase()}</span><strong>{formatTimelineValue(latest, metric)}</strong></div>
      </div>

      <div className="responsive-timeline-frame" ref={frameRef}>
        <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${timelineMetricLabel(metric)} over the selected monitoring window`}>
          {yTicks.map((tick, index) => {
            const y = top + (index / (yTicks.length - 1)) * plotHeight;
            return <g key={`${tick}-${index}`}><line x1={left} x2={width - right} y1={y} y2={y} className={`timeline-grid-line ${index === yTicks.length - 1 ? 'major' : ''}`} /><text x={left - 10} y={y + 3.5} textAnchor="end" className="timeline-axis-label">{formatTimelineAxis(tick, metric)}</text></g>;
          })}

          {timeTicks.map((tick, index) => (
            <g key={`grid-${tick.value}-${index}`}>
              {index > 0 && index < timeTicks.length - 1 && <line x1={tick.x} x2={tick.x} y1={top} y2={stateTop + stateHeight} className="timeline-time-grid" />}
              <text x={tick.x} y={timeY} textAnchor={index === 0 ? 'start' : index === timeTicks.length - 1 ? 'end' : 'middle'} className="timeline-time-label">{formatTimelineTime(tick.value, snapshot.window)}</text>
            </g>
          ))}

          <line x1={left} x2={width - right} y1={stateTop + stateHeight / 2} y2={stateTop + stateHeight / 2} className="timeline-state-baseline" />
          {points.filter((point) => point.sample.state === 'unresponsive').map((point, index) => <line key={`outage-${index}`} x1={point.x} x2={point.x} y1={top} y2={stateTop + stateHeight} className="timeline-outage-marker" />)}
          {path && <path d={path} className="timeline-series-line" />}

          {points.map((point, index) => (
            <g key={`${point.sample.timestamp}-${index}`} className="timeline-point-group">
              <title>{timelineTooltip(point.sample)}</title>
              <rect x={point.x - stateBlockWidth / 2} y={stateTop} width={stateBlockWidth} height={stateHeight} rx={Math.min(4, stateBlockWidth / 2)} className={`timeline-state-block ${point.sample.state}`} />
              {point.y != null && <circle cx={point.x} cy={point.y} r="4.8" className={`timeline-point ${point.sample.state}`} />}
              {point.y != null && point.sample.diagnosticLoad && <circle cx={point.x} cy={point.y} r="8" className="timeline-diagnostic-ring" />}
            </g>
          ))}
        </svg>
      </div>

      {sparse && <div className="responsive-timeline-note">Sparse baseline · sample spacing expanded for readability</div>}
      <div className="timeline-chart-legend" aria-label="Timeline state legend">
        <span className="responsive"><i />Responsive</span><span className="laggy"><i />Laggy</span><span className="unresponsive"><i />Unresponsive</span><span className="diagnostic"><i />Diagnostic load</span>
      </div>
    </div>
  );
}

function alertSummary(snapshot: MonitorSnapshot): string {
  if (!snapshot.alerts.length) return 'No recorded monitoring events.';
  if (snapshot.unreadAlertCount) return `${snapshot.unreadAlertCount} unread · ${snapshot.alerts.length} recorded`;
  return `${snapshot.alerts.length} recorded · all read`;
}

function timelineMetricValue(sample: TimelineSample, metric: TimelineMetric): number | null {
  if (metric === 'latency') return sample.latencyMs ?? null;
  if (metric === 'jitter') return sample.jitterMs ?? null;
  return Number.isFinite(sample.packetLossPercent) ? sample.packetLossPercent : null;
}

function latestMetricValue(samples: TimelineSample[], metric: TimelineMetric): number | null {
  for (let index = samples.length - 1; index >= 0; index--) {
    const value = timelineMetricValue(samples[index], metric);
    if (value != null && Number.isFinite(value)) return value;
  }
  return null;
}

function timelineScaleMax(metric: TimelineMetric, values: number[]): number {
  const maximum = values.length ? Math.max(...values) : 0;
  if (metric === 'loss') {
    const step = maximum <= 5 ? 1 : maximum <= 25 ? 5 : 10;
    return Math.min(100, Math.max(1, Math.ceil(maximum / step) * step));
  }
  if (metric === 'jitter') {
    const step = maximum <= 50 ? 10 : maximum <= 150 ? 25 : 50;
    return Math.max(20, Math.ceil(maximum / step) * step);
  }
  const step = maximum <= 100 ? 25 : maximum <= 300 ? 50 : 100;
  return Math.max(50, Math.ceil(maximum / step) * step);
}

function sparseTicks(samples: TimelineSample[], timestamps: number[], left: number, plotWidth: number) {
  if (samples.length === 1) {
    const value = Number.isFinite(timestamps[0]) ? timestamps[0] : Date.now();
    return [{ x: left + plotWidth, value }];
  }
  const indices = [...new Set([0, Math.floor((samples.length - 1) / 2), samples.length - 1])];
  return indices.map((index) => ({ x: left + (index / Math.max(1, samples.length - 1)) * plotWidth, value: Number.isFinite(timestamps[index]) ? timestamps[index] : Date.now() }));
}

function timelineMetricLabel(metric: TimelineMetric): string {
  return metric === 'jitter' ? 'Jitter' : metric === 'loss' ? 'Packet loss' : 'Latency';
}

function formatTimelineValue(value: number | null, metric: TimelineMetric): string {
  if (value == null) return 'Not measured';
  return metric === 'loss' ? `${formatNumber(value)}%` : `${formatNumber(value)} ms`;
}

function formatTimelineAxis(value: number, metric: TimelineMetric): string {
  return metric === 'loss' ? `${formatNumber(value)}%` : `${formatNumber(value)} ms`;
}

function formatTimelineTime(value: number, window: MonitorWindow): string {
  const date = new Date(value);
  if (window === '7d' || window === '24h') return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric' });
  return date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function timelineTooltip(sample: TimelineSample): string {
  const parts = [formatAlertTime(sample.timestamp), labelize(sample.state)];
  if (sample.latencyMs != null) parts.push(`${formatNumber(sample.latencyMs)} ms latency`);
  if (sample.jitterMs != null) parts.push(`${formatNumber(sample.jitterMs)} ms jitter`);
  parts.push(`${formatNumber(sample.packetLossPercent)}% loss`);
  if (sample.diagnosticLoad) parts.push('controlled diagnostic load');
  return parts.join(' · ');
}

function formatAlertTime(value: string): string {
  return new Date(value).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value);
}

function labelize(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/-/g, ' ');
}

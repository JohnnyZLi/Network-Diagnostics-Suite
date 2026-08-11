import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import './monitor.css';

export type MonitorWindow = '1m' | '5m' | '1h' | '24h' | '7d';
type TimelineMetric = 'latency' | 'jitter' | 'loss';

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
  diagnosticLoad?: boolean;
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

type ActiveDiagnostic = { profileName: string; phase: string };
type LatestDiagnostic = {
  profileName: string;
  generatedAt: string;
  outcome: string;
  label: string;
  verdict: string;
  summary: string;
  nextAction: string;
};

const windows: Array<{ id: MonitorWindow; label: string }> = [
  { id: '1m', label: '1 min' },
  { id: '5m', label: '5 min' },
  { id: '1h', label: '1 hr' },
  { id: '24h', label: '24 hr' },
  { id: '7d', label: '7 days' },
];

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

export function ContinuousDiagnostics({
  snapshot,
  loading,
  error,
  activeDiagnostic,
  latestDiagnostic,
  onUpdate,
  onError,
  onRunRecommended,
  onOpenLatestReport,
  onMeasureCapacity,
  onMeasurePeakCapacity,
}: {
  snapshot: MonitorSnapshot | null;
  loading: boolean;
  error: string | null;
  activeDiagnostic: ActiveDiagnostic | null;
  latestDiagnostic: LatestDiagnostic | null;
  onUpdate: (snapshot: MonitorSnapshot) => void;
  onError: (message: string | null) => void;
  onRunRecommended: () => void;
  onOpenLatestReport: () => void;
  onMeasureCapacity: () => void;
  onMeasurePeakCapacity: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [timelineMetric, setTimelineMetric] = useState<TimelineMetric>('latency');

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

  const range = snapshot ? timelineSummary(snapshot.timeline) : null;
  const recommendation = snapshot ? healthRecommendation(snapshot) : null;
  const latestTimelineValue = snapshot ? latestMetricValue(snapshot.timeline, timelineMetric) : null;

  return (
    <section id="live-network-health" className="workbench-section live-health-section" aria-labelledby="live-health-title">
      <div className="workbench-section-header live-health-header">
        <div>
          <h1 id="live-health-title">Live Network Health</h1>
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
          {activeDiagnostic && (
            <div className="diagnostic-load-notice" role="status">
              <span className="pulse" aria-hidden="true" />
              <div><strong>{activeDiagnostic.profileName} is generating controlled network load.</strong><p>{activeDiagnostic.phase}. Samples collected during test traffic are treated separately from normal passive health.</p></div>
            </div>
          )}

          <div className="live-health-dashboard">
          <div className="live-health-overview">
          <div className="live-health-hero">
            <div className={`health-score-orb ${snapshot.band}`} aria-label={snapshot.score == null ? 'Building network score' : `Network health score ${snapshot.score}`}>
              <strong>{snapshot.score ?? '—'}</strong>
              <span>{snapshot.score == null ? 'Building baseline' : snapshot.status}</span>
              <small>Passive network health</small>
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
              {recommendation && (
                <div className={`health-recommendation ${recommendation.kind}`}>
                  <span>Recommended action</span>
                  <strong>{recommendation.title}</strong>
                  <p>{recommendation.detail}</p>
                  {recommendation.action === 'diagnostic' && <button type="button" className="inline-action" onClick={onRunRecommended}>Prepare Connection Check</button>}
                  {recommendation.action === 'capacity' && <button type="button" className="inline-action" onClick={onMeasureCapacity}>Measure capacity</button>}
                </div>
              )}
            </div>
          </div>

          {latestDiagnostic && (
            <div className={`latest-diagnostic-summary ${latestDiagnostic.outcome}`}>
              <span>Latest diagnostic · {latestDiagnostic.profileName} · {relativeTime(latestDiagnostic.generatedAt)}</span>
              <div>
                <div><strong>{latestDiagnostic.verdict}</strong><p>{latestDiagnostic.summary}</p></div>
                <button type="button" className="secondary-action" onClick={onOpenLatestReport}>View report</button>
              </div>
            </div>
          )}

          </div>

          <div className="live-timeline-section">
            <div className="live-timeline-heading">
              <div><strong>Connection timeline</strong><span>{snapshot.timeline.length} samples · latency, jitter, loss & response state</span></div>
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
            {range && (
              <div className="timeline-range-summary">
                <strong>{range.samples} samples</strong>
                <span>{range.degraded} degraded</span>
                <span>{range.outages} outages</span>
                <span>{range.worstLatency}</span>
                <span>{range.worstJitter}</span>
                <span>{range.worstLoss}</span>
                {range.diagnosticLoad > 0 && <span>{range.diagnosticLoad} under diagnostic load</span>}
              </div>
            )}
            {snapshot.timeline.length > 0 ? (
              <div className="timeline-analysis">
                <div className="timeline-chart-toolbar">
                  <div className="timeline-metric-control" aria-label="Timeline metric">
                    {timelineMetrics.map((item) => (
                      <button
                        type="button"
                        key={item.id}
                        className={timelineMetric === item.id ? 'active' : ''}
                        aria-pressed={timelineMetric === item.id}
                        onClick={() => setTimelineMetric(item.id)}
                      >{item.label}</button>
                    ))}
                  </div>
                  <div className="timeline-chart-reading">
                    <span>Latest {timelineMetricLabel(timelineMetric).toLowerCase()}</span>
                    <strong>{formatTimelineValue(latestTimelineValue, timelineMetric)}</strong>
                  </div>
                </div>
                <TimelineChart samples={snapshot.timeline} window={snapshot.window} metric={timelineMetric} />
                <div className="timeline-chart-legend" aria-label="Timeline state legend">
                  <span className="responsive"><i />Responsive</span>
                  <span className="laggy"><i />Laggy</span>
                  <span className="unresponsive"><i />Unresponsive</span>
                  <span className="diagnostic"><i />Diagnostic load</span>
                </div>
              </div>
            ) : <p className="monitor-muted">The first heartbeat sample will appear after the native monitor reaches its endpoint.</p>}
          </div>
          </div>

          <div className="monitor-components live-health-components">
            <MonitorComponentCard component={snapshot.responsiveness} />
            <MonitorComponentCard component={snapshot.reliability} />
            <CapacityCard component={snapshot.speed} onAction={onMeasureCapacity} onPeakAction={onMeasurePeakCapacity} />
          </div>
        </>
      ) : (
        <div className="workbench-loading monitor-error"><strong>Live network health unavailable</strong><p>{error}</p></div>
      )}
    </section>
  );
}

function TimelineChart({ samples, window, metric }: { samples: MonitorTimelineSample[]; window: MonitorWindow; metric: TimelineMetric }) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ width: 1000, height: 230 });

  useEffect(() => {
    const svg = svgRef.current;
    if (!svg) return;
    const update = () => setSize({
      width: Math.max(560, Math.round(svg.clientWidth || 1000)),
      height: Math.max(190, Math.round(svg.clientHeight || 230)),
    });
    update();
    if (typeof ResizeObserver === 'undefined') {
      globalThis.addEventListener('resize', update);
      return () => globalThis.removeEventListener('resize', update);
    }
    const observer = new ResizeObserver(update);
    observer.observe(svg);
    return () => observer.disconnect();
  }, []);

  const { width, height } = size;
  const left = width < 760 ? 52 : 58;
  const right = 18;
  const top = 18;
  const plotBottom = height - 76;
  const stateTop = height - 52;
  const stateHeight = 13;
  const timeY = height - 14;
  const plotWidth = width - left - right;
  const plotHeight = plotBottom - top;
  const timestamps = samples.map((sample) => Date.parse(sample.timestamp));
  const validTimes = timestamps.filter(Number.isFinite);
  const latestTime = validTimes.length > 0 ? Math.max(...validTimes) : Date.now();
  const duration = windowDurationMs[window];
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

  const stateBlockWidth = samples.length <= 1
    ? 14
    : Math.max(5, Math.min(20, (plotWidth / Math.max(1, samples.length)) * .58));
  const yTicks = [scaleMax, scaleMax * .67, scaleMax * .33, 0];
  const timeTickCount = width >= 1200 ? 5 : 3;
  const timeTicks = sparse
    ? sparseTimeTicks(samples, timestamps, left, plotWidth)
    : Array.from({ length: timeTickCount }, (_, index) => {
        const ratio = index / (timeTickCount - 1);
        return { x: left + plotWidth * ratio, value: startTime + duration * ratio };
      });

  return (
    <div className={`timeline-chart-frame${sparse ? ' sparse' : ''}`}>
      <svg ref={svgRef} viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${timelineMetricLabel(metric)} over the selected monitoring window`}>
        {yTicks.map((tick, index) => {
          const y = top + (index / (yTicks.length - 1)) * plotHeight;
          return (
            <g key={`${tick}-${index}`}>
              <line x1={left} x2={width - right} y1={y} y2={y} className={`timeline-grid-line ${index === yTicks.length - 1 ? 'major' : ''}`} />
              <text x={left - 10} y={y + 3.5} textAnchor="end" className="timeline-axis-label">{formatTimelineAxis(tick, metric)}</text>
            </g>
          );
        })}

        {timeTicks.map((tick, index) => (
          <g key={`time-${tick.value}-${index}`}>
            {index > 0 && index < timeTicks.length - 1 && <line x1={tick.x} x2={tick.x} y1={top} y2={stateTop + stateHeight} className="timeline-time-grid" />}
            <text
              x={tick.x}
              y={timeY}
              textAnchor={index === 0 ? 'start' : index === timeTicks.length - 1 ? 'end' : 'middle'}
              className="timeline-time-label"
            >{formatTimelineTime(tick.value, window)}</text>
          </g>
        ))}

        <line x1={left} x2={width - right} y1={stateTop + stateHeight / 2} y2={stateTop + stateHeight / 2} className="timeline-state-baseline" />

        {points.filter((point) => point.sample.state === 'unresponsive').map((point, index) => (
          <line key={`outage-${index}`} x1={point.x} x2={point.x} y1={top} y2={stateTop + stateHeight} className="timeline-outage-marker" />
        ))}

        {path && <path d={path} className="timeline-series-line" />}

        {points.map((point, index) => (
          <g key={`${point.sample.timestamp}-${index}`} className="timeline-point-group">
            <title>{timelineTooltip(point.sample)}</title>
            <rect
              x={point.x - stateBlockWidth / 2}
              y={stateTop}
              width={stateBlockWidth}
              height={stateHeight}
              rx={Math.min(4, stateBlockWidth / 2)}
              className={`timeline-state-block ${point.sample.state}`}
            />
            {point.y != null && <circle cx={point.x} cy={point.y} r="4.8" className={`timeline-point ${point.sample.state}`} />}
            {point.y != null && point.sample.diagnosticLoad && <circle cx={point.x} cy={point.y} r="8" className="timeline-diagnostic-ring" />}
          </g>
        ))}
      </svg>
      {sparse && <span className="timeline-sparse-note">Sparse baseline · sample spacing expanded for readability</span>}
    </div>
  );
}

function sparseTimeTicks(samples: MonitorTimelineSample[], timestamps: number[], left: number, plotWidth: number) {
  if (samples.length === 1) {
    const value = Number.isFinite(timestamps[0]) ? timestamps[0] : Date.now();
    return [{ x: left + plotWidth, value }];
  }
  const indices = [...new Set([0, Math.floor((samples.length - 1) / 2), samples.length - 1])];
  return indices.map((index) => ({
    x: left + (index / Math.max(1, samples.length - 1)) * plotWidth,
    value: Number.isFinite(timestamps[index]) ? timestamps[index] : Date.now(),
  }));
}

function timelineMetricValue(sample: MonitorTimelineSample, metric: TimelineMetric): number | null {
  if (metric === 'latency') return sample.latencyMs ?? null;
  if (metric === 'jitter') return sample.jitterMs ?? null;
  return Number.isFinite(sample.packetLossPercent) ? sample.packetLossPercent : null;
}

function latestMetricValue(samples: MonitorTimelineSample[], metric: TimelineMetric): number | null {
  for (let index = samples.length - 1; index >= 0; index--) {
    const value = timelineMetricValue(samples[index], metric);
    if (value != null && Number.isFinite(value)) return value;
  }
  return null;
}

function timelineScaleMax(metric: TimelineMetric, values: number[]): number {
  const maximum = values.length > 0 ? Math.max(...values) : 0;
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

function timelineMetricLabel(metric: TimelineMetric): string {
  if (metric === 'jitter') return 'Jitter';
  if (metric === 'loss') return 'Packet loss';
  return 'Latency';
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
  if (window === '7d' || window === '24h') {
    return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric' });
  }
  return date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function timelineTooltip(sample: MonitorTimelineSample): string {
  const parts = [formatTime(sample.timestamp), labelize(sample.state)];
  if (sample.latencyMs != null) parts.push(`${formatNumber(sample.latencyMs)} ms latency`);
  if (sample.jitterMs != null) parts.push(`${formatNumber(sample.jitterMs)} ms jitter`);
  parts.push(`${formatNumber(sample.packetLossPercent)}% loss`);
  if (sample.diagnosticLoad) parts.push('controlled diagnostic load');
  return parts.join(' · ');
}

function LiveMetric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function MonitorComponentCard({ component }: { component: MonitorComponent }) {
  const headlineMetric = component.metrics[0];
  return (
    <article className="monitor-component-card">
      <div className="monitor-component-top"><div><span>{component.title}</span><strong>{component.status}</strong></div><b>{component.score ?? '—'}</b></div>
      {headlineMetric && <div className="component-headline-metric"><strong>{headlineMetric.value}</strong><span>{headlineMetric.label}</span></div>}
      <p>{component.summary}</p>
      {component.metrics.length > 1 && (
        <div className="monitor-component-metrics">
          {component.metrics.slice(1, 4).map((metric) => <div key={metric.label}><span>{metric.label}</span><strong>{metric.value}</strong></div>)}
        </div>
      )}
    </article>
  );
}

function CapacityCard({ component, onAction, onPeakAction }: { component: MonitorComponent; onAction: () => void; onPeakAction: () => void }) {
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
        <p>Run a lightweight content measurement to establish a download and upload baseline. Passive monitoring does not continuously load the connection.</p>
      )}
      <div className="capacity-actions"><button type="button" className="monitor-card-action" onClick={onAction}>{measured ? 'Measure again' : 'Measure capacity'}</button><button type="button" className="monitor-card-action secondary" onClick={onPeakAction}>Peak capacity</button></div>
    </article>
  );
}

function healthRecommendation(snapshot: MonitorSnapshot): { kind: string; title: string; detail: string; action: 'diagnostic' | 'capacity' } | null {
  if (!snapshot.running) return null;
  if (snapshot.band === 'fair' || snapshot.band === 'degraded' || snapshot.band === 'poor') {
    const driver = weakerComponent(snapshot);
    return {
      kind: 'attention',
      title: driver ? `${driver.title} is the weakest part of current health.` : 'Current health needs investigation.',
      detail: driver?.summary ?? 'Run a focused Connection Check for measurements passive monitoring cannot collect.',
      action: 'diagnostic',
    };
  }
  if (snapshot.speed.score == null) {
    return {
      kind: 'capacity',
      title: 'Capacity has not been measured yet.',
      detail: 'A lightweight content measurement adds download and upload context without continuously loading the connection.',
      action: 'capacity',
    };
  }
  return null;
}

function weakerComponent(snapshot: MonitorSnapshot): MonitorComponent | null {
  return [snapshot.responsiveness, snapshot.reliability]
    .filter((component) => component.score != null)
    .sort((left, right) => (left.score ?? 101) - (right.score ?? 101))[0] ?? null;
}

function timelineSummary(samples: MonitorTimelineSample[]) {
  if (samples.length === 0) return null;
  const degraded = samples.filter((sample) => sample.state === 'laggy').length;
  const outages = samples.filter((sample) => sample.state === 'unresponsive').length;
  const diagnosticLoad = samples.filter((sample) => sample.diagnosticLoad).length;
  const latencies = samples.flatMap((sample) => sample.latencyMs == null ? [] : [sample.latencyMs]);
  const jitters = samples.flatMap((sample) => sample.jitterMs == null ? [] : [sample.jitterMs]);
  const losses = samples.map((sample) => sample.packetLossPercent).filter(Number.isFinite);
  const worstLatency = latencies.length > 0 ? Math.max(...latencies) : null;
  const worstJitter = jitters.length > 0 ? Math.max(...jitters) : null;
  const worstLoss = losses.length > 0 ? Math.max(...losses) : null;
  return {
    samples: samples.length,
    degraded,
    outages,
    diagnosticLoad,
    worstLatency: worstLatency == null ? 'No latency sample' : `${formatNumber(worstLatency)} ms max latency`,
    worstJitter: worstJitter == null ? 'No jitter sample' : `${formatNumber(worstJitter)} ms max jitter`,
    worstLoss: worstLoss == null ? 'No loss sample' : `${formatNumber(worstLoss)}% max loss`,
  };
}

function healthHeadline(snapshot: MonitorSnapshot): string {
  if (snapshot.score == null) return 'Building a baseline for this connection';
  switch (snapshot.band) {
    case 'excellent': return 'Network health is within normal range';
    case 'good': return 'Network health is mostly within range';
    case 'fair': return 'Network health is inconsistent';
    case 'degraded': return 'Network degradation detected';
    case 'poor': return 'Network problems detected';
    default: return snapshot.status;
  }
}

function componentMetric(component: MonitorComponent, label: string): string {
  return component.metrics.find((metric) => metric.label.toLowerCase() === label.toLowerCase())?.value ?? '—';
}

function latestLoss(snapshot: MonitorSnapshot): string {
  const sample = [...snapshot.timeline].reverse().find((item) => !item.diagnosticLoad);
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

function relativeTime(value: string): string {
  const elapsed = Date.now() - new Date(value).getTime();
  if (elapsed < 60_000) return 'just now';
  if (elapsed < 3_600_000) return `${Math.max(1, Math.round(elapsed / 60_000))} min ago`;
  if (elapsed < 86_400_000) return `${Math.max(1, Math.round(elapsed / 3_600_000))} hr ago`;
  return new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
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

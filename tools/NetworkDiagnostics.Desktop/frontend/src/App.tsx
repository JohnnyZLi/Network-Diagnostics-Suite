import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import { HistoryPanel, type SavedReportSummary } from './HistoryPanel';
import { MonitorPanel, type MonitorSnapshot } from './MonitorPanel';
import { SettingsMenu, type AppearanceMode } from './SettingsMenu';

type TransferMethod = 'compare' | 'single' | 'aggregate';
type DiagnosticProfile = 'connection-check' | 'quick' | 'full' | 'stress';

type HostInfo = {
  product: string;
  host: string;
  version?: string | null;
  platform: string;
  architecture: string;
  appearance: AppearanceMode;
  monitor?: MonitorSnapshot | null;
};

type AppearanceSettings = {
  appearance: AppearanceMode;
};

type DiagnosticPlan = {
  profile: DiagnosticProfile;
  profileName: string;
  method: TransferMethod;
  transferCapBytes: number;
  downloadStages: number;
  uploadStages: number;
};

type RunAccepted = {
  runId: string;
  profile: DiagnosticProfile;
  method: TransferMethod;
  transferCapBytes: number;
};

type DiagnosticProgress = {
  runId: string;
  phase: string;
  message: string;
  fraction: number;
  liveMbps?: number | null;
  liveLatencyMs?: number | null;
  bytesTransferred: number;
};

type DiagnosticResult = {
  runId: string;
  reportId: string;
  generatedAt: string;
  profile: DiagnosticProfile;
  method: TransferMethod;
  latencyMs?: number | null;
  requestLossPercent?: number | null;
  downloadMbps?: number | null;
  uploadMbps?: number | null;
  dataUsedBytes?: number | null;
  savedLocally?: boolean;
  storageError?: string | null;
  storedReport?: SavedReportSummary | null;
};

type DiagnosticFailure = {
  runId: string;
  message: string;
  errorType: string;
};

type LiveMetrics = {
  latencyMs: number | null;
  downloadMbps: number | null;
  uploadMbps: number | null;
};

type ProfileOption = {
  id: DiagnosticProfile;
  label: string;
  title: string;
  description: string;
  idleCopy: string;
};

const emptyLiveMetrics: LiveMetrics = {
  latencyMs: null,
  downloadMbps: null,
  uploadMbps: null,
};

const profiles: ProfileOption[] = [
  {
    id: 'connection-check',
    label: 'Connection',
    title: 'Connection Check',
    description: 'A fast baseline for responsiveness, loss, and real download and upload performance.',
    idleCopy: 'Connection Check has the lowest transfer ceiling and does not collect local interface identifiers.',
  },
  {
    id: 'quick',
    label: 'Quick',
    title: 'Quick Test',
    description: 'A short native test for a broader throughput snapshot without the duration of a full run.',
    idleCopy: 'Quick increases the measurement budget while keeping the run short enough for routine checks.',
  },
  {
    id: 'full',
    label: 'Full',
    title: 'Full Test',
    description: 'The standard native profile for a more complete view of latency, loss, download, and upload behavior.',
    idleCopy: 'Full uses the standard transfer budget and is the default choice when you want a representative report.',
  },
  {
    id: 'stress',
    label: 'Stress',
    title: 'Stress Test',
    description: 'A heavier native run intended to expose sustained-load behavior and less obvious connection limits.',
    idleCopy: 'Stress uses the largest transfer budget. Run it when the extra traffic and duration are intentional.',
  },
];

const methods: Array<{ id: TransferMethod; label: string; detail: string }> = [
  { id: 'compare', label: 'Compare', detail: 'Single + aggregate' },
  { id: 'single', label: 'Single', detail: 'One transfer flow' },
  { id: 'aggregate', label: 'Aggregate', detail: 'Parallel flows' },
];

function App() {
  const [host, setHost] = useState<HostInfo | null>(null);
  const [appearance, setAppearance] = useState<AppearanceMode>('system');
  const [profile, setProfile] = useState<DiagnosticProfile>('connection-check');
  const [method, setMethod] = useState<TransferMethod>('compare');
  const [plan, setPlan] = useState<DiagnosticPlan | null>(null);
  const [progress, setProgress] = useState<DiagnosticProgress | null>(null);
  const [progressRatio, setProgressRatio] = useState(0);
  const [liveMetrics, setLiveMetrics] = useState<LiveMetrics>(emptyLiveMetrics);
  const [measuredBytes, setMeasuredBytes] = useState(0);
  const [result, setResult] = useState<DiagnosticResult | null>(null);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [reports, setReports] = useState<SavedReportSummary[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [monitorOpen, setMonitorOpen] = useState(false);
  const [monitorSnapshot, setMonitorSnapshot] = useState<MonitorSnapshot | null>(null);
  const [monitorLoading, setMonitorLoading] = useState(false);
  const [monitorError, setMonitorError] = useState<string | null>(null);
  const activeRunId = useRef<string | null>(null);
  const activeMethod = useRef<TransferMethod>('compare');
  const activeProfile = useRef<DiagnosticProfile>('connection-check');
  const highestProgress = useRef(0);
  const stageBytes = useRef(new Map<string, number>());
  const appearanceRequest = useRef(0);

  const selectedProfile = profiles.find((item) => item.id === profile) ?? profiles[0];
  const displayedProfile = result
    ? profiles.find((item) => item.id === result.profile) ?? selectedProfile
    : selectedProfile;

  useEffect(() => {
    document.documentElement.dataset.theme = appearance;
  }, [appearance]);

  useEffect(() => {
    if (!desktopBridge.available) return;
    void desktopBridge.request<HostInfo>('app.ready')
      .then((info) => {
        setHost(info);
        setAppearance(info.appearance || 'system');
        if (info.monitor) setMonitorSnapshot(info.monitor);
      })
      .catch((value: Error) => setError(value.message));
  }, []);

  useEffect(() => {
    if (!desktopBridge.available) return;
    setPlan(null);
    void desktopBridge.request<DiagnosticPlan>('diagnostic.describePlan', {
      profile,
      method,
    }).then(setPlan).catch((value: Error) => setError(value.message));
  }, [profile, method]);

  useEffect(() => {
    const removeProgress = desktopBridge.on<DiagnosticProgress>('diagnostic.progress', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;

      setProgress(next);
      const inferred = overallProgress(next, activeMethod.current);
      highestProgress.current = Math.max(highestProgress.current, inferred);
      setProgressRatio(highestProgress.current);

      setLiveMetrics((current) => ({
        latencyMs: next.phase === 'idle' && next.liveLatencyMs != null ? next.liveLatencyMs : current.latencyMs,
        downloadMbps: next.phase === 'download' && next.liveMbps != null ? next.liveMbps : current.downloadMbps,
        uploadMbps: next.phase === 'upload' && next.liveMbps != null ? next.liveMbps : current.uploadMbps,
      }));

      if ((next.phase === 'download' || next.phase === 'upload') && next.bytesTransferred > 0) {
        const stageKey = `${next.phase}:${next.message}`;
        const prior = stageBytes.current.get(stageKey) ?? 0;
        if (next.bytesTransferred > prior) {
          stageBytes.current.set(stageKey, next.bytesTransferred);
          setMeasuredBytes([...stageBytes.current.values()].reduce((total, value) => total + value, 0));
        }
      }
    });

    const removeCompleted = desktopBridge.on<DiagnosticResult>('diagnostic.completed', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      highestProgress.current = 1;
      setProgressRatio(1);
      setResult(next);
      setMeasuredBytes((current) => next.dataUsedBytes ?? current);
      setProgress((current) => current ? { ...current, fraction: 1, phase: 'complete', message: 'Complete' } : current);
      setRunning(false);

      if (next.storedReport) {
        setReports((current) => [
          next.storedReport!,
          ...current.filter((item) => item.id !== next.storedReport!.id),
        ]);
      }
      if (next.storageError) {
        setError(`Measurement completed, but the report could not be saved: ${next.storageError}`);
      }
    });

    const removeCancelled = desktopBridge.on<{ runId: string }>('diagnostic.cancelled', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      setRunning(false);
      setError(`${profileTitle(activeProfile.current)} was cancelled.`);
    });

    const removeFailed = desktopBridge.on<DiagnosticFailure>('diagnostic.failed', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      setRunning(false);
      setError(next.message);
    });

    const removeMonitorSnapshot = desktopBridge.on<MonitorSnapshot>('monitor.snapshot', (next) => {
      setMonitorSnapshot(next);
      setMonitorLoading(false);
    });

    const removeMonitorError = desktopBridge.on<{ message: string }>('monitor.error', (next) => {
      setMonitorError(next.message);
    });

    return () => {
      removeProgress();
      removeCompleted();
      removeCancelled();
      removeFailed();
      removeMonitorSnapshot();
      removeMonitorError();
    };
  }, []);

  const progressPercent = Math.round(progressRatio * 100);
  const livePrimary = progress?.liveMbps != null
    ? `${formatNumber(progress.liveMbps)} Mbps`
    : progress?.phase === 'idle' && progress.liveLatencyMs != null
      ? `${formatNumber(progress.liveLatencyMs)} ms`
      : running
        ? 'Measuring'
        : result
          ? 'Complete'
          : 'Ready';

  async function loadReports() {
    if (!desktopBridge.available) return;
    setHistoryLoading(true);
    setHistoryError(null);
    try {
      const savedReports = await desktopBridge.request<SavedReportSummary[]>('reports.list');
      setReports(savedReports);
    } catch (value) {
      setHistoryError(value instanceof Error ? value.message : 'Saved runs could not be read.');
    } finally {
      setHistoryLoading(false);
    }
  }

  function openHistory() {
    setMonitorOpen(false);
    setHistoryOpen(true);
    void loadReports();
  }

  async function loadMonitor() {
    if (!desktopBridge.available) return;
    setMonitorLoading(true);
    setMonitorError(null);
    try {
      setMonitorSnapshot(await desktopBridge.request<MonitorSnapshot>('monitor.get'));
    } catch (value) {
      setMonitorError(value instanceof Error ? value.message : 'Network monitor history could not be read.');
    } finally {
      setMonitorLoading(false);
    }
  }

  function openMonitor() {
    setHistoryOpen(false);
    setMonitorOpen(true);
    void loadMonitor();
  }

  async function changeAppearance(next: AppearanceMode) {
    const request = ++appearanceRequest.current;
    const previous = appearance;
    setAppearance(next);
    setError(null);

    try {
      const saved = await desktopBridge.request<AppearanceSettings>('settings.setAppearance', { appearance: next });
      if (request === appearanceRequest.current) setAppearance(saved.appearance);
    } catch (value) {
      if (request === appearanceRequest.current) setAppearance(previous);
      setError(value instanceof Error ? value.message : 'Appearance could not be saved.');
    }
  }

  function selectProfile(next: DiagnosticProfile) {
    if (running || next === profile) return;
    setProfile(next);
    setResult(null);
    setProgress(null);
    setProgressRatio(0);
    setLiveMetrics(emptyLiveMetrics);
    setMeasuredBytes(0);
    setError(null);
  }

  async function runDiagnostic() {
    activeProfile.current = profile;
    activeMethod.current = method;
    highestProgress.current = 0;
    stageBytes.current.clear();
    setError(null);
    setResult(null);
    setLiveMetrics(emptyLiveMetrics);
    setMeasuredBytes(0);
    setProgressRatio(0);
    setProgress({
      runId: '',
      phase: 'starting',
      message: 'Preparing the measurement path…',
      fraction: 0,
      bytesTransferred: 0,
    });
    setRunning(true);

    try {
      const accepted = await desktopBridge.request<RunAccepted>('diagnostic.run', {
        profile,
        method,
      });
      activeRunId.current = accepted.runId;
    } catch (value) {
      setRunning(false);
      setProgress(null);
      setError(value instanceof Error ? value.message : 'The diagnostic could not start.');
    }
  }

  async function cancelDiagnostic() {
    try {
      await desktopBridge.request('diagnostic.cancel');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The diagnostic could not be cancelled.');
    }
  }

  return (
    <div className="app-shell">
      <header className="product-bar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true" />
          <div>
            <strong>Network Diagnostics</strong>
            <span>Desktop</span>
          </div>
        </div>
        <div className="product-actions">
          <div className={`host-state ${host ? 'connected' : ''}`}>
            <span className="status-dot" aria-hidden="true" />
            {host ? `Native engine · ${host.architecture}` : desktopBridge.available ? 'Connecting to engine' : 'Browser preview'}
          </div>
          <button
            type="button"
            className={`monitor-trigger ${monitorOpen ? 'active' : ''}`}
            aria-label="Network monitor"
            aria-expanded={monitorOpen}
            onClick={openMonitor}
            disabled={!desktopBridge.available}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 13h3l2-6 4 11 2-5h5" /></svg>
            <span>Monitor</span>
            {monitorSnapshot?.score != null && <b className="monitor-trigger-score">{monitorSnapshot.score}</b>}
            {(monitorSnapshot?.unreadAlertCount ?? 0) > 0 && <i className="monitor-trigger-alert" aria-label={`${monitorSnapshot!.unreadAlertCount} unread monitor alerts`} />}
          </button>
          <button
            type="button"
            className={`history-trigger ${historyOpen ? 'active' : ''}`}
            aria-label="Saved runs"
            aria-expanded={historyOpen}
            onClick={openHistory}
            disabled={!desktopBridge.available}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 7v5l3 2" />
              <circle cx="12" cy="12" r="8" />
            </svg>
            <span>History</span>
          </button>
          <SettingsMenu
            appearance={appearance}
            onAppearanceChange={(next) => void changeAppearance(next)}
            disabled={!desktopBridge.available}
          />
        </div>
      </header>

      <main>
        <section className="intro-row">
          <div>
            <span className="eyebrow">Diagnostics</span>
            <h1>{selectedProfile.title}</h1>
            <p>{selectedProfile.description}</p>
          </div>
          <div style={{ display: 'grid', gap: 8, justifyItems: 'end' }}>
            <div className="method-control" aria-label="Diagnostic profile">
              {profiles.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className={profile === item.id ? 'active' : ''}
                  onClick={() => selectProfile(item.id)}
                  disabled={running}
                  title={item.description}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <div className="method-control" aria-label="Transfer method">
              {methods.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className={method === item.id ? 'active' : ''}
                  onClick={() => setMethod(item.id)}
                  disabled={running}
                  title={item.detail}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>
        </section>

        <section className="diagnostic-surface">
          <div className="run-panel">
            <div
              className={`progress-orb ${running ? 'running' : ''}`}
              style={{ '--progress': `${progressPercent * 3.6}deg` } as React.CSSProperties}
              aria-label={`${progressPercent}% complete`}
            >
              <div className="progress-orb-inner">
                <span>{livePrimary}</span>
                <small>{running ? `${progressPercent}%` : result ? displayedProfile.title : 'Native test'}</small>
              </div>
            </div>

            <div className="run-copy">
              <div className="run-status-line">
                <span className={running ? 'pulse' : ''} aria-hidden="true" />
                {running ? phaseLabel(progress?.phase, progress?.message) : result ? 'Measurement complete' : 'Ready to measure'}
              </div>
              <h2>{running ? progress?.message || 'Preparing the test…' : result ? `${displayedProfile.title} is ready.` : 'Measure the connection you are on now.'}</h2>
              <p>
                {running
                  ? 'Headline measurements stay visible as the test moves between phases. The final report is assembled locally.'
                  : result
                    ? `Completed ${new Date(result.generatedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}. The headline results are ready below.`
                    : selectedProfile.idleCopy}
              </p>

              <div className="actions">
                {running ? (
                  <button type="button" className="secondary-action" onClick={() => void cancelDiagnostic()}>
                    Cancel test
                  </button>
                ) : (
                  <button
                    type="button"
                    className="primary-action"
                    onClick={() => void runDiagnostic()}
                    disabled={!desktopBridge.available}
                  >
                    {result ? `Run ${selectedProfile.label.toLowerCase()} again` : `Run ${selectedProfile.label.toLowerCase()} test`}
                  </button>
                )}
              </div>
            </div>
          </div>

          <div className="metric-strip" aria-label="Connection metrics">
            <Metric
              label="Latency"
              value={metric(result?.latencyMs ?? liveMetrics.latencyMs, 'ms')}
              detail={running && liveMetrics.latencyMs != null ? 'Measured baseline' : 'Median latency'}
            />
            <Metric
              label="Download"
              value={metric(result?.downloadMbps ?? liveMetrics.downloadMbps, 'Mbps')}
              detail={running && liveMetrics.downloadMbps != null ? 'Latest live sample' : 'Steady throughput'}
            />
            <Metric
              label="Upload"
              value={metric(result?.uploadMbps ?? liveMetrics.uploadMbps, 'Mbps')}
              detail={running && liveMetrics.uploadMbps != null ? 'Latest live sample' : 'Steady throughput'}
            />
            <Metric label="Request loss" value={metric(result?.requestLossPercent, '%')} detail="First-party requests" />
          </div>
        </section>

        <section className="detail-row">
          <div className="plan-card">
            <span className="section-kicker">Current test plan</span>
            <div className="plan-main">
              <strong>{plan?.profileName || selectedProfile.title}</strong>
              <span>{methodLabel(method)}</span>
            </div>
            <div className="plan-facts">
              <span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span>
              <span><b>{plan ? `${plan.downloadStages + plan.uploadStages}` : '—'}</b> transfer stages</span>
              <span><b>Local</b> report assembly</span>
            </div>
          </div>

          <div className="evidence-card">
            <span className="section-kicker">Run evidence</span>
            <div className="evidence-line">
              <span>Profile</span>
              <strong>{result ? displayedProfile.title : selectedProfile.title}</strong>
            </div>
            <div className="evidence-line">
              <span>Phase</span>
              <strong>{running ? phaseLabel(progress?.phase, progress?.message) : result ? 'Complete' : 'Idle'}</strong>
            </div>
            <div className="evidence-line">
              <span>Measured payload</span>
              <strong>{formatBytes(result?.dataUsedBytes ?? measuredBytes)}</strong>
            </div>
            <div className="evidence-line">
              <span>Method</span>
              <strong>{methodLabel(method)}</strong>
            </div>
          </div>
        </section>

        {error && <div className="error-banner" role="alert">{error}</div>}
      </main>

      <HistoryPanel
        open={historyOpen}
        reports={reports}
        loading={historyLoading}
        error={historyError}
        onClose={() => setHistoryOpen(false)}
        onRefresh={() => void loadReports()}
      />

      <MonitorPanel
        open={monitorOpen}
        snapshot={monitorSnapshot}
        loading={monitorLoading}
        error={monitorError}
        onClose={() => setMonitorOpen(false)}
        onUpdate={setMonitorSnapshot}
        onError={setMonitorError}
      />
    </div>
  );
}

function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </div>
  );
}

function overallProgress(progress: DiagnosticProgress, method: TransferMethod): number {
  const fraction = Math.min(1, Math.max(0, progress.fraction || 0));
  const message = progress.message.toLowerCase();

  if (progress.phase === 'complete') return 1;
  if (progress.phase === 'starting') return 0.01;
  if (progress.phase === 'idle') return 0.08 + fraction * 0.17;

  if (progress.phase === 'download') {
    if (method === 'compare') {
      if (message.includes('single')) return 0.25 + fraction * 0.15;
      if (message.includes('aggregate')) return 0.40 + fraction * 0.20;
    }
    return 0.28 + fraction * 0.34;
  }

  if (progress.phase === 'upload') {
    return method === 'compare'
      ? 0.60 + fraction * 0.25
      : 0.62 + fraction * 0.26;
  }

  if (progress.phase === 'diagnostics') {
    if (message.includes('selecting the measurement endpoint')) return 0.03;
    if (message.includes('latency')) return 0.08;
    if (message.includes('single download')) return method === 'compare' ? 0.25 : 0.28;
    if (message.includes('aggregate download')) return method === 'compare' ? 0.40 : 0.28;
    if (message.includes('upload')) return method === 'compare' ? 0.60 : 0.62;
    if (message.includes('finalizing')) return 0.88;
    return 0.90;
  }

  return 0.02;
}

function metric(value: number | null | undefined, unit: string): string {
  return value == null ? '—' : `${formatNumber(value)} ${unit}`;
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: value >= 100 ? 0 : 1,
  }).format(value);
}

function formatBytes(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return value === 0 ? '0 MB' : '—';
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)} GB`;
  return `${(value / 1_000_000).toFixed(value >= 100_000_000 ? 0 : 1)} MB`;
}

function methodLabel(method: TransferMethod): string {
  if (method === 'single') return 'Single flow';
  if (method === 'aggregate') return 'Aggregate flows';
  return 'Single + aggregate';
}

function profileTitle(profile: DiagnosticProfile): string {
  return profiles.find((item) => item.id === profile)?.title ?? 'Diagnostic';
}

function phaseLabel(phase?: string, message?: string): string {
  switch (phase) {
    case 'idle': return 'Baseline latency';
    case 'download': return 'Download measurement';
    case 'upload': return 'Upload measurement';
    case 'diagnostics': {
      const normalized = message?.toLowerCase() ?? '';
      if (normalized.includes('selecting')) return 'Endpoint selection';
      if (normalized.includes('latency')) return 'Latency checks';
      if (normalized.includes('download')) return 'Download checks';
      if (normalized.includes('upload')) return 'Upload checks';
      return 'Network evidence';
    }
    case 'complete': return 'Complete';
    case 'starting': return 'Preparing';
    default: return 'Measuring';
  }
}

export default App;

import { useEffect, useRef, useState } from 'react';
import { AdvancedDiagnostics, type AdvancedRuntimeStatus } from './AdvancedDiagnostics';
import { desktopBridge } from './bridge';
import { CommandPalette, type PaletteCommand } from './CommandPalette';
import { ContinuousDiagnostics, type MonitorSnapshot } from './ContinuousDiagnostics';
import { HistoryPanel, type SavedReportSummary } from './HistoryPanel';
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

type AppearanceSettings = { appearance: AppearanceMode };

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

type DiagnosticFailure = { runId: string; message: string; errorType: string };
type LiveMetrics = { latencyMs: number | null; downloadMbps: number | null; uploadMbps: number | null };
type ProfileOption = { id: DiagnosticProfile; label: string; title: string; description: string; idleCopy: string };

const emptyLiveMetrics: LiveMetrics = { latencyMs: null, downloadMbps: null, uploadMbps: null };
const defaultAdvancedStatus: AdvancedRuntimeStatus = {
  hasOverrides: false,
  summary: 'Default routing · first-party endpoint · identifiers excluded',
  serverRunning: false,
  serverPort: null,
};

const profiles: ProfileOption[] = [
  {
    id: 'connection-check',
    label: 'Connection',
    title: 'Connection Check',
    description: 'A fast baseline for responsiveness, loss, and real download and upload performance.',
    idleCopy: 'The lowest transfer ceiling for a focused baseline before you move into a deeper diagnostic.',
  },
  {
    id: 'quick',
    label: 'Quick',
    title: 'Quick Test',
    description: 'A short native test for a broader throughput snapshot without the duration of a full run.',
    idleCopy: 'A larger measurement budget than Connection Check while remaining short enough for routine investigation.',
  },
  {
    id: 'full',
    label: 'Full',
    title: 'Full Test',
    description: 'The standard native profile for a more complete view of latency, loss, download, upload, and path evidence.',
    idleCopy: 'The representative diagnostic when passive health says something is wrong and you want the complete native evidence set.',
  },
  {
    id: 'stress',
    label: 'Stress',
    title: 'Stress Test',
    description: 'A heavier native run intended to expose sustained-load behavior and less obvious connection limits.',
    idleCopy: 'The largest transfer budget. Use it intentionally when you want sustained-load and capacity behavior.',
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
  const [monitorSnapshot, setMonitorSnapshot] = useState<MonitorSnapshot | null>(null);
  const [monitorLoading, setMonitorLoading] = useState(desktopBridge.available);
  const [monitorError, setMonitorError] = useState<string | null>(null);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [peakConfirm, setPeakConfirm] = useState(false);
  const [speedNotice, setSpeedNotice] = useState<string | null>(null);
  const [advancedStatus, setAdvancedStatus] = useState<AdvancedRuntimeStatus>(defaultAdvancedStatus);
  const [advancedResetRequest, setAdvancedResetRequest] = useState(0);
  const [diagnosticsInView, setDiagnosticsInView] = useState(true);
  const activeRunId = useRef<string | null>(null);
  const activeMethod = useRef<TransferMethod>('compare');
  const activeProfile = useRef<DiagnosticProfile>('connection-check');
  const highestProgress = useRef(0);
  const stageBytes = useRef(new Map<string, number>());
  const appearanceRequest = useRef(0);

  const selectedProfile = profiles.find((item) => item.id === profile) ?? profiles[0];
  const displayedProfile = result ? profiles.find((item) => item.id === result.profile) ?? selectedProfile : selectedProfile;

  useEffect(() => {
    document.documentElement.dataset.theme = appearance;
  }, [appearance]);

  useEffect(() => {
    if (!desktopBridge.available) {
      setMonitorLoading(false);
      return;
    }
    void desktopBridge.request<HostInfo>('app.ready')
      .then((info) => {
        setHost(info);
        setAppearance(info.appearance || 'system');
        if (info.monitor) {
          setMonitorSnapshot(info.monitor);
          setMonitorLoading(false);
        } else {
          void loadMonitor();
        }
      })
      .catch((value: Error) => {
        setError(value.message);
        setMonitorLoading(false);
      });
  }, []);

  useEffect(() => {
    if (!desktopBridge.available) return;
    setPlan(null);
    void desktopBridge.request<DiagnosticPlan>('diagnostic.describePlan', { profile, method })
      .then(setPlan)
      .catch((value: Error) => setError(value.message));
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
        setReports((current) => [next.storedReport!, ...current.filter((item) => item.id !== next.storedReport!.id)]);
      }
      if (next.storageError) setError(`Measurement completed, but the report could not be saved: ${next.storageError}`);
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
    const removeMonitorError = desktopBridge.on<{ message: string }>('monitor.error', (next) => setMonitorError(next.message));

    return () => {
      removeProgress();
      removeCompleted();
      removeCancelled();
      removeFailed();
      removeMonitorSnapshot();
      removeMonitorError();
    };
  }, []);

  useEffect(() => {
    const diagnostics = document.getElementById('run-diagnostics');
    if (!diagnostics || typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(
      ([entry]) => setDiagnosticsInView(entry.isIntersecting),
      { root: null, rootMargin: '-68px 0px -15% 0px', threshold: 0.05 },
    );
    observer.observe(diagnostics);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.altKey) return;
      const modifier = event.metaKey || event.ctrlKey;
      if (!modifier) return;
      const key = event.key.toLowerCase();
      if (key === 'f') {
        event.preventDefault();
        setPaletteOpen(true);
      } else if (key === 'h') {
        event.preventDefault();
        setPaletteOpen(false);
        openHistory();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  const progressPercent = Math.round(progressRatio * 100);

  async function loadReports() {
    if (!desktopBridge.available) return;
    setHistoryLoading(true);
    setHistoryError(null);
    try {
      setReports(await desktopBridge.request<SavedReportSummary[]>('reports.list'));
    } catch (value) {
      setHistoryError(value instanceof Error ? value.message : 'Saved runs could not be read.');
    } finally {
      setHistoryLoading(false);
    }
  }

  function openHistory() {
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
      setMonitorError(value instanceof Error ? value.message : 'Live network health could not be read.');
    } finally {
      setMonitorLoading(false);
    }
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
    setPeakConfirm(false);
    setSpeedNotice(null);
    setError(null);
  }

  async function runDiagnostic(nextProfile: DiagnosticProfile = profile, nextMethod: TransferMethod = method) {
    if (running) return;
    if (nextProfile !== profile) setProfile(nextProfile);
    if (nextMethod !== method) setMethod(nextMethod);
    activeProfile.current = nextProfile;
    activeMethod.current = nextMethod;
    highestProgress.current = 0;
    stageBytes.current.clear();
    setError(null);
    setSpeedNotice(null);
    setPeakConfirm(false);
    setLiveMetrics(emptyLiveMetrics);
    setMeasuredBytes(0);
    setProgressRatio(0);
    setProgress({ runId: '', phase: 'starting', message: 'Preparing the measurement path…', fraction: 0, bytesTransferred: 0 });
    setRunning(true);
    try {
      const accepted = await desktopBridge.request<RunAccepted>('diagnostic.run', { profile: nextProfile, method: nextMethod });
      activeRunId.current = accepted.runId;
    } catch (value) {
      setRunning(false);
      setProgress(null);
      setError(value instanceof Error ? value.message : 'The diagnostic could not start.');
    }
  }

  function scrollToSection(id: string) {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function runContentSpeed() {
    scrollToSection('run-diagnostics');
    void runDiagnostic('connection-check', 'aggregate');
  }

  function reviewPeakSpeed() {
    scrollToSection('speed-checks');
    if (!peakConfirm) {
      setPeakConfirm(true);
      setSpeedNotice('Peak uses Stress + Aggregate and the largest transfer budget. Select Run peak again to continue.');
      return;
    }
    setPeakConfirm(false);
    setSpeedNotice(null);
    void runDiagnostic('stress', 'aggregate');
  }

  async function cancelDiagnostic() {
    try {
      await desktopBridge.request('diagnostic.cancel');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The diagnostic could not be cancelled.');
    }
  }

  const paletteCommands: PaletteCommand[] = [
    {
      id: 'workspace-health',
      title: 'Go to Live Network Health',
      detail: 'Current score, passive measurements, timeline, capacity history, and alerts.',
      keywords: 'monitor health timeline alerts continuous live network',
      priority: 1,
      enabled: true,
      run: () => scrollToSection('live-network-health'),
    },
    {
      id: 'workspace-diagnostics',
      title: 'Go to Run Diagnostics',
      detail: 'Connection Check, Quick, Full, Stress, measurement methods, and speed presets.',
      keywords: 'run diagnostics test connection quick full stress speed',
      priority: 2,
      enabled: true,
      run: () => scrollToSection('run-diagnostics'),
    },
    {
      id: 'workspace-history',
      title: 'Open Saved Runs',
      detail: 'Browse, compare, import, export, and annotate persisted reports.',
      keywords: 'history reports saved runs compare json',
      shortcut: 'Ctrl/⌘ H',
      priority: 3,
      enabled: desktopBridge.available,
      run: openHistory,
    },
    {
      id: 'workspace-advanced',
      title: 'Go to Advanced Diagnostics',
      detail: 'Targeting, interface binding, privacy, native preflight, and LAN tools.',
      keywords: 'advanced endpoint interface lan privacy preflight',
      priority: 4,
      enabled: true,
      run: () => scrollToSection('advanced-diagnostics'),
    },
    {
      id: 'run-selected',
      title: running ? 'Diagnostic is already running' : `Run ${selectedProfile.title}`,
      detail: `${methodLabel(method)} using the current native configuration.`,
      keywords: 'run start diagnostic current selected test',
      priority: 5,
      enabled: desktopBridge.available && !running,
      run: () => { scrollToSection('run-diagnostics'); void runDiagnostic(); },
    },
    {
      id: 'run-content',
      title: 'Run Content Speed Check',
      detail: 'Low-data Connection Check using aggregate transfer flow.',
      keywords: 'content speed aggregate low data bandwidth capacity',
      priority: 6,
      enabled: desktopBridge.available && !running,
      run: runContentSpeed,
    },
    {
      id: 'open-peak',
      title: 'Review Peak Capacity Check',
      detail: 'Jump to the explicit Stress + Aggregate high-data confirmation.',
      keywords: 'peak speed stress aggregate bandwidth capacity',
      priority: 7,
      enabled: desktopBridge.available && !running,
      run: () => scrollToSection('speed-checks'),
    },
    ...(running ? [{
      id: 'cancel-run',
      title: 'Cancel Active Diagnostic',
      detail: 'Stop the native measurement currently in progress.',
      keywords: 'cancel stop abort diagnostic test',
      priority: 0,
      enabled: true,
      run: () => { void cancelDiagnostic(); },
    } satisfies PaletteCommand] : []),
    ...profiles.map((item, index) => ({
      id: `profile-${item.id}`,
      title: `Select ${item.title}`,
      detail: item.description,
      keywords: `profile ${item.id} ${item.label}`,
      priority: 20 + index,
      enabled: !running,
      run: () => { selectProfile(item.id); scrollToSection('run-diagnostics'); },
    } satisfies PaletteCommand)),
    ...methods.map((item, index) => ({
      id: `method-${item.id}`,
      title: `Use ${item.label} Transfer`,
      detail: item.detail,
      keywords: `method transfer flow ${item.id} ${item.detail}`,
      priority: 30 + index,
      enabled: !running,
      run: () => { setMethod(item.id); scrollToSection('run-diagnostics'); },
    } satisfies PaletteCommand)),
  ];

  return (
    <div className="app-shell">
      <header className="product-bar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true" />
          <div><strong>Network Diagnostics</strong><span>Desktop</span></div>
        </div>
        <div className="product-actions">
          <div className={`host-state ${host ? 'connected' : ''}`}>
            <span className="status-dot" aria-hidden="true" />
            {host ? `Native engine · ${host.architecture}` : desktopBridge.available ? 'Connecting to engine' : 'Browser preview'}
          </div>
          {advancedStatus.serverRunning && <span className="lan-runtime-badge"><i />LAN server · :{advancedStatus.serverPort}</span>}
          <button
            type="button"
            className={`history-trigger ${historyOpen ? 'active' : ''}`}
            aria-label="Saved runs"
            aria-expanded={historyOpen}
            onClick={openHistory}
            disabled={!desktopBridge.available}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 7v5l3 2" /><circle cx="12" cy="12" r="8" /></svg>
            <span>History</span>
          </button>
          <SettingsMenu appearance={appearance} onAppearanceChange={(next) => void changeAppearance(next)} disabled={!desktopBridge.available} />
        </div>
      </header>

      <main>
        <ContinuousDiagnostics
          snapshot={monitorSnapshot}
          loading={monitorLoading}
          error={monitorError}
          onUpdate={setMonitorSnapshot}
          onError={setMonitorError}
          onMeasureCapacity={() => scrollToSection('speed-checks')}
        />

        <section id="run-diagnostics" className="workbench-section diagnostics-workbench" aria-labelledby="run-diagnostics-title">
          <div className="workbench-section-header diagnostics-header">
            <div>
              <span className="section-kicker">Run diagnostics</span>
              <h2 id="run-diagnostics-title">Investigate the connection</h2>
              <p>Run a controlled measurement when live health shows a problem or when you need evidence passive monitoring cannot collect.</p>
            </div>
          </div>

          {error && <div className="error-banner" role="alert">{error}</div>}

          <div className="diagnostic-selector-grid">
            <div className="diagnostic-selector-group">
              <span>Diagnostic profile</span>
              <div className="method-control" aria-label="Diagnostic profile">
                {profiles.map((item) => (
                  <button key={item.id} type="button" className={profile === item.id ? 'active' : ''} onClick={() => selectProfile(item.id)} disabled={running}>{item.label}</button>
                ))}
              </div>
              <small>{selectedProfile.description}</small>
            </div>
            <div className="diagnostic-selector-group method-selector-group">
              <span>Measurement method</span>
              <div className="method-control" aria-label="Transfer method">
                {methods.map((item) => (
                  <button key={item.id} type="button" className={method === item.id ? 'active' : ''} onClick={() => setMethod(item.id)} disabled={running}>{item.label}</button>
                ))}
              </div>
              <small>{methods.find((item) => item.id === method)?.detail}</small>
            </div>
          </div>

          {advancedStatus.hasOverrides && (
            <div className="advanced-config-banner">
              <div><span aria-hidden="true">⚙</span><div><strong>Advanced configuration active</strong><small>{advancedStatus.summary}</small></div></div>
              <div><button type="button" onClick={() => scrollToSection('advanced-diagnostics')}>Review</button><button type="button" onClick={() => setAdvancedResetRequest((current) => current + 1)}>Reset</button></div>
            </div>
          )}

          {running ? (
            <div className="diagnostic-run-state running">
              <div className="diagnostic-run-heading">
                <div>
                  <div className="run-status-line"><span className="pulse" aria-hidden="true" />{phaseLabel(progress?.phase, progress?.message)}</div>
                  <h3>{progress?.message || 'Preparing the test…'}</h3>
                  <p>{profileTitle(activeProfile.current)} is running through the native engine while live network monitoring continues independently above.</p>
                </div>
                <button type="button" className="secondary-action" onClick={() => void cancelDiagnostic()}>Cancel test</button>
              </div>
              <div className="diagnostic-progress-row">
                <div className="diagnostic-progress-track" aria-label={`${progressPercent}% complete`}><span style={{ width: `${progressPercent}%` }} /></div>
                <strong>{progressPercent}%</strong>
              </div>
              <div className="metric-strip diagnostic-metrics" aria-label="Active diagnostic metrics">
                <Metric label="Latency" value={metric(liveMetrics.latencyMs, 'ms')} detail="Measured baseline" />
                <Metric label="Download" value={metric(liveMetrics.downloadMbps, 'Mbps')} detail="Latest live sample" />
                <Metric label="Upload" value={metric(liveMetrics.uploadMbps, 'Mbps')} detail="Latest live sample" />
                <Metric label="Payload" value={formatBytes(measuredBytes)} detail="Measured so far" />
              </div>
            </div>
          ) : result ? (
            <div className="diagnostic-run-state result">
              <div className="diagnostic-run-heading">
                <div>
                  <span className="result-label">Latest diagnostic · {new Date(result.generatedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</span>
                  <h3>{displayedProfile.title} complete</h3>
                  <p>The latest successful measurement stays here until another diagnostic completes. Selecting another profile above only prepares the next run.</p>
                </div>
                <div className="diagnostic-result-actions"><button type="button" className="secondary-action" onClick={openHistory}>Open history</button><button type="button" className="primary-action" onClick={() => void runDiagnostic()} disabled={!desktopBridge.available}>Run {selectedProfile.title}</button></div>
              </div>
              <div className="metric-strip diagnostic-metrics" aria-label="Latest diagnostic metrics">
                <Metric label="Latency" value={metric(result.latencyMs, 'ms')} detail="Median latency" />
                <Metric label="Download" value={metric(result.downloadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Upload" value={metric(result.uploadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Request loss" value={metric(result.requestLossPercent, '%')} detail="First-party requests" />
              </div>
            </div>
          ) : (
            <div className="diagnostic-run-state ready">
              <div>
                <span className="result-label">Selected test</span>
                <h3>{selectedProfile.title}</h3>
                <p>{selectedProfile.idleCopy}</p>
                <div className="diagnostic-plan-facts">
                  <span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span>
                  <span><b>{plan ? `${plan.downloadStages + plan.uploadStages}` : '—'}</b> transfer stages</span>
                  <span><b>{methodLabel(method)}</b> measurement method</span>
                </div>
              </div>
              <button type="button" className="primary-action diagnostic-run-button" onClick={() => void runDiagnostic()} disabled={!desktopBridge.available}>Run {selectedProfile.title}</button>
            </div>
          )}

          {(running || result) && (
            <section className="detail-row diagnostic-evidence-row">
              <div className="plan-card">
                <span className="section-kicker">{running ? 'Active test plan' : 'Next test plan'}</span>
                <div className="plan-main"><strong>{plan?.profileName || selectedProfile.title}</strong><span>{methodLabel(method)}</span></div>
                <div className="plan-facts">
                  <span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span>
                  <span><b>{plan ? `${plan.downloadStages + plan.uploadStages}` : '—'}</b> transfer stages</span>
                  <span><b>Local</b> report assembly</span>
                </div>
              </div>

              <div className="evidence-card">
                <span className="section-kicker">Run evidence</span>
                <div className="evidence-line"><span>Profile</span><strong>{running ? profileTitle(activeProfile.current) : displayedProfile.title}</strong></div>
                <div className="evidence-line"><span>Phase</span><strong>{running ? phaseLabel(progress?.phase, progress?.message) : 'Complete'}</strong></div>
                <div className="evidence-line"><span>Measured payload</span><strong>{formatBytes(running ? measuredBytes : result?.dataUsedBytes ?? 0)}</strong></div>
                <div className="evidence-line"><span>Method</span><strong>{methodLabel(running ? activeMethod.current : result?.method ?? method)}</strong></div>
              </div>
            </section>
          )}

          {!running && (
            <div id="speed-checks" className="speed-check-section">
              <div className="speed-check-heading"><div><strong>Speed checks</strong><span>Convenient presets using the same native diagnostic runner</span></div></div>
              {speedNotice && <div className="speed-check-notice" role="status">{speedNotice}</div>}
              <div className="speed-check-grid">
                <button type="button" className="speed-check-card" disabled={!desktopBridge.available} onClick={runContentSpeed}>
                  <span><strong>Content speed</strong><small>Connection Check · Aggregate</small></span>
                  <p>Lower-data capacity estimate representative of normal transfers.</p>
                  <b>Run</b>
                </button>
                <button type="button" className={`speed-check-card ${peakConfirm ? 'confirm' : ''}`} disabled={!desktopBridge.available} onClick={reviewPeakSpeed}>
                  <span><strong>Peak capacity</strong><small>Stress · Aggregate</small></span>
                  <p>High-data measurement intended to approach maximum sustained throughput.</p>
                  <b>{peakConfirm ? 'Run peak' : 'Review'}</b>
                </button>
              </div>
            </div>
          )}
        </section>

        <AdvancedDiagnostics
          profile={profile}
          method={method}
          onStatusChange={setAdvancedStatus}
          resetRequest={advancedResetRequest}
        />
      </main>

      {running && !diagnosticsInView && (
        <button type="button" className="sticky-run-status" onClick={() => scrollToSection('run-diagnostics')}>
          <span className="pulse" aria-hidden="true" />
          <strong>{profileTitle(activeProfile.current)}</strong>
          <small>{phaseLabel(progress?.phase, progress?.message)} · {progressPercent}%</small>
          <b>View</b>
        </button>
      )}

      <HistoryPanel
        open={historyOpen}
        reports={reports}
        loading={historyLoading}
        error={historyError}
        onClose={() => setHistoryOpen(false)}
        onRefresh={() => void loadReports()}
      />

      <CommandPalette open={paletteOpen} commands={paletteCommands} onClose={() => setPaletteOpen(false)} />
    </div>
  );
}

function Metric({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <div className="metric"><span>{label}</span><strong>{value}</strong><small>{detail}</small></div>;
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
  if (progress.phase === 'upload') return method === 'compare' ? 0.60 + fraction * 0.25 : 0.62 + fraction * 0.26;
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

function metric(value: number | null | undefined, unit: string): string { return value == null ? '—' : `${formatNumber(value)} ${unit}`; }
function formatNumber(value: number): string { return new Intl.NumberFormat(undefined, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value); }
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
function profileTitle(profile: DiagnosticProfile): string { return profiles.find((item) => item.id === profile)?.title ?? 'Diagnostic'; }
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
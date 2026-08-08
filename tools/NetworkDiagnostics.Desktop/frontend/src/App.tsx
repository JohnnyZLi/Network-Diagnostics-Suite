import { useEffect, useRef, useState } from 'react';
import { AdvancedDiagnostics, type AdvancedRuntimeStatus } from './AdvancedDiagnostics';
import { desktopBridge } from './bridge';
import { CommandPalette, type PaletteCommand } from './CommandPalette';
import { ContinuousDiagnostics, type MonitorSnapshot } from './ContinuousDiagnostics';
import { HistoryPanel, type SavedReportSummary } from './HistoryPanel';
import { SettingsMenu, type AppearanceMode } from './SettingsMenu';

type TransferMethod = 'compare' | 'single' | 'aggregate';
type DiagnosticProfile = 'connection-check' | 'quick' | 'full' | 'stress';
type WorkbenchSection = 'live-network-health' | 'run-diagnostics' | 'advanced-diagnostics';

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

type ReportPresentation = {
  outcome: string;
  label: string;
  verdict: string;
  summary: string;
  nextAction: string;
  metrics: Array<{ label: string; value: string; detail: string; wasMeasured: boolean }>;
  findings: Array<{ label: string; title: string; summary: string }>;
  technicalEvidence: string[];
};

type SavedReportDetail = {
  report: SavedReportSummary;
  context: string;
  method: string;
  presentation: ReportPresentation;
};

type PendingRun = {
  profile: DiagnosticProfile;
  method: TransferMethod;
  plan: DiagnosticPlan;
};

type DiagnosticFailure = { runId: string; message: string; errorType: string };
type LiveMetrics = { latencyMs: number | null; downloadMbps: number | null; uploadMbps: number | null };
type ProfileOption = {
  id: DiagnosticProfile;
  label: string;
  title: string;
  description: string;
  idleCopy: string;
  duration: string;
  evidence: string;
};

const HIGH_DATA_WARNING_BYTES = 750_000_000;
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
    idleCopy: 'Start here when something feels wrong and you want a focused baseline before deeper investigation.',
    duration: 'Usually under a minute',
    evidence: 'Latency · loss · download · upload',
  },
  {
    id: 'quick',
    label: 'Quick',
    title: 'Quick Test',
    description: 'A short native test for a broader throughput snapshot without the duration of a full run.',
    idleCopy: 'Use this for a broader routine measurement when Connection Check is not enough.',
    duration: 'About 1 minute',
    evidence: 'Latency · loss · throughput · basic path evidence',
  },
  {
    id: 'full',
    label: 'Full',
    title: 'Full Test',
    description: 'The standard native profile for a complete view of performance and network-path evidence.',
    idleCopy: 'Use Full when passive health or a shorter test points to a problem that needs localization.',
    duration: 'About 1–3 minutes',
    evidence: 'Latency · loss · throughput · DNS · route · MTU · IPv4/IPv6 · service reachability',
  },
  {
    id: 'stress',
    label: 'Stress',
    title: 'Stress Test',
    description: 'A heavier native run intended to expose sustained-load behavior and connection limits.',
    idleCopy: 'Use Stress intentionally when you need sustained-load and peak-capacity behavior.',
    duration: 'Several minutes',
    evidence: 'Sustained throughput · loaded latency · path evidence · capacity limits',
  },
];

const methods: Array<{ id: TransferMethod; label: string; detail: string }> = [
  { id: 'compare', label: 'Both', detail: 'Single + aggregate flows' },
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
  const [latestReport, setLatestReport] = useState<SavedReportDetail | null>(null);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyInitialReportId, setHistoryInitialReportId] = useState<string | null>(null);
  const [reports, setReports] = useState<SavedReportSummary[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [monitorSnapshot, setMonitorSnapshot] = useState<MonitorSnapshot | null>(null);
  const [monitorLoading, setMonitorLoading] = useState(desktopBridge.available);
  const [monitorError, setMonitorError] = useState<string | null>(null);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [pendingRun, setPendingRun] = useState<PendingRun | null>(null);
  const [advancedStatus, setAdvancedStatus] = useState<AdvancedRuntimeStatus>(defaultAdvancedStatus);
  const [advancedResetRequest, setAdvancedResetRequest] = useState(0);
  const [diagnosticsInView, setDiagnosticsInView] = useState(true);
  const [activeSection, setActiveSection] = useState<WorkbenchSection>('live-network-health');
  const [historyDocked, setHistoryDocked] = useState(false);
  const activeRunId = useRef<string | null>(null);
  const activeMethod = useRef<TransferMethod>('compare');
  const activeProfile = useRef<DiagnosticProfile>('connection-check');
  const highestProgress = useRef(0);
  const stageBytes = useRef(new Map<string, number>());
  const appearanceRequest = useRef(0);

  const selectedProfile = profiles.find((item) => item.id === profile) ?? profiles[0];
  const displayedProfile = result ? profiles.find((item) => item.id === result.profile) ?? selectedProfile : selectedProfile;
  const shellHealthLabel = !monitorSnapshot
    ? 'Live network health: building baseline'
    : !monitorSnapshot.running
      ? 'Live network health: monitoring paused'
      : `Live network health: ${monitorSnapshot.status}${monitorSnapshot.score == null ? '' : ` · score ${monitorSnapshot.score}`}`;

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
        void loadReports(true);
      })
      .catch((value: Error) => {
        setError(value.message);
        setMonitorLoading(false);
      });
  }, []);

  useEffect(() => {
    if (!desktopBridge.available) return;
    setPlan(null);
    setPendingRun(null);
    void describePlan(profile, method)
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
      setPendingRun(null);
      if (next.storedReport) {
        setReports((current) => [next.storedReport!, ...current.filter((item) => item.id !== next.storedReport!.id)]);
      }
      if (next.reportId) void loadReportDetail(next.reportId).then(setLatestReport).catch(() => undefined);
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
    if (typeof window.matchMedia !== 'function') return;
    const query = window.matchMedia('(min-width: 1500px)');
    const update = () => setHistoryDocked(query.matches);
    update();
    query.addEventListener?.('change', update);
    return () => query.removeEventListener?.('change', update);
  }, []);

  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined') return;
    const root = document.documentElement.dataset.platform === 'macos'
      ? document.querySelector('main')
      : null;
    const sectionIds: WorkbenchSection[] = ['live-network-health', 'run-diagnostics', 'advanced-diagnostics'];
    const visibility = new Map<WorkbenchSection, number>();
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        visibility.set(entry.target.id as WorkbenchSection, entry.isIntersecting ? entry.intersectionRatio : 0);
      }
      const visible = sectionIds
        .map((id) => ({ id, ratio: visibility.get(id) ?? 0 }))
        .sort((left, right) => right.ratio - left.ratio)[0];
      if (visible?.ratio > 0) setActiveSection(visible.id);
    }, { root, rootMargin: '-70px 0px -42% 0px', threshold: [0.05, 0.2, 0.45, 0.7] });
    for (const id of sectionIds) {
      const element = document.getElementById(id);
      if (element) observer.observe(element);
    }
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const diagnostics = document.getElementById('run-diagnostics');
    if (!diagnostics || typeof IntersectionObserver === 'undefined') return;
    const root = document.documentElement.dataset.platform === 'macos'
      ? document.querySelector('main')
      : null;
    const observer = new IntersectionObserver(
      ([entry]) => setDiagnosticsInView(entry.isIntersecting),
      { root, rootMargin: '-68px 0px -15% 0px', threshold: 0.05 },
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
        historyOpen ? setHistoryOpen(false) : openHistory();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [historyOpen]);

  const progressPercent = Math.round(progressRatio * 100);

  async function describePlan(nextProfile: DiagnosticProfile, nextMethod: TransferMethod): Promise<DiagnosticPlan> {
    return desktopBridge.request<DiagnosticPlan>('diagnostic.describePlan', { profile: nextProfile, method: nextMethod });
  }

  async function loadReportDetail(id: string): Promise<SavedReportDetail> {
    return desktopBridge.request<SavedReportDetail>('reports.get', { id });
  }

  async function loadReports(loadLatest = false) {
    if (!desktopBridge.available) return;
    setHistoryLoading(true);
    setHistoryError(null);
    try {
      const nextReports = await desktopBridge.request<SavedReportSummary[]>('reports.list');
      setReports(nextReports);
      if (loadLatest && nextReports.length > 0) {
        try {
          setLatestReport(await loadReportDetail(nextReports[0].id));
        } catch {
          // History remains usable even if a single detail report cannot be opened.
        }
      }
    } catch (value) {
      setHistoryError(value instanceof Error ? value.message : 'Saved runs could not be read.');
    } finally {
      setHistoryLoading(false);
    }
  }

  function openHistory(reportId: string | null = null) {
    setHistoryInitialReportId(reportId);
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
    setPendingRun(null);
    setError(null);
  }

  function selectMethod(next: TransferMethod) {
    if (running || next === method) return;
    setMethod(next);
    setPendingRun(null);
    setError(null);
  }

  async function prepareDiagnostic(nextProfile: DiagnosticProfile = profile, nextMethod: TransferMethod = method) {
    if (running || !desktopBridge.available) return;
    setError(null);
    try {
      const nextPlan = nextProfile === profile && nextMethod === method && plan
        ? plan
        : await describePlan(nextProfile, nextMethod);
      if (nextPlan.transferCapBytes >= HIGH_DATA_WARNING_BYTES) {
        setPendingRun({ profile: nextProfile, method: nextMethod, plan: nextPlan });
        scrollToSection('run-diagnostics');
        return;
      }
      await startDiagnostic(nextProfile, nextMethod);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The diagnostic plan could not be prepared.');
    }
  }

  async function startDiagnostic(nextProfile: DiagnosticProfile, nextMethod: TransferMethod) {
    if (running) return;
    if (nextProfile !== profile) setProfile(nextProfile);
    if (nextMethod !== method) setMethod(nextMethod);
    activeProfile.current = nextProfile;
    activeMethod.current = nextMethod;
    highestProgress.current = 0;
    stageBytes.current.clear();
    setError(null);
    setPendingRun(null);
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

  function scrollToSection(id: WorkbenchSection | string) {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function measureCapacity() {
    scrollToSection('run-diagnostics');
    void prepareDiagnostic('connection-check', 'aggregate');
  }

  function measurePeakCapacity() {
    scrollToSection('run-diagnostics');
    void prepareDiagnostic('stress', 'aggregate');
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
      detail: 'Connection Check, Quick, Full, Stress, and test options.',
      keywords: 'run diagnostics test connection quick full stress',
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
      run: () => openHistory(),
    },
    {
      id: 'workspace-advanced',
      title: 'Go to Advanced Diagnostics',
      detail: 'Test configuration, native preflight, interface binding, privacy, and LAN tools.',
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
      run: () => { scrollToSection('run-diagnostics'); void prepareDiagnostic(); },
    },
    {
      id: 'run-content',
      title: 'Measure Capacity',
      detail: 'Lower-data Connection Check using aggregate transfer flow.',
      keywords: 'content speed aggregate low data bandwidth capacity',
      priority: 6,
      enabled: desktopBridge.available && !running,
      run: measureCapacity,
    },
    {
      id: 'run-peak',
      title: 'Measure Peak Capacity',
      detail: 'Stress + Aggregate with plan-based high-data confirmation.',
      keywords: 'peak speed stress aggregate bandwidth capacity',
      priority: 7,
      enabled: desktopBridge.available && !running,
      run: measurePeakCapacity,
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
      title: `Use ${item.label} measurement`,
      detail: item.detail,
      keywords: `method transfer flow ${item.id} ${item.detail}`,
      priority: 30 + index,
      enabled: !running,
      run: () => { selectMethod(item.id); scrollToSection('run-diagnostics'); },
    } satisfies PaletteCommand)),
  ];

  const latestDiagnostic = latestReport ? {
    profileName: latestReport.report.profileName,
    generatedAt: latestReport.report.generatedAt,
    outcome: latestReport.presentation.outcome,
    label: latestReport.presentation.label,
    verdict: latestReport.presentation.verdict,
    summary: latestReport.presentation.summary,
    nextAction: latestReport.presentation.nextAction,
  } : null;

  const primaryFinding = latestReport?.presentation.findings[0] ?? null;
  const recommendedProfile = recommendationProfile(result, latestReport);

  return (
    <div className={`app-shell ${historyOpen && historyDocked ? 'history-docked' : ''}`}>
      <header className="product-bar">
        <button type="button" className="brand brand-home" onClick={() => scrollToSection('live-network-health')} aria-label="Go to Live Network Health">
          <span className="brand-mark" role="img" aria-label={shellHealthLabel} title={shellHealthLabel} />
          <div><strong>Network Diagnostics</strong><span>Desktop</span></div>
        </button>

        <nav className="workbench-nav" aria-label="Workbench sections">
          <button type="button" className={activeSection === 'live-network-health' ? 'active' : ''} aria-current={activeSection === 'live-network-health' ? 'location' : undefined} onClick={() => scrollToSection('live-network-health')}>Health</button>
          <button type="button" className={activeSection === 'run-diagnostics' ? 'active' : ''} aria-current={activeSection === 'run-diagnostics' ? 'location' : undefined} onClick={() => scrollToSection('run-diagnostics')}>Diagnostics</button>
          <button type="button" className={activeSection === 'advanced-diagnostics' ? 'active' : ''} aria-current={activeSection === 'advanced-diagnostics' ? 'location' : undefined} onClick={() => scrollToSection('advanced-diagnostics')}>Advanced</button>
        </nav>

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
            onClick={() => historyOpen ? setHistoryOpen(false) : openHistory()}
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
          activeDiagnostic={running ? { profileName: profileTitle(activeProfile.current), phase: phaseLabel(progress?.phase, progress?.message) } : null}
          latestDiagnostic={latestDiagnostic}
          onUpdate={setMonitorSnapshot}
          onError={setMonitorError}
          onRunRecommended={() => { scrollToSection('run-diagnostics'); selectProfile('connection-check'); }}
          onOpenLatestReport={() => latestReport && openHistory(latestReport.report.id)}
          onMeasureCapacity={measureCapacity}
          onMeasurePeakCapacity={measurePeakCapacity}
        />

        <section id="run-diagnostics" className="workbench-section diagnostics-workbench" aria-labelledby="run-diagnostics-title">
          <div className="workbench-section-header diagnostics-header">
            <div>
              <h2 id="run-diagnostics-title">RUN DIAGNOSTICS</h2>
              <p>Choose what you need to learn. Test topology and native overrides stay secondary until you need them.</p>
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

            <details className="diagnostic-options">
              <summary><span>Test options</span><span>{methods.find((item) => item.id === method)?.label} · {methods.find((item) => item.id === method)?.detail}</span></summary>
              <div className="diagnostic-options-body">
                <div className="diagnostic-options-copy">Transfer topology is an expert option. Both runs single and aggregate flows so you can distinguish one-flow behavior from parallel capacity.</div>
                <div className="method-control" aria-label="Measurement method">
                  {methods.map((item) => (
                    <button key={item.id} type="button" className={method === item.id ? 'active' : ''} onClick={() => selectMethod(item.id)} disabled={running}>{item.label}</button>
                  ))}
                </div>
              </div>
            </details>
          </div>

          {advancedStatus.hasOverrides && (
            <div className="advanced-config-banner">
              <div><span aria-hidden="true">⚙</span><div><strong>Advanced configuration active</strong><small>{advancedStatus.summary}</small></div></div>
              <div><button type="button" onClick={() => scrollToSection('advanced-diagnostics')}>Review</button><button type="button" onClick={() => setAdvancedResetRequest((current) => current + 1)}>Reset</button></div>
            </div>
          )}

          {pendingRun && !running && (
            <div className="high-data-confirm" role="alert">
              <div>
                <span>High-data diagnostic</span>
                <strong>{profileTitle(pendingRun.profile)} may transfer up to {formatBytes(pendingRun.plan.transferCapBytes)}.</strong>
                <p>{durationFor(pendingRun.profile)}. This warning follows the actual measurement plan regardless of where the test was started.</p>
              </div>
              <div><button type="button" className="secondary-action" onClick={() => setPendingRun(null)}>Cancel</button><button type="button" className="primary-action" onClick={() => void startDiagnostic(pendingRun.profile, pendingRun.method)}>Run diagnostic</button></div>
            </div>
          )}

          {running ? (
            <div className="diagnostic-run-state running">
              <div className="diagnostic-run-heading">
                <div>
                  <div className="run-status-line"><span className="pulse" aria-hidden="true" />{phaseLabel(progress?.phase, progress?.message)}</div>
                  <h3>{progress?.message || 'Preparing the test…'}</h3>
                  <p>{profileTitle(activeProfile.current)} is running through the native engine. Passive monitoring continues, but test-generated load is identified separately.</p>
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
                  <h3>{latestReport?.presentation.verdict || `${displayedProfile.title} complete`}</h3>
                  <p>{latestReport?.presentation.summary || 'Latest successful measurement. It stays here until the next diagnostic completes.'}</p>
                </div>
                <div className="diagnostic-result-actions">
                  <button type="button" className="secondary-action" onClick={() => openHistory(result.reportId)}>View report</button>
                  <button type="button" className="primary-action" onClick={() => void prepareDiagnostic()} disabled={!desktopBridge.available}>Run {selectedProfile.title}</button>
                </div>
              </div>
              <div className="metric-strip diagnostic-metrics" aria-label="Latest diagnostic metrics">
                <Metric label="Latency" value={metric(result.latencyMs, 'ms')} detail="Median latency" />
                <Metric label="Download" value={metric(result.downloadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Upload" value={metric(result.uploadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Request loss" value={metric(result.requestLossPercent, '%')} detail="First-party requests" />
              </div>
              {latestReport && (
                <div className="diagnostic-verdict">
                  <div className="diagnostic-verdict-main">
                    <span>{latestReport.presentation.label}</span>
                    <strong>{primaryFinding?.title || latestReport.presentation.verdict}</strong>
                    <p>{primaryFinding?.summary || latestReport.presentation.summary}</p>
                  </div>
                  <div className="recommended-next-step">
                    <span>Recommended next step</span>
                    <strong>{latestReport.presentation.nextAction || 'No additional testing is required right now.'}</strong>
                    {recommendedProfile ? <p><button type="button" className="inline-action" onClick={() => { selectProfile(recommendedProfile); scrollToSection('run-diagnostics'); }}>Prepare {profileTitle(recommendedProfile)}</button></p> : <p>Keep passive monitoring running and retest only if the connection changes.</p>}
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="diagnostic-run-state ready">
              <div>
                <span className="result-label">Selected test</span>
                <h3>{selectedProfile.title}</h3>
                <p>{selectedProfile.idleCopy}</p>
                <div className="diagnostic-plan-facts">
                  <span><b>{selectedProfile.duration}</b> typical duration</span>
                  <span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span>
                  <span><b>{methodLabel(method)}</b> test topology</span>
                </div>
                <div className="diagnostic-evidence-summary"><span>Evidence</span><strong>{selectedProfile.evidence}</strong></div>
              </div>
              <button type="button" className="primary-action diagnostic-run-button" onClick={() => void prepareDiagnostic()} disabled={!desktopBridge.available}>Run {selectedProfile.title}</button>
            </div>
          )}

          {running && (
            <section className="detail-row diagnostic-evidence-row">
              <div className="plan-card">
                <span className="section-kicker">Active test plan</span>
                <div className="plan-main"><strong>{plan?.profileName || selectedProfile.title}</strong><span>{methodLabel(activeMethod.current)}</span></div>
                <div className="plan-facts">
                  <span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span>
                  <span><b>{plan ? `${plan.downloadStages + plan.uploadStages}` : '—'}</b> transfer stages</span>
                  <span><b>Local</b> report assembly</span>
                </div>
              </div>

              <div className="evidence-card">
                <span className="section-kicker">Run evidence</span>
                <div className="evidence-line"><span>Profile</span><strong>{profileTitle(activeProfile.current)}</strong></div>
                <div className="evidence-line"><span>Phase</span><strong>{phaseLabel(progress?.phase, progress?.message)}</strong></div>
                <div className="evidence-line"><span>Measured payload</span><strong>{formatBytes(measuredBytes)}</strong></div>
                <div className="evidence-line"><span>Method</span><strong>{methodLabel(activeMethod.current)}</strong></div>
              </div>
            </section>
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
        initialReportId={historyInitialReportId}
        docked={historyDocked}
        onClose={() => { setHistoryOpen(false); setHistoryInitialReportId(null); }}
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

function recommendationProfile(result: DiagnosticResult | null, detail: SavedReportDetail | null): DiagnosticProfile | null {
  if (!result || !detail) return null;
  const outcome = detail.presentation.outcome.toLowerCase();
  if (outcome === 'success' || outcome === 'healthy' || outcome === 'good') return null;
  if (result.profile === 'connection-check' || result.profile === 'quick') return 'full';
  return null;
}

function durationFor(profile: DiagnosticProfile): string {
  return profiles.find((item) => item.id === profile)?.duration ?? 'Duration depends on the measurement plan';
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

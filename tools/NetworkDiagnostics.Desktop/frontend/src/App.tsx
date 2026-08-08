import { useEffect, useRef, useState } from 'react';
import { AdvancedDiagnostics, type AdvancedRuntimeStatus } from './AdvancedDiagnostics';
import { AlertsPanel } from './AlertsPanel';
import { desktopBridge } from './bridge';
import { CommandPalette, type PaletteCommand } from './CommandPalette';
import { ContinuousDiagnostics, type MonitorSnapshot } from './ContinuousDiagnostics';
import { HistoryPanel, type SavedReportSummary } from './HistoryPanel';
import { SettingsMenu, type AppearanceMode } from './SettingsMenu';

type TransferMethod = 'compare' | 'single' | 'aggregate';
type DownloadPathPreference = 'automatic' | 'direct-r2' | 'worker';
type DiagnosticProfile = 'connection-check' | 'quick' | 'full' | 'stress';
type WorkbenchSection = 'live-network-health' | 'run-diagnostics' | 'advanced-diagnostics';
type InterfaceChoice = Record<string, unknown>;

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

type DiagnosticStage = {
  id: string;
  direction: 'download' | 'upload';
  strategy: 'single' | 'aggregate';
  connections: number;
  durationMs: number;
  capBytes: number;
  samples: number;
};

type DiagnosticPlan = {
  profile: DiagnosticProfile;
  profileName: string;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  estimatedSeconds: number;
  transferCapBytes: number;
  includeServices: boolean;
  deepDiagnostics: boolean;
  idlePingCount: number;
  pingIntervalMs: number;
  downloadStages: DiagnosticStage[];
  uploadStages: DiagnosticStage[];
  downloadRuns: number;
  maxDownloadConnections: number;
  maxUploadConnections: number;
  totalTransferStages: number;
};

type DownloadDelivery = {
  requestedPath: string;
  selectedPath: string;
  r2ProbeStatus: string;
  fallbackReason?: string | null;
  r2Origin: string;
  bytes: number;
  requestsStarted: number;
  requestsCompleted: number;
  r2Requests: number;
  workerRequests: number;
};

type RunAccepted = {
  runId: string;
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
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
  downloadPath?: DownloadPathPreference;
  downloadDelivery?: DownloadDelivery | null;
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
  technicalData?: string[];
  technicalEvidence?: string[];
};

type SavedReportDetail = {
  report: SavedReportSummary;
  context: string;
  method: string;
  downloadDelivery?: DownloadDelivery | null;
  presentation: ReportPresentation;
};

type PendingRun = {
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  plan: DiagnosticPlan;
};

type AdvancedSettings = {
  endpointCandidates: string[];
  interfaceId?: string | null;
  includeLocalIdentifiers: boolean;
  lanTarget?: string | null;
  lanPort: number;
  lanDurationSeconds: number;
  lanConnections: number;
};

type PreflightResult = {
  measurement?: unknown;
  interfaces?: InterfaceChoice[];
};

type DiagnosticFailure = { runId: string; message: string; errorType: string };
type LiveMetrics = { latencyMs: number | null; downloadMbps: number | null; uploadMbps: number | null };
type ProfileOption = {
  id: DiagnosticProfile;
  label: string;
  title: string;
  description: string;
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
    description: 'Fast native baseline for responsiveness, loss, throughput, loaded latency, and current path behavior.',
  },
  {
    id: 'quick',
    label: 'Quick',
    title: 'Quick Test',
    description: 'Short native run with broader throughput sampling and path data while keeping transfer use moderate.',
  },
  {
    id: 'full',
    label: 'Full',
    title: 'Full Test',
    description: 'Complete native network diagnostic with Internet performance, local-link, routing, protocol, and host data.',
  },
  {
    id: 'stress',
    label: 'Stress',
    title: 'Stress Test',
    description: 'Heavy sustained-load run with connection scaling, capacity limits, loaded responsiveness, and full native diagnostics.',
  },
];

const methods: Array<{ id: TransferMethod; label: string; detail: string }> = [
  { id: 'compare', label: 'Both', detail: 'Single + aggregate flows' },
  { id: 'single', label: 'Single', detail: 'One transfer flow' },
  { id: 'aggregate', label: 'Aggregate', detail: 'Parallel flows' },
];

const downloadPaths: Array<{ id: DownloadPathPreference; label: string; detail: string }> = [
  { id: 'automatic', label: 'Automatic', detail: 'R2 with Worker fallback' },
  { id: 'direct-r2', label: 'Direct R2', detail: 'Require direct R2' },
  { id: 'worker', label: 'Worker', detail: 'Worker stream only' },
];

function App() {
  const [host, setHost] = useState<HostInfo | null>(null);
  const [appearance, setAppearance] = useState<AppearanceMode>('system');
  const [profile, setProfile] = useState<DiagnosticProfile>('connection-check');
  const [method, setMethod] = useState<TransferMethod>('compare');
  const [downloadPath, setDownloadPath] = useState<DownloadPathPreference>('automatic');
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
  const [alertsOpen, setAlertsOpen] = useState(false);
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
  const [advancedSyncKey, setAdvancedSyncKey] = useState(0);
  const [advancedSettings, setAdvancedSettings] = useState<AdvancedSettings | null>(null);
  const [interfaces, setInterfaces] = useState<InterfaceChoice[]>([]);
  const [preflight, setPreflight] = useState<PreflightResult | null>(null);
  const [preflightLoading, setPreflightLoading] = useState(false);
  const [preflightError, setPreflightError] = useState<string | null>(null);
  const [diagnosticsInView, setDiagnosticsInView] = useState(true);
  const [activeSection, setActiveSection] = useState<WorkbenchSection>('live-network-health');
  const [historyDocked, setHistoryDocked] = useState(false);
  const activeRunId = useRef<string | null>(null);
  const activeMethod = useRef<TransferMethod>('compare');
  const activeProfile = useRef<DiagnosticProfile>('connection-check');
  const activeDownloadPath = useRef<DownloadPathPreference>('automatic');
  const highestProgress = useRef(0);
  const stageBytes = useRef(new Map<string, number>());
  const appearanceRequest = useRef(0);
  const preflightRequest = useRef(0);

  const selectedProfile = profiles.find((item) => item.id === profile) ?? profiles[0];
  const displayedProfile = result ? profiles.find((item) => item.id === result.profile) ?? selectedProfile : selectedProfile;
  const selectedPath = downloadPaths.find((item) => item.id === downloadPath) ?? downloadPaths[0];
  const preflightSummary = summarizePreflight(preflight?.measurement);
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
        void loadRunConfiguration(true);
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
    void describePlan(profile, method, downloadPath)
      .then(setPlan)
      .catch((value: Error) => setError(value.message));
  }, [profile, method, downloadPath]);

  useEffect(() => {
    if (!desktopBridge.available || !advancedSettings || running) return;
    void refreshPreflight(advancedSettings);
  }, [profile, method, downloadPath, advancedSettings?.interfaceId]);

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
      for (const entry of entries) visibility.set(entry.target.id as WorkbenchSection, entry.isIntersecting ? entry.intersectionRatio : 0);
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
        setAlertsOpen(false);
        historyOpen ? setHistoryOpen(false) : openHistory();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [historyOpen]);

  const progressPercent = Math.round(progressRatio * 100);

  async function describePlan(nextProfile: DiagnosticProfile, nextMethod: TransferMethod, nextPath: DownloadPathPreference): Promise<DiagnosticPlan> {
    return desktopBridge.request<DiagnosticPlan>('diagnostic.describePlan', { profile: nextProfile, method: nextMethod, downloadPath: nextPath });
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
    setAlertsOpen(false);
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

  async function loadRunConfiguration(refresh = false) {
    if (!desktopBridge.available) return;
    try {
      const [settings, choices] = await Promise.all([
        desktopBridge.request<AdvancedSettings>('settings.getAdvanced'),
        desktopBridge.request<InterfaceChoice[]>('diagnostic.interfaces'),
      ]);
      setAdvancedSettings(settings);
      setInterfaces(choices);
      if (refresh) await refreshPreflight(settings);
    } catch (value) {
      setPreflightError(value instanceof Error ? value.message : 'Native measurement configuration could not be read.');
    }
  }

  async function refreshPreflight(settingsOverride: AdvancedSettings | null = advancedSettings) {
    if (!desktopBridge.available || !settingsOverride || running) return;
    const request = ++preflightRequest.current;
    setPreflightLoading(true);
    setPreflightError(null);
    try {
      const next = await desktopBridge.request<PreflightResult>('diagnostic.preflight', { profile, method, downloadPath });
      if (request !== preflightRequest.current) return;
      setPreflight(next);
      if (next.interfaces?.length) setInterfaces(next.interfaces);
    } catch (value) {
      if (request !== preflightRequest.current) return;
      setPreflight(null);
      setPreflightError(value instanceof Error ? value.message : 'Native preflight could not be completed.');
    } finally {
      if (request === preflightRequest.current) setPreflightLoading(false);
    }
  }

  async function selectInterface(interfaceId: string) {
    if (!advancedSettings || running) return;
    setError(null);
    try {
      const saved = await desktopBridge.request<AdvancedSettings>('settings.setAdvanced', {
        ...advancedSettings,
        interfaceId: interfaceId || null,
      });
      setAdvancedSettings(saved);
      setAdvancedSyncKey((current) => current + 1);
      await refreshPreflight(saved);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The network interface could not be changed.');
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

  function selectDownloadPath(next: DownloadPathPreference) {
    if (running || next === downloadPath) return;
    setDownloadPath(next);
    setPendingRun(null);
    setError(null);
  }

  async function prepareDiagnostic(
    nextProfile: DiagnosticProfile = profile,
    nextMethod: TransferMethod = method,
    nextPath: DownloadPathPreference = downloadPath,
  ) {
    if (running || !desktopBridge.available) return;
    setError(null);
    try {
      const nextPlan = nextProfile === profile && nextMethod === method && nextPath === downloadPath && plan
        ? plan
        : await describePlan(nextProfile, nextMethod, nextPath);
      if (nextPlan.transferCapBytes >= HIGH_DATA_WARNING_BYTES) {
        setPendingRun({ profile: nextProfile, method: nextMethod, downloadPath: nextPath, plan: nextPlan });
        scrollToSection('run-diagnostics');
        return;
      }
      await startDiagnostic(nextProfile, nextMethod, nextPath);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The diagnostic plan could not be prepared.');
    }
  }

  async function startDiagnostic(nextProfile: DiagnosticProfile, nextMethod: TransferMethod, nextPath: DownloadPathPreference) {
    if (running) return;
    if (nextProfile !== profile) setProfile(nextProfile);
    if (nextMethod !== method) setMethod(nextMethod);
    if (nextPath !== downloadPath) setDownloadPath(nextPath);
    activeProfile.current = nextProfile;
    activeMethod.current = nextMethod;
    activeDownloadPath.current = nextPath;
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
      const accepted = await desktopBridge.request<RunAccepted>('diagnostic.run', {
        profile: nextProfile,
        method: nextMethod,
        downloadPath: nextPath,
      });
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
    void prepareDiagnostic('connection-check', 'aggregate', 'automatic');
  }

  function measurePeakCapacity() {
    scrollToSection('run-diagnostics');
    void prepareDiagnostic('stress', 'aggregate', 'automatic');
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
      detail: 'Current score, passive measurements, timeline, and capacity history.',
      keywords: 'monitor health timeline continuous live network',
      priority: 1,
      enabled: true,
      run: () => scrollToSection('live-network-health'),
    },
    {
      id: 'workspace-diagnostics',
      title: 'Go to Run Diagnostics',
      detail: 'Profiles, transfer topology, download path, native plan, and measurement path.',
      keywords: 'run diagnostics test connection quick full stress r2 worker',
      priority: 2,
      enabled: true,
      run: () => scrollToSection('run-diagnostics'),
    },
    {
      id: 'workspace-alerts',
      title: 'Open Issues & Alerts',
      detail: 'Review outages, recoveries, network changes, and degradation events.',
      keywords: 'alerts issues outage recovery network change events',
      priority: 3,
      enabled: desktopBridge.available,
      run: () => { setHistoryOpen(false); setAlertsOpen(true); },
    },
    {
      id: 'workspace-history',
      title: 'Open Saved Runs',
      detail: 'Browse, compare, import, export, and annotate persisted reports.',
      keywords: 'history reports saved runs compare json',
      shortcut: 'Ctrl/⌘ H',
      priority: 4,
      enabled: desktopBridge.available,
      run: () => openHistory(),
    },
    {
      id: 'workspace-advanced',
      title: 'Go to Advanced Diagnostics',
      detail: 'Custom endpoints, native preflight, privacy, and LAN tools.',
      keywords: 'advanced endpoint lan privacy preflight',
      priority: 5,
      enabled: true,
      run: () => scrollToSection('advanced-diagnostics'),
    },
    {
      id: 'run-selected',
      title: running ? 'Diagnostic is already running' : `Run ${selectedProfile.title}`,
      detail: `${methodLabel(method)} · ${downloadPathLabel(downloadPath)}.`,
      keywords: 'run start diagnostic current selected test',
      priority: 6,
      enabled: desktopBridge.available && !running,
      run: () => { scrollToSection('run-diagnostics'); void prepareDiagnostic(); },
    },
    {
      id: 'run-content',
      title: 'Measure Capacity',
      detail: 'Connection Check + Aggregate using Automatic download path.',
      keywords: 'content speed aggregate low data bandwidth capacity',
      priority: 7,
      enabled: desktopBridge.available && !running,
      run: measureCapacity,
    },
    {
      id: 'run-peak',
      title: 'Measure Peak Capacity',
      detail: 'Stress + Aggregate using Automatic download path.',
      keywords: 'peak speed stress aggregate bandwidth capacity',
      priority: 8,
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
      title: `Use ${item.label} transfer topology`,
      detail: item.detail,
      keywords: `method transfer flow ${item.id} ${item.detail}`,
      priority: 30 + index,
      enabled: !running,
      run: () => { selectMethod(item.id); scrollToSection('run-diagnostics'); },
    } satisfies PaletteCommand)),
    ...downloadPaths.map((item, index) => ({
      id: `download-path-${item.id}`,
      title: `Use ${item.label} download path`,
      detail: item.detail,
      keywords: `download path r2 worker ${item.id}`,
      priority: 40 + index,
      enabled: !running,
      run: () => { selectDownloadPath(item.id); scrollToSection('run-diagnostics'); },
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
  const technicalData = latestReport?.presentation.technicalData ?? latestReport?.presentation.technicalEvidence ?? [];

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
          <button type="button" className={`history-trigger ${historyOpen ? 'active' : ''}`} aria-label="Saved runs" aria-expanded={historyOpen} onClick={() => { setAlertsOpen(false); historyOpen ? setHistoryOpen(false) : openHistory(); }} disabled={!desktopBridge.available}>
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 7v5l3 2" /><circle cx="12" cy="12" r="8" /></svg>
            <span>History</span>
          </button>
          <button type="button" className={`history-trigger alerts-trigger ${alertsOpen ? 'active' : ''}`} aria-label={monitorSnapshot?.unreadAlertCount ? `Alerts, ${monitorSnapshot.unreadAlertCount} unread` : 'Alerts'} aria-expanded={alertsOpen} onClick={() => { setHistoryOpen(false); setAlertsOpen((current) => !current); }} disabled={!desktopBridge.available}>
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6.8 9.6a5.2 5.2 0 0 1 10.4 0c0 5.2 2.1 5.7 2.1 5.7H4.7s2.1-.5 2.1-5.7Z" /><path d="M10 18.2h4" /></svg>
            <span>Alerts</span>
            {!!monitorSnapshot?.unreadAlertCount && <b>{monitorSnapshot.unreadAlertCount > 99 ? '99+' : monitorSnapshot.unreadAlertCount}</b>}
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
          <div className="workbench-section-header diagnostics-header"><div><h2 id="run-diagnostics-title">RUN DIAGNOSTICS</h2></div></div>
          {error && <div className="error-banner" role="alert">{error}</div>}

          {!running && (
            <div className="diagnostic-launcher">
              <section className="diagnostic-profile-control">
                <span className="diagnostic-control-label">Test profile</span>
                <div className="method-control profile-control" aria-label="Diagnostic profile">
                  {profiles.map((item) => (
                    <button key={item.id} type="button" className={profile === item.id ? 'active' : ''} onClick={() => selectProfile(item.id)}>{item.label}</button>
                  ))}
                </div>
                <p>{selectedProfile.description}</p>
              </section>

              <div className="diagnostic-choice-grid">
                <section className="diagnostic-choice-group">
                  <div><span className="diagnostic-control-label">Transfer topology</span><small>{methods.find((item) => item.id === method)?.detail}</small></div>
                  <div className="method-control compact" aria-label="Transfer topology">
                    {methods.map((item) => (
                      <button key={item.id} type="button" className={method === item.id ? 'active' : ''} onClick={() => selectMethod(item.id)}>{item.label}</button>
                    ))}
                  </div>
                </section>
                <section className="diagnostic-choice-group">
                  <div><span className="diagnostic-control-label">Download path</span><small>{selectedPath.detail}</small></div>
                  <div className="method-control compact path-control" aria-label="Download measurement path">
                    {downloadPaths.map((item) => (
                      <button key={item.id} type="button" className={downloadPath === item.id ? 'active' : ''} onClick={() => selectDownloadPath(item.id)}>{item.label}</button>
                    ))}
                  </div>
                </section>
              </div>

              <div className="diagnostic-information-grid">
                <section className="diagnostic-info-card test-plan-card">
                  <div className="diagnostic-info-heading"><span>TEST PLAN</span><strong>{plan?.profileName ?? selectedProfile.title}</strong></div>
                  <dl className="diagnostic-data-list">
                    <DataRow label="Estimated time" value={plan ? formatDuration(plan.estimatedSeconds) : '—'} />
                    <DataRow label="Transfer cap" value={plan ? formatBytes(plan.transferCapBytes) : '—'} />
                    <DataRow label="Download connections" value={plan ? connectionSequence(plan.downloadStages) : '—'} />
                    <DataRow label="Upload connections" value={plan ? connectionSequence(plan.uploadStages) : '—'} />
                    <DataRow label="Download runs" value={plan ? String(plan.downloadRuns) : '—'} />
                    <DataRow label="Transfer stages" value={plan ? String(plan.totalTransferStages) : '—'} />
                    <DataRow label="Baseline samples" value={plan ? String(plan.idlePingCount) : '—'} />
                    <DataRow label="Service checks" value={plan ? plan.includeServices ? 'Enabled' : 'Off' : '—'} />
                    <DataRow label="Native deep diagnostics" value={plan ? plan.deepDiagnostics ? 'Full suite' : 'Core set' : '—'} />
                    <DataRow label="Saved report" value="Automatic · local" />
                  </dl>
                </section>

                <section className="diagnostic-info-card measurement-path-card">
                  <div className="diagnostic-info-heading"><span>MEASUREMENT PATH</span><button type="button" className="text-action" disabled={preflightLoading || !desktopBridge.available} onClick={() => void refreshPreflight()}>{preflightLoading ? 'Checking…' : 'Refresh'}</button></div>
                  <dl className="diagnostic-data-list">
                    <DataRow label="Endpoint" value={preflightLoading ? 'Selecting…' : preflightSummary.endpoint} />
                    <DataRow label="Provider / network" value={preflightSummary.providerNetwork} />
                    <DataRow label="Edge / location" value={preflightSummary.edge} />
                    <DataRow label="Preflight latency" value={preflightSummary.latency} />
                    <div className="diagnostic-data-row interface-row"><dt>Network interface</dt><dd>
                      <select aria-label="Network interface" value={advancedSettings?.interfaceId ?? ''} disabled={!advancedSettings || running} onChange={(event) => void selectInterface(event.target.value)}>
                        <option value="">Automatic routing</option>
                        {interfaces.map((choice, index) => {
                          const id = interfaceId(choice);
                          if (!id) return null;
                          return <option key={`${id}-${index}`} value={id}>{interfaceLabel(choice, id)}</option>;
                        })}
                        {advancedSettings?.interfaceId && !interfaces.some((choice) => interfaceId(choice) === advancedSettings.interfaceId) && <option value={advancedSettings.interfaceId}>{advancedSettings.interfaceId}</option>}
                      </select>
                    </dd></div>
                    <DataRow label="Download path" value={downloadPathLabel(downloadPath)} />
                    <DataRow label="R2 behavior" value={downloadPath === 'worker' ? 'Not requested' : downloadPath === 'direct-r2' ? 'Required' : 'Probe + fallback'} />
                  </dl>
                  {preflightError && <p className="diagnostic-path-error">{preflightError}</p>}
                  {advancedSettings?.endpointCandidates.length ? <p className="diagnostic-path-note">{advancedSettings.endpointCandidates.length} custom endpoint candidate{advancedSettings.endpointCandidates.length === 1 ? '' : 's'} active.</p> : <p className="diagnostic-path-note">Built-in first-party endpoint selection.</p>}
                </section>

                <section className="diagnostic-info-card data-collected-card">
                  <div className="diagnostic-info-heading"><span>DATA COLLECTED</span><strong>{dataCollectionLevel(profile)}</strong></div>
                  <div className="data-collection-groups">
                    {dataCollectedFor(profile, method, !!advancedSettings?.lanTarget).map((group) => (
                      <div className="data-collection-group" key={group.label}><span>{group.label}</span><ul>{group.items.map((item) => <li key={item}>{item}</li>)}</ul></div>
                    ))}
                  </div>
                </section>
              </div>
            </div>
          )}

          {advancedStatus.hasOverrides && !running && (
            <div className="advanced-config-banner">
              <div><span aria-hidden="true">⚙</span><div><strong>Custom configuration active</strong><small>{advancedStatus.summary}</small></div></div>
              <div><button type="button" onClick={() => scrollToSection('advanced-diagnostics')}>Review</button><button type="button" onClick={() => setAdvancedResetRequest((current) => current + 1)}>Reset</button></div>
            </div>
          )}

          {pendingRun && !running && (
            <div className="high-data-confirm" role="alert">
              <div><span>High-data diagnostic</span><strong>{profileTitle(pendingRun.profile)} may transfer up to {formatBytes(pendingRun.plan.transferCapBytes)}.</strong><p>Estimated {formatDuration(pendingRun.plan.estimatedSeconds)} · {methodLabel(pendingRun.method)} · {downloadPathLabel(pendingRun.downloadPath)}.</p></div>
              <div><button type="button" className="secondary-action" onClick={() => setPendingRun(null)}>Cancel</button><button type="button" className="primary-action" onClick={() => void startDiagnostic(pendingRun.profile, pendingRun.method, pendingRun.downloadPath)}>Run diagnostic</button></div>
            </div>
          )}

          {running ? (
            <div className="diagnostic-running-stack">
              <div className="diagnostic-run-state running">
                <div className="diagnostic-run-heading">
                  <div><div className="run-status-line"><span className="pulse" aria-hidden="true" />{phaseLabel(progress?.phase, progress?.message)}</div><h3>{progress?.message || 'Preparing the test…'}</h3><p>{profileTitle(activeProfile.current)} is running through the native engine. Test-generated traffic is tracked separately from passive health.</p></div>
                  <button type="button" className="secondary-action" onClick={() => void cancelDiagnostic()}>Cancel test</button>
                </div>
                <div className="diagnostic-progress-row"><div className="diagnostic-progress-track" aria-label={`${progressPercent}% complete`}><span style={{ width: `${progressPercent}%` }} /></div><strong>{progressPercent}%</strong></div>
                <div className="metric-strip diagnostic-metrics" aria-label="Active diagnostic metrics">
                  <Metric label="Latency" value={metric(liveMetrics.latencyMs, 'ms')} detail="Latest baseline sample" />
                  <Metric label="Download" value={metric(liveMetrics.downloadMbps, 'Mbps')} detail="Latest live sample" />
                  <Metric label="Upload" value={metric(liveMetrics.uploadMbps, 'Mbps')} detail="Latest live sample" />
                  <Metric label="Payload" value={formatBytes(measuredBytes)} detail="Measured so far" />
                </div>
              </div>
              <section className="detail-row diagnostic-run-data-row">
                <div className="plan-card">
                  <span className="section-kicker">Active test plan</span>
                  <div className="plan-main"><strong>{plan?.profileName || selectedProfile.title}</strong><span>{methodLabel(activeMethod.current)} · {downloadPathLabel(activeDownloadPath.current)}</span></div>
                  <div className="plan-facts"><span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span><span><b>{plan?.totalTransferStages ?? '—'}</b> transfer stages</span><span><b>{plan?.maxDownloadConnections ?? '—'}</b> max download connections</span></div>
                </div>
                <div className="data-card">
                  <span className="section-kicker">Run data</span>
                  <div className="data-line"><span>Phase</span><strong>{phaseLabel(progress?.phase, progress?.message)}</strong></div>
                  <div className="data-line"><span>Endpoint</span><strong>{preflightSummary.endpoint}</strong></div>
                  <div className="data-line"><span>Interface</span><strong>{activeInterfaceLabel(advancedSettings, interfaces)}</strong></div>
                  <div className="data-line"><span>Download path</span><strong>{downloadPathLabel(activeDownloadPath.current)}</strong></div>
                  <div className="data-line"><span>Measured payload</span><strong>{formatBytes(measuredBytes)}</strong></div>
                </div>
              </section>
            </div>
          ) : result ? (
            <div className="diagnostic-run-state result">
              <div className="diagnostic-run-heading">
                <div><span className="result-label">Latest diagnostic · {new Date(result.generatedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</span><h3>{latestReport?.presentation.verdict || `${displayedProfile.title} complete`}</h3><p>{latestReport?.presentation.summary || 'Latest successful measurement. It stays here until the next diagnostic completes.'}</p></div>
                <div className="diagnostic-result-actions"><button type="button" className="secondary-action" onClick={() => openHistory(result.reportId)}>View report</button><button type="button" className="primary-action" onClick={() => void prepareDiagnostic()} disabled={!desktopBridge.available}>Run {selectedProfile.title}</button></div>
              </div>
              <div className="metric-strip diagnostic-metrics" aria-label="Latest diagnostic metrics">
                <Metric label="Latency" value={metric(result.latencyMs, 'ms')} detail="Median latency" />
                <Metric label="Download" value={metric(result.downloadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Upload" value={metric(result.uploadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Request loss" value={metric(result.requestLossPercent, '%')} detail="First-party requests" />
              </div>
              {(result.downloadDelivery || latestReport?.downloadDelivery) && <DownloadDeliveryPanel delivery={result.downloadDelivery ?? latestReport?.downloadDelivery ?? null} />}
              {latestReport && (
                <>
                  <div className="diagnostic-verdict">
                    <div className="diagnostic-verdict-main"><span>{latestReport.presentation.label}</span><strong>{primaryFinding?.title || latestReport.presentation.verdict}</strong><p>{primaryFinding?.summary || latestReport.presentation.summary}</p></div>
                    <div className="recommended-next-step"><span>Recommended next step</span><strong>{latestReport.presentation.nextAction || 'No additional testing is required right now.'}</strong>{recommendedProfile ? <p><button type="button" className="inline-action" onClick={() => { selectProfile(recommendedProfile); scrollToSection('run-diagnostics'); }}>Prepare {profileTitle(recommendedProfile)}</button></p> : <p>Keep passive monitoring running and retest only if the connection changes.</p>}</div>
                  </div>
                  <section className="result-measurements">
                    <div className="result-section-heading"><span>MEASUREMENTS</span><strong>{latestReport.presentation.metrics.filter((item) => item.wasMeasured).length} measured</strong></div>
                    <div className="result-measurement-grid">{latestReport.presentation.metrics.map((item, index) => <div className={`result-measurement ${item.wasMeasured ? '' : 'not-measured'}`} key={`${item.label}-${index}`}><span>{item.label}</span><strong>{item.value}</strong><small>{item.detail}</small></div>)}</div>
                  </section>
                  {technicalData.length > 0 && <details className="result-technical-data"><summary>Technical data · {technicalData.length} items</summary><ul>{technicalData.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}</ul></details>}
                </>
              )}
            </div>
          ) : (
            <div className="diagnostic-run-state ready">
              <div><strong>{selectedProfile.title}</strong><span>{plan ? `${formatDuration(plan.estimatedSeconds)} · up to ${formatBytes(plan.transferCapBytes)} · ${methodLabel(method)} · ${downloadPathLabel(downloadPath)}` : 'Reading native test plan…'}</span></div>
              <button type="button" className="primary-action diagnostic-run-button" onClick={() => void prepareDiagnostic()} disabled={!desktopBridge.available || !plan}>Run {selectedProfile.title}</button>
            </div>
          )}
        </section>

        <AdvancedDiagnostics
          key={`advanced-${advancedSyncKey}`}
          profile={profile}
          method={method}
          onStatusChange={(next) => { setAdvancedStatus(next); void loadRunConfiguration(false); }}
          resetRequest={advancedResetRequest}
        />
      </main>

      {running && !diagnosticsInView && (
        <button type="button" className="sticky-run-status" onClick={() => scrollToSection('run-diagnostics')}><span className="pulse" aria-hidden="true" /><strong>{profileTitle(activeProfile.current)}</strong><small>{phaseLabel(progress?.phase, progress?.message)} · {progressPercent}%</small><b>View</b></button>
      )}

      <AlertsPanel open={alertsOpen} snapshot={monitorSnapshot} onUpdate={setMonitorSnapshot} onClose={() => setAlertsOpen(false)} />
      <HistoryPanel open={historyOpen} reports={reports} loading={historyLoading} error={historyError} initialReportId={historyInitialReportId} docked={historyDocked} onClose={() => { setHistoryOpen(false); setHistoryInitialReportId(null); }} onRefresh={() => void loadReports()} />
      <CommandPalette open={paletteOpen} commands={paletteCommands} onClose={() => setPaletteOpen(false)} />
    </div>
  );
}

function DataRow({ label, value }: { label: string; value: string }) {
  return <div className="diagnostic-data-row"><dt>{label}</dt><dd>{value}</dd></div>;
}

function Metric({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <div className="metric"><span>{label}</span><strong>{value}</strong><small>{detail}</small></div>;
}

function DownloadDeliveryPanel({ delivery }: { delivery: DownloadDelivery | null }) {
  if (!delivery) return null;
  return <section className="download-delivery-panel"><div className="result-section-heading"><span>DOWNLOAD DELIVERY</span><strong>{downloadPathResultLabel(delivery.selectedPath)}</strong></div><div className="download-delivery-grid"><div><span>Requested</span><strong>{downloadPathResultLabel(delivery.requestedPath)}</strong></div><div><span>Actual path</span><strong>{downloadPathResultLabel(delivery.selectedPath)}</strong></div><div><span>R2 status</span><strong>{statusLabel(delivery.r2ProbeStatus)}</strong></div><div><span>Requests</span><strong>{delivery.requestsCompleted} / {delivery.requestsStarted} completed</strong></div><div><span>R2 requests</span><strong>{delivery.r2Requests}</strong></div><div><span>Worker requests</span><strong>{delivery.workerRequests}</strong></div></div>{delivery.fallbackReason && <p>{delivery.fallbackReason}</p>}</section>;
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
      if (message.includes('aggregate') || message.includes('scale')) return 0.40 + fraction * 0.20;
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

function metric(value: number | null | undefined, unit: string): string { return value == null ? '—' : `${formatNumber(value)} ${unit}`; }
function formatNumber(value: number): string { return new Intl.NumberFormat(undefined, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value); }
function formatBytes(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return value === 0 ? '0 MB' : '—';
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)} GB`;
  return `${(value / 1_000_000).toFixed(value >= 100_000_000 ? 0 : 1)} MB`;
}
function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds <= 0) return '—';
  if (seconds < 60) return `${Math.round(seconds)} sec`;
  const minutes = Math.floor(seconds / 60);
  const remainder = Math.round(seconds % 60);
  return remainder ? `${minutes} min ${remainder} sec` : `${minutes} min`;
}
function connectionSequence(stages: DiagnosticStage[]): string {
  const values = stages.map((stage) => stage.connections);
  return values.length ? values.join(' + ') : '—';
}
function methodLabel(method: TransferMethod): string {
  if (method === 'single') return 'Single flow';
  if (method === 'aggregate') return 'Aggregate flows';
  return 'Single + aggregate';
}
function downloadPathLabel(path: DownloadPathPreference): string {
  if (path === 'direct-r2') return 'Direct R2';
  if (path === 'worker') return 'Worker';
  return 'Automatic';
}
function downloadPathResultLabel(path: string): string {
  switch (path.toLowerCase()) {
    case 'direct-r2': return 'Direct R2';
    case 'worker': return 'Worker';
    case 'mixed': return 'R2 → Worker';
    case 'unavailable': return 'Unavailable';
    default: return 'Automatic';
  }
}
function statusLabel(value: string): string { return value.replaceAll('-', ' ').replace(/\b\w/g, (match) => match.toUpperCase()); }
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
      return 'Network diagnostics';
    }
    case 'complete': return 'Complete';
    case 'starting': return 'Preparing';
    default: return 'Measuring';
  }
}

function dataCollectionLevel(profile: DiagnosticProfile): string {
  if (profile === 'stress') return 'Maximum';
  if (profile === 'full') return 'Full';
  if (profile === 'quick') return 'Expanded';
  return 'Core';
}

function dataCollectedFor(profile: DiagnosticProfile, method: TransferMethod, hasLanTarget: boolean): Array<{ label: string; items: string[] }> {
  const core = [
    'HTTP latency + jitter + request loss',
    'Download + upload throughput',
    'Loaded download + upload latency',
    method === 'compare' ? 'Single-flow + aggregate measurements' : method === 'single' ? 'Single-flow measurements' : 'Aggregate-flow measurements',
    'Transfer timeline + stability + peak rate',
  ];
  const protocol = [
    'Endpoint + network metadata',
    'IPv4 + IPv6 path checks',
    'HTTP/3 availability',
    'Network-change detection',
    'Host CPU + memory + TCP counters',
  ];
  if (profile === 'connection-check' || profile === 'quick') return [{ label: 'Performance', items: core }, { label: 'Path + host', items: protocol }];
  const native = [
    'Gateway + ICMP latency',
    'Traceroute',
    'DNS resolvers',
    'Path MTU',
    'Service reachability',
    'Wi-Fi + routing details',
    'Loaded-path localization',
    ...(hasLanTarget ? ['LAN throughput'] : []),
  ];
  const stress = profile === 'stress' ? ['Connection scaling curve', 'Sustained-load stability', 'Capacity-limit behavior'] : [];
  return [{ label: 'Performance', items: [...core, ...stress] }, { label: 'Path + protocol', items: protocol }, { label: 'Native system', items: native }];
}

function summarizePreflight(measurement: unknown): { endpoint: string; providerNetwork: string; edge: string; latency: string } {
  if (!measurement || typeof measurement !== 'object') return { endpoint: 'Not checked', providerNetwork: '—', edge: '—', latency: '—' };
  const selected = findObjectWithKeys(measurement, ['origin', 'preflightLatencyMs']) ?? findObjectWithKeys(measurement, ['origin', 'name']);
  const endpoint = selected ? stringValue(selected, ['name', 'origin', 'id']) ?? 'Selected endpoint' : findString(measurement, ['origin', 'endpoint']) ?? 'Selected endpoint';
  const provider = selected ? stringValue(selected, ['provider']) : null;
  const network = findString(measurement, ['network', 'isp', 'asnName']);
  const edge = findString(measurement, ['edge', 'location', 'colo', 'city']);
  const latencyValue = selected ? numberValue(selected, ['preflightLatencyMs', 'latencyMs', 'medianLatencyMs']) : findNumber(measurement, ['preflightLatencyMs', 'latencyMs', 'medianLatencyMs']);
  return {
    endpoint,
    providerNetwork: [provider, network].filter(Boolean).join(' · ') || '—',
    edge: edge ?? '—',
    latency: latencyValue == null ? '—' : `${formatNumber(latencyValue)} ms`,
  };
}

function interfaceId(choice: InterfaceChoice): string | null { return stringValue(choice, ['id', 'interfaceId', 'adapterId', 'name']); }
function interfaceLabel(choice: InterfaceChoice, fallback: string): string { const name = stringValue(choice, ['displayName', 'name', 'description', 'interfaceName']); const type = stringValue(choice, ['type', 'interfaceType']); return [name ?? fallback, type].filter(Boolean).join(' · '); }
function activeInterfaceLabel(settings: AdvancedSettings | null, choices: InterfaceChoice[]): string {
  if (!settings?.interfaceId) return 'Automatic routing';
  const match = choices.find((choice) => interfaceId(choice) === settings.interfaceId);
  return match ? interfaceLabel(match, settings.interfaceId) : settings.interfaceId;
}
function stringValue(record: Record<string, unknown>, keys: string[]): string | null { for (const key of keys) { const value = record[key]; if (typeof value === 'string' && value.trim()) return value; } return null; }
function numberValue(record: Record<string, unknown>, keys: string[]): number | null { for (const key of keys) { const value = record[key]; if (typeof value === 'number' && Number.isFinite(value)) return value; } return null; }
function findString(value: unknown, keys: string[]): string | null {
  if (!value || typeof value !== 'object') return null;
  const record = value as Record<string, unknown>;
  const direct = stringValue(record, keys);
  if (direct) return direct;
  for (const child of Object.values(record)) {
    if (Array.isArray(child)) { for (const item of child) { const found = findString(item, keys); if (found) return found; } }
    else if (child && typeof child === 'object') { const found = findString(child, keys); if (found) return found; }
  }
  return null;
}
function findNumber(value: unknown, keys: string[]): number | null {
  if (!value || typeof value !== 'object') return null;
  const record = value as Record<string, unknown>;
  const direct = numberValue(record, keys);
  if (direct != null) return direct;
  for (const child of Object.values(record)) {
    if (Array.isArray(child)) { for (const item of child) { const found = findNumber(item, keys); if (found != null) return found; } }
    else if (child && typeof child === 'object') { const found = findNumber(child, keys); if (found != null) return found; }
  }
  return null;
}
function findObjectWithKeys(value: unknown, keys: string[]): Record<string, unknown> | null {
  if (!value || typeof value !== 'object') return null;
  const record = value as Record<string, unknown>;
  if (keys.every((key) => key in record)) return record;
  for (const child of Object.values(record)) {
    if (Array.isArray(child)) { for (const item of child) { const found = findObjectWithKeys(item, keys); if (found) return found; } }
    else if (child && typeof child === 'object') { const found = findObjectWithKeys(child, keys); if (found) return found; }
  }
  return null;
}

export default App;

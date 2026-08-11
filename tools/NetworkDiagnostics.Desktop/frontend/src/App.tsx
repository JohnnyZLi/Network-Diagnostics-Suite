import { useEffect, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { AdvancedDiagnostics, type AdvancedRuntimeStatus } from './AdvancedDiagnostics';
import { AlertsPanel } from './AlertsPanel';
import { desktopBridge } from './bridge';
import { CommandPalette, type PaletteCommand } from './CommandPalette';
import type {
  AdvancedSettings,
  AppearanceMode,
  DiagnosticPlan,
  DiagnosticProfile,
  DiagnosticProgress,
  DiagnosticResult,
  DownloadDelivery,
  DownloadPathPreference,
  InterfaceChoice,
  MeasurementPathSummary,
  PreflightResult,
  RunAccepted,
  SavedReportDetail,
  SavedReportSummary,
  TransferMethod,
  WorkbenchSection,
} from './contracts';
import { ContinuousDiagnostics, type MonitorSnapshot } from './ContinuousDiagnostics';
import { HistoryPanel } from './HistoryPanel';
import { SettingsMenu } from './SettingsMenu';

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

type PendingRun = {
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  plan: DiagnosticPlan;
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

type StartupPanel = 'history' | 'alerts' | 'settings' | null;
type StartupAdvancedTool = 'configuration' | 'lan';
type StartupOptions = {
  appearance: AppearanceMode | null;
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  workspace: WorkbenchSection;
  panel: StartupPanel;
  advancedTool: StartupAdvancedTool;
  runConnectionCheck: boolean;
};

const startupOptions = readStartupOptions();

function App() {
  const [host, setHost] = useState<HostInfo | null>(null);
  const [appearance, setAppearance] = useState<AppearanceMode>(startupOptions.appearance ?? 'system');
  const [profile, setProfile] = useState<DiagnosticProfile>(startupOptions.profile);
  const [method, setMethod] = useState<TransferMethod>(startupOptions.method);
  const [downloadPath, setDownloadPath] = useState<DownloadPathPreference>(startupOptions.downloadPath);
  const [plan, setPlan] = useState<DiagnosticPlan | null>(null);
  const [progress, setProgress] = useState<DiagnosticProgress | null>(null);
  const [progressRatio, setProgressRatio] = useState(0);
  const [liveMetrics, setLiveMetrics] = useState<LiveMetrics>(emptyLiveMetrics);
  const [measuredBytes, setMeasuredBytes] = useState(0);
  const [result, setResult] = useState<DiagnosticResult | null>(null);
  const [latestSavedReport, setLatestSavedReport] = useState<SavedReportDetail | null>(null);
  const [currentResultDetail, setCurrentResultDetail] = useState<SavedReportDetail | null>(null);
  const [resultActionBusy, setResultActionBusy] = useState<'save' | 'export' | null>(null);
  const [resultNotice, setResultNotice] = useState<string | null>(null);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(startupOptions.panel === 'history');
  const [alertsOpen, setAlertsOpen] = useState(startupOptions.panel === 'alerts');
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
  const [advancedPreflightRequest, setAdvancedPreflightRequest] = useState(0);
  const [advancedSyncKey, setAdvancedSyncKey] = useState(0);
  const [advancedSettings, setAdvancedSettings] = useState<AdvancedSettings | null>(null);
  const [interfaces, setInterfaces] = useState<InterfaceChoice[]>([]);
  const [preflight, setPreflight] = useState<PreflightResult | null>(null);
  const [preflightLoading, setPreflightLoading] = useState(false);
  const [preflightError, setPreflightError] = useState<string | null>(null);
  const [activeSection, setActiveSection] = useState<WorkbenchSection>(startupOptions.workspace);
  const [historyDocked, setHistoryDocked] = useState(false);
  const activeRunId = useRef<string | null>(null);
  const activeMethod = useRef<TransferMethod>('compare');
  const activeProfile = useRef<DiagnosticProfile>('connection-check');
  const activeDownloadPath = useRef<DownloadPathPreference>('automatic');
  const highestProgress = useRef(0);
  const appearanceRequest = useRef(0);
  const preflightRequest = useRef(0);
  const startupRunScheduled = useRef(false);

  const selectedProfile = profiles.find((item) => item.id === profile) ?? profiles[0];
  const displayedProfile = result ? profiles.find((item) => item.id === result.profile) ?? selectedProfile : selectedProfile;
  const selectedPath = downloadPaths.find((item) => item.id === downloadPath) ?? downloadPaths[0];
  const preflightSummary = summarizePreflight(preflight?.measurement);
  const resultPathSummary = summarizePreflight(currentResultDetail?.measurement ?? preflight?.measurement);
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
        setAppearance(startupOptions.appearance ?? info.appearance ?? 'system');
        if (info.monitor) {
          setMonitorSnapshot(info.monitor);
          setMonitorLoading(false);
        } else {
          void loadMonitor();
        }
        void loadRunConfiguration();
        void loadReports(true);
        if (startupOptions.panel === 'history') setHistoryOpen(true);
        if (startupOptions.panel === 'alerts') setAlertsOpen(true);
        if (startupOptions.runConnectionCheck && !startupRunScheduled.current) {
          startupRunScheduled.current = true;
          window.setTimeout(() => void prepareDiagnostic('connection-check', startupOptions.method, startupOptions.downloadPath), 350);
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
      highestProgress.current = Math.max(highestProgress.current, next.overallFraction);
      setProgressRatio(highestProgress.current);
      setLiveMetrics((current) => ({
        latencyMs: next.phase === 'idle' && next.liveLatencyMs != null ? next.liveLatencyMs : current.latencyMs,
        downloadMbps: next.phase === 'download' && next.liveMbps != null ? next.liveMbps : current.downloadMbps,
        uploadMbps: next.phase === 'upload' && next.liveMbps != null ? next.liveMbps : current.uploadMbps,
      }));
      setMeasuredBytes(next.totalBytesTransferred);
    });

    const removeCompleted = desktopBridge.on<DiagnosticResult>('diagnostic.completed', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      highestProgress.current = 1;
      setProgressRatio(1);
      setResult(next);
      setCurrentResultDetail(next.detail);
      setMeasuredBytes((current) => next.dataUsedBytes ?? current);
      setProgress((current) => current ? { ...current, fraction: 1, overallFraction: 1, phase: 'complete', stage: 'complete', stageLabel: 'Complete', message: 'Complete', estimatedSecondsRemaining: 0 } : current);
      setRunning(false);
      setPendingRun(null);
      if (next.storedReport) {
        setReports((current) => [next.storedReport!, ...current.filter((item) => item.id !== next.storedReport!.id)]);
        setLatestSavedReport(next.detail);
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
    if (typeof window.matchMedia !== 'function') return;
    const query = window.matchMedia('(min-width: 1760px)');
    const update = () => setHistoryDocked(query.matches);
    update();
    query.addEventListener?.('change', update);
    return () => query.removeEventListener?.('change', update);
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
      if (loadLatest) {
        if (nextReports.length === 0) {
          setLatestSavedReport(null);
        } else {
          try {
            setLatestSavedReport(await loadReportDetail(nextReports[0].id));
          } catch {
            // History remains usable even if a single detail report cannot be opened.
          }
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

  async function loadRunConfiguration() {
    if (!desktopBridge.available) return;
    try {
      const [settings, choices] = await Promise.all([
        desktopBridge.request<AdvancedSettings>('settings.getAdvanced'),
        desktopBridge.request<InterfaceChoice[]>('diagnostic.interfaces'),
      ]);
      setAdvancedSettings(settings);
      setInterfaces(choices);
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
    setResultNotice(null);
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
    if (running) return;
    setResult(null);
    setCurrentResultDetail(null);
    if (next === profile) return;
    setProfile(next);
    setPendingRun(null);
    setError(null);
  }

  function selectMethod(next: TransferMethod) {
    if (running) return;
    setResult(null);
    setCurrentResultDetail(null);
    if (next === method) return;
    setMethod(next);
    setPendingRun(null);
    setError(null);
  }

  function selectDownloadPath(next: DownloadPathPreference) {
    if (running) return;
    setResult(null);
    setCurrentResultDetail(null);
    if (next === downloadPath) return;
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
        showWorkspace('run-diagnostics');
        return;
      }
      await startDiagnostic(nextProfile, nextMethod, nextPath);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The diagnostic plan could not be prepared.');
    }
  }

  async function startDiagnostic(nextProfile: DiagnosticProfile, nextMethod: TransferMethod, nextPath: DownloadPathPreference) {
    if (running) return;
    showWorkspace('run-diagnostics');
    if (nextProfile !== profile) setProfile(nextProfile);
    if (nextMethod !== method) setMethod(nextMethod);
    if (nextPath !== downloadPath) setDownloadPath(nextPath);
    activeProfile.current = nextProfile;
    activeMethod.current = nextMethod;
    activeDownloadPath.current = nextPath;
    highestProgress.current = 0;
    setError(null);
    setPendingRun(null);
    setLiveMetrics(emptyLiveMetrics);
    setResult(null);
    setCurrentResultDetail(null);
    setMeasuredBytes(0);
    setProgressRatio(0);
    setProgress({
      runId: '',
      phase: 'starting',
      stage: 'starting',
      stageLabel: 'Preparing',
      message: 'Preparing the measurement path…',
      fraction: 0,
      overallFraction: 0,
      stageIndex: 0,
      totalStages: plan?.totalTransferStages ? plan.totalTransferStages + (plan.deepDiagnostics ? 4 : 3) : 1,
      elapsedSeconds: 0,
      estimatedSecondsRemaining: plan?.estimatedSeconds ?? null,
      bytesTransferred: 0,
      totalBytesTransferred: 0,
    });
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

  function showWorkspace(id: WorkbenchSection) {
    setActiveSection(id);
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}#${id}`);
    window.requestAnimationFrame(() => {
      document.querySelector<HTMLElement>('.workspace-stage')?.scrollTo({ top: 0, behavior: 'smooth' });
      document.querySelector<HTMLElement>('main')?.scrollTo({ top: 0, behavior: 'smooth' });
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  function measureCapacity() {
    showWorkspace('run-diagnostics');
    void prepareDiagnostic('connection-check', 'aggregate', 'automatic');
  }

  function measurePeakCapacity() {
    showWorkspace('run-diagnostics');
    void prepareDiagnostic('stress', 'aggregate', 'automatic');
  }

  async function cancelDiagnostic() {
    try {
      await desktopBridge.request('diagnostic.cancel');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The diagnostic could not be cancelled.');
    }
  }

  async function saveCurrentResult() {
    if (!result || resultActionBusy) return;
    setResultActionBusy('save');
    setError(null);
    setResultNotice(null);
    try {
      const saved = await desktopBridge.request<{ saved: boolean; detail: SavedReportDetail }>('reports.saveCurrent', { id: result.reportId });
      setCurrentResultDetail(saved.detail);
      setLatestSavedReport(saved.detail);
      setResult((current) => current ? { ...current, savedLocally: true, storageError: null, storedReport: saved.detail.report } : current);
      setReports((current) => [saved.detail.report, ...current.filter((item) => item.id !== saved.detail.report.id)]);
      setResultNotice('Report saved locally.');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The report could not be saved.');
    } finally {
      setResultActionBusy(null);
    }
  }

  async function exportCurrentResult() {
    if (!result || resultActionBusy) return;
    setResultActionBusy('export');
    setError(null);
    setResultNotice(null);
    try {
      const exported = await desktopBridge.request<{ cancelled: boolean; fileName?: string }>('reports.exportCurrent', { id: result.reportId });
      if (!exported.cancelled) setResultNotice(`Exported ${exported.fileName || 'report JSON'}.`);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The report could not be exported.');
    } finally {
      setResultActionBusy(null);
    }
  }

  function configureNextRun() {
    setResult(null);
    setCurrentResultDetail(null);
    setResultNotice(null);
    setError(null);
  }

  function repeatDiagnostic() {
    setResult(null);
    setCurrentResultDetail(null);
    setResultNotice(null);
    window.requestAnimationFrame(() => void prepareDiagnostic());
  }

  const paletteCommands: PaletteCommand[] = [
    {
      id: 'workspace-health',
      title: 'Go to Live Network Health',
      detail: 'Current score, passive measurements, timeline, and capacity history.',
      keywords: 'monitor health timeline continuous live network',
      priority: 1,
      enabled: true,
      run: () => showWorkspace('live-network-health'),
    },
    {
      id: 'workspace-diagnostics',
      title: 'Go to Run Diagnostics',
      detail: 'Profiles, transfer topology, download path, native plan, and measurement path.',
      keywords: 'run diagnostics test connection quick full stress r2 worker',
      priority: 2,
      enabled: true,
      run: () => showWorkspace('run-diagnostics'),
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
      run: () => showWorkspace('advanced-diagnostics'),
    },
    {
      id: 'advanced-preflight',
      title: 'Run Native Preflight',
      detail: 'Validate the saved endpoint, route, interface, and protocol path without a throughput run.',
      keywords: 'advanced validate setup endpoint route interface protocol preflight',
      priority: 6,
      enabled: desktopBridge.available && !running,
      run: () => { showWorkspace('advanced-diagnostics'); setAdvancedPreflightRequest((current) => current + 1); },
    },
    {
      id: 'run-selected',
      title: running ? 'Diagnostic is already running' : `Run ${selectedProfile.title}`,
      detail: `${methodLabel(method)} · ${downloadPathLabel(downloadPath)}.`,
      keywords: 'run start diagnostic current selected test',
      priority: 6,
      enabled: desktopBridge.available && !running,
      run: () => { showWorkspace('run-diagnostics'); void prepareDiagnostic(); },
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
      run: () => { selectProfile(item.id); showWorkspace('run-diagnostics'); },
    } satisfies PaletteCommand)),
    ...methods.map((item, index) => ({
      id: `method-${item.id}`,
      title: `Use ${item.label} transfer topology`,
      detail: item.detail,
      keywords: `method transfer flow ${item.id} ${item.detail}`,
      priority: 30 + index,
      enabled: !running,
      run: () => { selectMethod(item.id); showWorkspace('run-diagnostics'); },
    } satisfies PaletteCommand)),
    ...downloadPaths.map((item, index) => ({
      id: `download-path-${item.id}`,
      title: `Use ${item.label} download path`,
      detail: item.detail,
      keywords: `download path r2 worker ${item.id}`,
      priority: 40 + index,
      enabled: !running,
      run: () => { selectDownloadPath(item.id); showWorkspace('run-diagnostics'); },
    } satisfies PaletteCommand)),
  ];

  const latestDiagnostic = latestSavedReport ? {
    profileName: latestSavedReport.report.profileName,
    generatedAt: latestSavedReport.report.generatedAt,
    outcome: latestSavedReport.presentation.outcome,
    label: latestSavedReport.presentation.label,
    verdict: latestSavedReport.presentation.verdict,
    summary: latestSavedReport.presentation.summary,
    nextAction: latestSavedReport.presentation.nextAction,
  } : null;

  const recommendedProfile = recommendationProfile(result, currentResultDetail);
  const technicalData = currentResultDetail?.presentation.technicalData ?? currentResultDetail?.presentation.technicalEvidence ?? [];

  return (
    <div className={`app-shell ${historyOpen && historyDocked ? 'history-docked' : ''}`}>
      <header className="product-bar">
        <button type="button" className="brand brand-home" onClick={() => showWorkspace('live-network-health')} aria-label="Go to Live Network Health">
          <span className="brand-mark" role="img" aria-label={shellHealthLabel} title={shellHealthLabel} />
          <div><strong>Network Diagnostics</strong><span>Desktop</span></div>
        </button>

        <nav className="workbench-nav" aria-label="Primary workspaces">
          <button type="button" className={activeSection === 'live-network-health' ? 'active' : ''} aria-current={activeSection === 'live-network-health' ? 'page' : undefined} onClick={() => showWorkspace('live-network-health')}>Health</button>
          <button type="button" className={activeSection === 'run-diagnostics' ? 'active' : ''} aria-current={activeSection === 'run-diagnostics' ? 'page' : undefined} onClick={() => showWorkspace('run-diagnostics')}>Diagnostics</button>
          <button type="button" className={activeSection === 'advanced-diagnostics' ? 'active' : ''} aria-current={activeSection === 'advanced-diagnostics' ? 'page' : undefined} onClick={() => showWorkspace('advanced-diagnostics')}>Advanced</button>
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
          <SettingsMenu appearance={appearance} onAppearanceChange={(next) => void changeAppearance(next)} onReportsChanged={() => void loadReports(true)} disabled={!desktopBridge.available} initialOpen={startupOptions.panel === 'settings'} />
        </div>
      </header>

      <main className="app-main">
        <div className="workspace-stage">
        <motion.div className={`workspace-pane ${activeSection === 'live-network-health' ? 'active' : ''}`} aria-hidden={activeSection !== 'live-network-health'} animate={workspaceAnimation(activeSection === 'live-network-health')}>
        <ContinuousDiagnostics
          snapshot={monitorSnapshot}
          loading={monitorLoading}
          error={monitorError}
          activeDiagnostic={running ? { profileName: profileTitle(activeProfile.current), phase: progress?.stageLabel || phaseLabel(progress?.phase) } : null}
          latestDiagnostic={latestDiagnostic}
          onUpdate={setMonitorSnapshot}
          onError={setMonitorError}
          onRunRecommended={() => { showWorkspace('run-diagnostics'); selectProfile('connection-check'); }}
          onOpenLatestReport={() => latestSavedReport && openHistory(latestSavedReport.report.id)}
          onMeasureCapacity={measureCapacity}
          onMeasurePeakCapacity={measurePeakCapacity}
        />
        </motion.div>

        <motion.div className={`workspace-pane ${activeSection === 'run-diagnostics' ? 'active' : ''}`} aria-hidden={activeSection !== 'run-diagnostics'} animate={workspaceAnimation(activeSection === 'run-diagnostics')}>
        <section id="run-diagnostics" className="workbench-section diagnostics-workbench" aria-labelledby="run-diagnostics-title">
          <header className="workspace-heading diagnostics-heading">
            <div>
              <h1 id="run-diagnostics-title">Run Diagnostics</h1>
            </div>
            <div className="workspace-heading-status" aria-live="polite">
              <span>{running ? 'RUN IN PROGRESS' : result ? 'LATEST RESULT' : 'NATIVE ENGINE READY'}</span>
              <strong>{running ? progress?.stageLabel || phaseLabel(progress?.phase) : result ? displayedProfile.title : selectedProfile.title}</strong>
            </div>
          </header>

          {error && <div className="error-banner" role="alert"><strong>Diagnostic status</strong><span>{error}</span><button type="button" aria-label="Dismiss status" onClick={() => setError(null)}>×</button></div>}

          {!running && !result && (
            <>
              {advancedStatus.hasOverrides && (
                <div className="advanced-config-banner">
                  <div><span aria-hidden="true">CUSTOM</span><div><strong>Non-default native configuration</strong><small>{advancedStatus.summary}</small></div></div>
                  <div><button type="button" onClick={() => showWorkspace('advanced-diagnostics')}>Review</button><button type="button" onClick={() => setAdvancedResetRequest((current) => current + 1)}>Reset</button></div>
                </div>
              )}

              <div className="diagnostic-setup-grid">
                <section className="setup-console" aria-label="Diagnostic controls">
                  <div className="console-heading">
                    <div><span>RUN CONFIGURATION</span><strong>{selectedProfile.title}</strong></div>
                    <span>{plan ? formatDuration(plan.estimatedSeconds) : 'Reading plan…'}</span>
                  </div>

                  <fieldset className="console-control profile-fieldset">
                    <legend>Profile</legend>
                    <div className="segmented-control profile-control" aria-label="Diagnostic profile">
                      {profiles.map((item) => (
                        <button key={item.id} type="button" aria-pressed={profile === item.id} className={profile === item.id ? 'active' : ''} onClick={() => selectProfile(item.id)}>
                          <strong>{item.label}</strong>
                        </button>
                      ))}
                    </div>
                    <p>{selectedProfile.description}</p>
                  </fieldset>

                  <div className="console-control-pair">
                    <fieldset className="console-control topology-control">
                      <legend>Topology <small>{methods.find((item) => item.id === method)?.detail}</small></legend>
                      <div className="segmented-control compact" aria-label="Transfer topology">
                        {methods.map((item) => (
                          <button key={item.id} type="button" aria-pressed={method === item.id} className={method === item.id ? 'active' : ''} onClick={() => selectMethod(item.id)}>{item.label}</button>
                        ))}
                      </div>
                    </fieldset>

                    <fieldset className="console-control download-path-control">
                      <legend>Download path <small>{selectedPath.detail}</small></legend>
                      <div className="segmented-control compact" aria-label="Download measurement path">
                        {downloadPaths.map((item) => (
                          <button key={item.id} type="button" aria-pressed={downloadPath === item.id} className={downloadPath === item.id ? 'active' : ''} onClick={() => selectDownloadPath(item.id)}>{item.label}</button>
                        ))}
                      </div>
                    </fieldset>
                  </div>

                  <div className="console-control interface-control">
                    <div className="control-label-row"><label htmlFor="run-interface">Network interface</label><span>{interfaces.length} detected</span></div>
                    <div className="interface-row diagnostic-interface-row">
                      <select id="run-interface" data-interface-picker aria-label="Network interface" value={advancedSettings?.interfaceId ?? ''} disabled={!advancedSettings || running} onChange={(event) => void selectInterface(event.target.value)}>
                        <option value="">Automatic routing · System default route</option>
                        {interfaces.map((choice, index) => {
                          const id = interfaceId(choice);
                          if (!id) return null;
                          return <option key={id + '-' + index} value={id}>{interfaceLabel(choice, id)}</option>;
                        })}
                        {advancedSettings?.interfaceId && !interfaces.some((choice) => interfaceId(choice) === advancedSettings.interfaceId) && <option value={advancedSettings.interfaceId}>{advancedSettings.interfaceId}</option>}
                      </select>
                    </div>
                    <small>Explicit binding applies to supported HTTP and LAN sockets; system-routed probes remain identified in the report.</small>
                  </div>

                  {pendingRun ? (
                    <div className="high-data-confirm" role="alert">
                      <div><span>TRANSFER CONFIRMATION</span><strong>Up to {formatBytes(pendingRun.plan.transferCapBytes)}</strong><p>{profileTitle(pendingRun.profile)} · {formatDuration(pendingRun.plan.estimatedSeconds)} · {methodLabel(pendingRun.method)} · {downloadPathLabel(pendingRun.downloadPath)}</p></div>
                      <div><button type="button" className="secondary-action" onClick={() => setPendingRun(null)}>Cancel</button><button type="button" className="primary-action" onClick={() => void startDiagnostic(pendingRun.profile, pendingRun.method, pendingRun.downloadPath)}>Confirm run</button></div>
                    </div>
                  ) : (
                    <div className="run-command">
                      <div>
                        <span>READY TO RUN</span>
                        <strong>{plan ? formatBytes(plan.transferCapBytes) + ' maximum transfer' : 'Loading native plan'}</strong>
                        <small>{methodLabel(method)} · {downloadPathLabel(downloadPath)} · report saved locally</small>
                      </div>
                      <button type="button" className="primary-action diagnostic-run-button" onClick={() => void prepareDiagnostic()} disabled={!desktopBridge.available || !plan}>
                        <span aria-hidden="true">▶</span> Run {selectedProfile.label}
                      </button>
                    </div>
                  )}
                </section>

                <section className="run-intelligence" aria-label="Native run plan and measurement path">
                  <div className="instrument-columns">
                    <section className="instrument-section run-plan-section">
                      <header><div><span>RUN PLAN</span><strong>{selectedProfile.label}</strong></div><small>{plan ? plan.totalTransferStages + ' transfer stages' : 'Loading'}</small></header>
                      <div className="plan-fact-grid">
                        <PlanFact label="Runtime" value={plan ? formatDuration(plan.estimatedSeconds) : '—'} />
                        <PlanFact label="Internet transfer cap" value={plan ? formatBytes(plan.internetTransferCapBytes) : '—'} />
                        <PlanFact label="Baseline" value={plan ? plan.idlePingCount + ' × ' + plan.pingIntervalMs + ' ms' : '—'} />
                        <PlanFact label="Download runs" value={plan ? String(plan.downloadRuns) : '—'} />
                        <PlanFact label="Service checks" value={plan ? plan.serviceCheckCount ? `${plan.serviceCheckCount} endpoints` : 'Off' : '—'} />
                        <PlanFact label="Diagnostic depth" value={plan?.diagnosticDepth ?? '—'} />
                        {plan?.lanEnabled && <PlanFact label="LAN measurement" value={`${plan.lanConnections} streams · ${formatDuration(plan.lanEstimatedSeconds)}`} />}
                        {plan?.lanEnabled && <PlanFact label="LAN target" value={`${plan.lanTarget}:${plan.lanPort}`} />}
                      </div>
                      <div className="stage-plan">
                        <span>CONNECTION STAGES</span>
                        <PlanStages plan={plan} />
                      </div>
                    </section>

                    <section className="instrument-section path-section">
                      <header><div><span>MEASUREMENT PATH</span><strong>{preflightLoading ? 'Checking route' : preflightSummary.endpoint}</strong></div><small>{preflightLoading ? 'LIVE PREFLIGHT' : 'CURRENT'}</small></header>
                      <dl className="path-ledger">
                        <PathRow label="Endpoint" value={preflightLoading ? 'Selecting…' : preflightSummary.origin} copyable={!preflightLoading} />
                        <PathRow label="Provider / network" value={preflightSummary.providerNetwork} />
                        <PathRow label="Location / protocol" value={[preflightSummary.edge, preflightSummary.protocol, preflightSummary.ipVersion].filter((value) => value !== '—').join(' · ') || '—'} />
                        <PathRow label="Preflight latency" value={preflightSummary.latency} />
                        <PathRow label="Interface" value={resultPathSummary.interface} />
                        <PathRow label="Requested delivery" value={downloadPathLabel(downloadPath)} />
                        <PathRow label="Actual preflight path" value={preflight?.downloadPath ? downloadPathResultLabel(preflight.downloadPath.selectedPath) : preflightLoading ? 'Checking…' : '—'} accent />
                        <PathRow label="R2 probe" value={preflight?.downloadPath ? statusLabel(preflight.downloadPath.r2ProbeStatus) : downloadPath === 'worker' ? 'Not requested' : 'Pending'} />
                      </dl>
                      {preflight?.downloadPath?.fallbackReason && <p className="path-callout">{preflight.downloadPath.fallbackReason}</p>}
                      {preflightError && <p className="path-callout error">{preflightError}</p>}
                    </section>
                  </div>

                  <section className="measurements-manifest">
                    <header><div><span>MEASUREMENTS THIS PROFILE COLLECTS</span><strong>{dataCollectionLevel(profile)} depth</strong></div><small>{plan?.lanEnabled ? 'Internet + native system + LAN' : 'Internet + native system'}</small></header>
                    <div className="measurement-groups">
                      {dataCollectedFor(profile, method, !!plan?.lanEnabled).map((group) => (
                        <div className="measurement-group" key={group.label}><span>{group.label}</span><ul>{group.items.map((item) => <li key={item}>{item}</li>)}</ul></div>
                      ))}
                    </div>
                  </section>
                </section>
              </div>
            </>
          )}

          {running && (
            <motion.div className="active-run-instrument" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
              <header className="active-run-header">
                <div>
                  <span className="run-status-line"><i className="pulse" aria-hidden="true" />CONTROLLED LOAD · {profileTitle(activeProfile.current).toUpperCase()}</span>
                  <h2>{progress?.message || 'Preparing the measurement path…'}</h2>
                  <p>{progress?.stageLabel || phaseLabel(progress?.phase)} · stage {progress?.stageIndex ?? 0} of {progress?.totalStages ?? '—'} · {methodLabel(activeMethod.current)} · {downloadPathLabel(activeDownloadPath.current)}</p>
                </div>
                <button type="button" className="secondary-action danger-action" onClick={() => void cancelDiagnostic()}>Cancel run</button>
              </header>

              <div className="active-progress">
                <div className="active-progress-value"><strong>{progressPercent}</strong><span>%</span></div>
                <div><div className="diagnostic-progress-track" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={progressPercent}><span style={{ width: progressPercent + '%' }} /></div><small>{formatBytes(measuredBytes)} measured · {plan ? formatBytes(plan.transferCapBytes) : '—'} cap · {formatDuration(progress?.elapsedSeconds ?? 0)} elapsed{progress?.estimatedSecondsRemaining != null && progress.estimatedSecondsRemaining > 0 ? ` · about ${formatDuration(progress.estimatedSecondsRemaining)} remaining` : ''}</small></div>
              </div>

              <RunPhaseRail phase={progress?.phase} plan={plan} />

              <div className="active-run-grid">
                <section className="live-readings">
                  <header><span>LIVE MEASUREMENTS</span><small>Native progress samples</small></header>
                  <div className="live-reading-grid">
                    <Metric label="Latency" value={metric(liveMetrics.latencyMs, 'ms')} detail="Latest baseline" />
                    <Metric label="Download" value={metric(liveMetrics.downloadMbps, 'Mbps')} detail="Latest throughput" />
                    <Metric label="Upload" value={metric(liveMetrics.uploadMbps, 'Mbps')} detail="Latest throughput" />
                    <Metric label="Payload" value={formatBytes(measuredBytes)} detail="Transferred so far" />
                  </div>
                </section>

                <section className="active-route">
                  <header><span>ACTIVE PATH</span><small>{preflightSummary.latency} preflight</small></header>
                  <dl className="path-ledger">
                    <PathRow label="Endpoint" value={preflightSummary.endpoint} copyable />
                    <PathRow label="Network / edge" value={[preflightSummary.providerNetwork, preflightSummary.edge].filter((value) => value !== '—').join(' · ') || '—'} />
                    <PathRow label="Interface" value={activeInterfaceLabel(advancedSettings, interfaces)} />
                    <PathRow label="Delivery request" value={downloadPathLabel(activeDownloadPath.current)} accent />
                  </dl>
                </section>
              </div>

              <section className="active-stage-budget">
                <header><span>STAGE BUDGET</span><small>{plan?.totalTransferStages ?? '—'} transfer stages · {plan?.downloadRuns ?? '—'} download runs</small></header>
                <PlanStages plan={plan} activePhase={progress?.phase} activeStage={progress?.stage} />
              </section>
            </motion.div>
          )}

          {!running && result && (
            <motion.div key={result.reportId} className="completed-run" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
              <header className="completed-run-header">
                <div>
                  <span className={'result-outcome ' + (currentResultDetail?.presentation.outcome ?? 'complete')}>{currentResultDetail?.presentation.label || 'COMPLETE'}</span>
                  <h2>{currentResultDetail?.presentation.verdict || displayedProfile.title + ' complete'}</h2>
                  <p>{currentResultDetail?.presentation.summary || 'The native measurement completed.'}</p>
                  <small>{new Date(result.generatedAt).toLocaleString()} · {methodLabel(result.method)} · {result.savedLocally ? 'Saved locally' : 'Report not saved'} · {formatBytes(result.dataUsedBytes ?? 0)} transferred</small>
                </div>
                <div className="diagnostic-result-actions">
                  <button type="button" className="secondary-action" onClick={configureNextRun}>Configure new run</button>
                  <button type="button" className="secondary-action" onClick={() => void exportCurrentResult()} disabled={resultActionBusy !== null}>{resultActionBusy === 'export' ? 'Exporting…' : 'Export JSON'}</button>
                  {result.savedLocally && <button type="button" className="secondary-action" onClick={() => openHistory(result.reportId)}>Open saved report</button>}
                  <button type="button" className="primary-action" onClick={repeatDiagnostic} disabled={!desktopBridge.available}>Run again</button>
                </div>
              </header>

              {!result.savedLocally && <div className="result-storage-warning" role="alert"><div><strong>Report is only in this session</strong><span>{result.storageError || 'The local report write did not complete.'}</span></div><button type="button" className="secondary-action" onClick={() => void saveCurrentResult()} disabled={resultActionBusy !== null}>{resultActionBusy === 'save' ? 'Saving…' : 'Retry save'}</button></div>}
              {resultNotice && <div className="result-success-notice" role="status">{resultNotice}</div>}

              <div className="result-kpi-band" aria-label="Primary result measurements">
                <Metric label="Latency" value={metric(result.latencyMs, 'ms')} detail="Median response" />
                <Metric label="Download" value={metric(result.downloadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Upload" value={metric(result.uploadMbps, 'Mbps')} detail="Steady throughput" />
                <Metric label="Request loss" value={metric(result.requestLossPercent, '%')} detail="First-party requests" />
              </div>

              {currentResultDetail ? (
                <>
                  <div className="result-analysis-grid">
                    <section className="findings-section">
                      <header><div><span>FINDINGS</span><strong>{currentResultDetail.presentation.findings.length} reported</strong></div><small>Native classifier</small></header>
                      <div className="findings-list">
                        {currentResultDetail.presentation.findings.map((finding, index) => (
                          <article className="finding-row" key={finding.title + '-' + index}>
                            <span>{String(index + 1).padStart(2, '0')} · {finding.label}</span>
                            <div><strong>{finding.title}</strong><p>{finding.summary}</p></div>
                          </article>
                        ))}
                      </div>
                    </section>

                    <aside className="result-route-section">
                      <header><div><span>ACTUAL MEASUREMENT PATH</span><strong>{resultPathSummary.endpoint}</strong></div><small>{resultPathSummary.latency}</small></header>
                      <dl className="path-ledger">
                        <PathRow label="Endpoint" value={resultPathSummary.origin} copyable />
                        <PathRow label="Provider / network" value={resultPathSummary.providerNetwork} />
                        <PathRow label="Location / protocol" value={[resultPathSummary.edge, resultPathSummary.protocol, resultPathSummary.ipVersion].filter((value) => value !== '—').join(' · ') || '—'} />
                        <PathRow label="TLS / HTTP3" value={[resultPathSummary.tls, resultPathSummary.http3].filter((value) => value !== '—').join(' · ') || '—'} />
                        <PathRow label="Interface" value={activeInterfaceLabel(advancedSettings, interfaces)} />
                      </dl>
                      <DownloadDeliveryRows delivery={result.downloadDelivery ?? currentResultDetail.downloadDelivery ?? null} />
                      <div className="next-action-block">
                        <span>NEXT STEP</span>
                        <strong>{currentResultDetail.presentation.nextAction || 'No additional diagnostic is required.'}</strong>
                        {recommendedProfile && <button type="button" className="inline-action" onClick={() => { configureNextRun(); selectProfile(recommendedProfile); }}>Configure {profileTitle(recommendedProfile)}</button>}
                      </div>
                    </aside>
                  </div>

                  <section className="measurement-ledger">
                    <header><div><span>MEASUREMENTS</span><strong>{currentResultDetail.presentation.metrics.filter((item) => item.wasMeasured).length} measured</strong></div><small>{displayedProfile.title} · schema 2.0 report</small></header>
                    <div className="measurement-ledger-grid">
                      {currentResultDetail.presentation.metrics.map((item, index) => (
                        <div className={'measurement-entry ' + (item.wasMeasured ? '' : 'not-measured')} key={item.label + '-' + index}>
                          <span>{item.label}</span><strong>{item.value}</strong><small>{item.detail}</small>
                        </div>
                      ))}
                    </div>
                  </section>

                  {technicalData.length > 0 && <details className="technical-data-panel"><summary><span>TECHNICAL DATA</span><strong>{technicalData.length} items</strong><small>Native context and report identifiers</small></summary><ul>{technicalData.map((item, index) => <li key={index + '-' + item}>{item}</li>)}</ul></details>}
                  {currentResultDetail.technicalReport && <details className="technical-data-panel raw-report-panel"><summary><span>SCHEMA 2.0 REPORT</span><strong>Full technical data</strong><small>Raw native measurements</small></summary><pre>{JSON.stringify(currentResultDetail.technicalReport, null, 2)}</pre></details>}
                </>
              ) : <div className="result-loading"><span className="monitor-loader" /><strong>Loading native findings and measurements</strong></div>}
            </motion.div>
          )}
        </section>
        </motion.div>

        <motion.div className={`workspace-pane ${activeSection === 'advanced-diagnostics' ? 'active' : ''}`} aria-hidden={activeSection !== 'advanced-diagnostics'} animate={workspaceAnimation(activeSection === 'advanced-diagnostics')}>
        {activeSection === 'advanced-diagnostics' && <AdvancedDiagnostics
          key={`advanced-${advancedSyncKey}`}
          profile={profile}
          method={method}
          downloadPath={downloadPath}
          initialSettings={advancedSettings}
          initialInterfaces={interfaces}
          initialPreflight={preflight}
          onSettingsChange={setAdvancedSettings}
          onPreflightChange={(next) => { setPreflight(next); if (next.interfaces?.length) setInterfaces(next.interfaces); }}
          onStatusChange={setAdvancedStatus}
          initialTool={startupOptions.advancedTool}
          resetRequest={advancedResetRequest}
          preflightRequest={advancedPreflightRequest}
        />}
        </motion.div>
        </div>
      </main>

      {running && activeSection !== 'run-diagnostics' && (
        <button type="button" className="sticky-run-status" onClick={() => showWorkspace('run-diagnostics')}><span className="pulse" aria-hidden="true" /><strong>{profileTitle(activeProfile.current)}</strong><small>{progress?.stageLabel || phaseLabel(progress?.phase)} · {progressPercent}%</small><b>View</b></button>
      )}

      <AlertsPanel open={alertsOpen} snapshot={monitorSnapshot} onUpdate={setMonitorSnapshot} onClose={() => setAlertsOpen(false)} />
      <HistoryPanel
        open={historyOpen}
        reports={reports}
        loading={historyLoading}
        error={historyError}
        initialReportId={historyInitialReportId}
        docked={historyDocked}
        onClose={() => { setHistoryOpen(false); setHistoryInitialReportId(null); }}
        onRefresh={() => void loadReports()}
        onDeleted={(reportId) => {
          if (currentResultDetail?.report.id !== reportId) return;
          setResult((current) => current ? { ...current, savedLocally: false, storageError: 'The saved copy was deleted from local storage.' } : current);
          setCurrentResultDetail((current) => current ? { ...current, report: { ...current.report, savedLocally: false } } : current);
        }}
      />
      <CommandPalette open={paletteOpen} commands={paletteCommands} onClose={() => setPaletteOpen(false)} />
    </div>
  );
}

function readStartupOptions(): StartupOptions {
  const query = new URLSearchParams(window.location.search);
  const appearanceValue = query.get('appearance');
  const appearance = appearanceValue && ['system', 'light', 'dark'].includes(appearanceValue)
    ? appearanceValue as AppearanceMode
    : null;
  const profile = allowedStartupValue(query.get('profile'), ['connection-check', 'quick', 'full', 'stress'] as const, 'connection-check');
  const method = allowedStartupValue(query.get('method'), ['compare', 'single', 'aggregate'] as const, 'compare');
  const downloadPath = allowedStartupValue(query.get('download-path'), ['automatic', 'direct-r2', 'worker'] as const, 'automatic');
  const panelValue = query.get('panel');
  const panel = panelValue && ['history', 'alerts', 'settings'].includes(panelValue) ? panelValue as StartupPanel : null;
  const advancedTool = allowedStartupValue(query.get('advanced-tool'), ['configuration', 'lan'] as const, 'configuration');
  const runConnectionCheck = query.get('run') === 'connection-check';
  const hash = window.location.hash.replace(/^#/, '');
  const workspace = ['live-network-health', 'run-diagnostics', 'advanced-diagnostics'].includes(hash)
    ? hash as WorkbenchSection
    : runConnectionCheck ? 'run-diagnostics' : 'live-network-health';
  return { appearance, profile, method, downloadPath, workspace, panel, advancedTool, runConnectionCheck };
}

function allowedStartupValue<T extends string>(value: string | null, allowed: readonly T[], fallback: T): T {
  return value && allowed.includes(value as T) ? value as T : fallback;
}

function Metric({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <div className="metric"><span>{label}</span><strong>{value}</strong><small>{detail}</small></div>;
}

function PlanFact({ label, value }: { label: string; value: string }) {
  return <div className="plan-fact"><span>{label}</span><strong>{value}</strong></div>;
}

function PathRow({ label, value, accent = false, copyable = false }: { label: string; value: string; accent?: boolean; copyable?: boolean }) {
  const canCopy = copyable && value !== '—' && value.length > 0;
  return <div className={accent ? 'path-row accent' : 'path-row'}><dt>{label}</dt><dd title={value}><span>{value}</span>{canCopy && <button type="button" className="path-copy" aria-label={`Copy ${label.toLowerCase()}`} onClick={() => void navigator.clipboard?.writeText(value)}>Copy</button>}</dd></div>;
}

function workspaceAnimation(active: boolean) {
  return active ? { opacity: 1, y: 0 } : { opacity: 0, y: 6 };
}

function PlanStages({ plan, activePhase, activeStage }: { plan: DiagnosticPlan | null; activePhase?: string; activeStage?: string }) {
  if (!plan) return <span className="stage-plan-empty">Reading connection stages…</span>;
  const stages = [...plan.downloadStages, ...plan.uploadStages];
  const activeIndex = stages.findIndex((stage) => stage.direction === activePhase && stage.id === activeStage);
  const transfersComplete = activePhase === 'diagnostics' || activePhase === 'finalize' || activePhase === 'complete';
  return (
    <div className="stage-chip-list">
      {stages.map((stage, index) => {
        const done = transfersComplete || activeIndex > index;
        const active = activeIndex === index;
        return (
          <div className={'stage-chip ' + (done ? 'done' : active ? 'active' : '')} key={stage.direction + '-' + stage.id + '-' + index}>
            <span>{stage.direction === 'download' ? '↓' : '↑'}</span>
            <div><strong>{stage.connections}× {stage.strategy}</strong><small>{formatDuration(stage.durationMs / 1000)} · {formatBytes(stage.capBytes)} · {stage.samples} sample{stage.samples === 1 ? '' : 's'}</small></div>
          </div>
        );
      })}
    </div>
  );
}

function RunPhaseRail({ phase, plan }: { phase?: string; plan: DiagnosticPlan | null }) {
  const current = phase === 'complete' || phase === 'finalize' || phase === 'diagnostics' ? 4
    : phase === 'upload' ? 3
      : phase === 'download' ? 2
        : phase === 'idle' ? 1
          : 0;
  const phases = [
    ['Path', 'Endpoint + route'],
    ['Baseline', plan ? plan.idlePingCount + ' samples' : 'Latency samples'],
    ['Download', plan ? plan.downloadStages.length + ' stages' : 'Transfer stages'],
    ['Upload', plan ? plan.uploadStages.length + ' stages' : 'Transfer stages'],
    ['Analysis', plan?.deepDiagnostics ? 'Full native suite' : 'Core findings'],
  ];
  return <ol className="run-phase-rail" aria-label="Diagnostic phases">{phases.map(([label, detail], index) => <li key={label} className={index < current ? 'done' : index === current ? 'active' : ''}><i>{index < current ? '✓' : index + 1}</i><div><strong>{label}</strong><span>{detail}</span></div></li>)}</ol>;
}

function DownloadDeliveryRows({ delivery }: { delivery: DownloadDelivery | null }) {
  if (!delivery) return null;
  return (
    <dl className="delivery-ledger">
      <PathRow label="Requested delivery" value={downloadPathResultLabel(delivery.requestedPath)} />
      <PathRow label="Actual delivery" value={downloadPathResultLabel(delivery.selectedPath)} accent />
      <PathRow label="R2 probe" value={statusLabel(delivery.r2ProbeStatus)} />
      <PathRow label="Requests" value={delivery.requestsCompleted + ' / ' + delivery.requestsStarted + ' completed · ' + delivery.r2Requests + ' R2 · ' + delivery.workerRequests + ' Worker'} />
      {delivery.fallbackReason && <div className="delivery-fallback"><dt>Fallback</dt><dd>{delivery.fallbackReason}</dd></div>}
    </dl>
  );
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
function phaseLabel(phase?: string): string {
  switch (phase) {
    case 'preflight': return 'Measurement path';
    case 'idle': return 'Baseline latency';
    case 'download': return 'Download measurement';
    case 'upload': return 'Upload measurement';
    case 'diagnostics': return 'System and path diagnostics';
    case 'finalize': return 'Findings and report';
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

function summarizePreflight(measurement: unknown): MeasurementPathSummary {
  const empty: MeasurementPathSummary = { endpoint: 'Not checked', origin: '—', providerNetwork: '—', edge: '—', latency: '—', interface: 'Automatic routing', protocol: '—', ipVersion: '—', tls: '—', http3: '—' };
  if (!measurement || typeof measurement !== 'object') return empty;
  const selected = findObjectWithKeys(measurement, ['origin', 'preflightLatencyMs']) ?? findObjectWithKeys(measurement, ['origin', 'name']);
  const networkRecord = findObjectWithKeys(measurement, ['edge', 'network']) ?? findObjectWithKeys(measurement, ['protocol', 'ipVersion']);
  const interfaceRecord = findObjectWithKeys(measurement, ['id', 'name', 'type']) ?? findObjectWithKeys(measurement, ['name', 'description', 'type']);
  const http3Record = findObjectWithKeys(measurement, ['attempted', 'supported']);
  const endpoint = selected ? stringValue(selected, ['name', 'origin', 'id']) ?? 'Selected endpoint' : findString(measurement, ['origin', 'endpoint']) ?? 'Selected endpoint';
  const origin = selected ? stringValue(selected, ['origin']) : findString(measurement, ['origin']);
  const provider = selected ? stringValue(selected, ['provider']) : null;
  const network = networkRecord ? stringValue(networkRecord, ['network', 'isp', 'asnName']) : findString(measurement, ['network', 'isp', 'asnName']);
  const edge = networkRecord ? stringValue(networkRecord, ['edge', 'location', 'colo', 'city']) : findString(measurement, ['edge', 'location', 'colo', 'city']);
  const latencyValue = selected ? numberValue(selected, ['preflightLatencyMs', 'latencyMs', 'medianLatencyMs']) : findNumber(measurement, ['preflightLatencyMs', 'latencyMs', 'medianLatencyMs']);
  const interfaceName = interfaceRecord ? stringValue(interfaceRecord, ['displayName', 'name', 'description']) : null;
  const interfaceType = interfaceRecord ? stringValue(interfaceRecord, ['type', 'interfaceType']) : null;
  const http3Supported = http3Record?.supported;
  return {
    endpoint,
    origin: origin ?? '—',
    providerNetwork: [provider, network].filter(Boolean).join(' · ') || '—',
    edge: edge ?? '—',
    latency: latencyValue == null ? '—' : `${formatNumber(latencyValue)} ms`,
    interface: [interfaceName, interfaceType].filter(Boolean).join(' · ') || 'Automatic routing',
    protocol: networkRecord ? stringValue(networkRecord, ['protocol']) ?? '—' : '—',
    ipVersion: networkRecord ? stringValue(networkRecord, ['ipVersion']) ?? '—' : '—',
    tls: networkRecord ? stringValue(networkRecord, ['tlsVersion']) ?? '—' : '—',
    http3: typeof http3Supported === 'boolean' ? http3Supported ? 'HTTP/3 available' : 'HTTP/3 unavailable' : '—',
  };
}

function interfaceId(choice: InterfaceChoice): string | null { return stringValue(choice, ['id', 'interfaceId', 'adapterId', 'name']); }
function interfaceLabel(choice: InterfaceChoice, fallback: string): string {
  const name = stringValue(choice, ['displayName', 'name', 'interfaceName']) ?? fallback;
  const description = stringValue(choice, ['description']);
  const type = stringValue(choice, ['type', 'interfaceType']);
  const speed = numberValue(choice, ['linkSpeedMbps']);
  const families = [choice.supportsIpv4 === true ? 'IPv4' : null, choice.supportsIpv6 === true ? 'IPv6' : null].filter(Boolean).join(' + ');
  const speedLabel = speed == null ? null : speed >= 1000 ? `${formatNumber(speed / 1000)} Gbps link` : `${formatNumber(speed)} Mbps link`;
  return [name, description && description !== name ? description : null, type, speedLabel, families].filter(Boolean).join(' · ');
}
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

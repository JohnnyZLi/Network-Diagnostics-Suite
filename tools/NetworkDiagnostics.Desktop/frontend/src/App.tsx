import { useEffect, useMemo, useRef, useState } from 'react';
import { desktopBridge } from './bridge';

type TransferMethod = 'compare' | 'single' | 'aggregate';

type HostInfo = {
  product: string;
  host: string;
  version?: string | null;
  platform: string;
  architecture: string;
};

type DiagnosticPlan = {
  profile: string;
  profileName: string;
  method: TransferMethod;
  transferCapBytes: number;
  downloadStages: number;
  uploadStages: number;
};

type RunAccepted = {
  runId: string;
  profile: string;
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
  profile: string;
  method: TransferMethod;
  latencyMs?: number | null;
  requestLossPercent?: number | null;
  downloadMbps?: number | null;
  uploadMbps?: number | null;
  dataUsedBytes?: number | null;
};

type DiagnosticFailure = {
  runId: string;
  message: string;
  errorType: string;
};

const methods: Array<{ id: TransferMethod; label: string; detail: string }> = [
  { id: 'compare', label: 'Compare', detail: 'Single + aggregate' },
  { id: 'single', label: 'Single', detail: 'One transfer flow' },
  { id: 'aggregate', label: 'Aggregate', detail: 'Parallel flows' },
];

function App() {
  const [host, setHost] = useState<HostInfo | null>(null);
  const [method, setMethod] = useState<TransferMethod>('compare');
  const [plan, setPlan] = useState<DiagnosticPlan | null>(null);
  const [progress, setProgress] = useState<DiagnosticProgress | null>(null);
  const [result, setResult] = useState<DiagnosticResult | null>(null);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const activeRunId = useRef<string | null>(null);

  useEffect(() => {
    if (!desktopBridge.available) return;
    void desktopBridge.request<HostInfo>('app.ready')
      .then(setHost)
      .catch((value: Error) => setError(value.message));
  }, []);

  useEffect(() => {
    if (!desktopBridge.available) return;
    void desktopBridge.request<DiagnosticPlan>('diagnostic.describePlan', {
      profile: 'connection-check',
      method,
    }).then(setPlan).catch((value: Error) => setError(value.message));
  }, [method]);

  useEffect(() => {
    const removeProgress = desktopBridge.on<DiagnosticProgress>('diagnostic.progress', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      setProgress(next);
    });
    const removeCompleted = desktopBridge.on<DiagnosticResult>('diagnostic.completed', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      setResult(next);
      setProgress((current) => current ? { ...current, fraction: 1, phase: 'complete' } : current);
      setRunning(false);
    });
    const removeCancelled = desktopBridge.on<{ runId: string }>('diagnostic.cancelled', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      setRunning(false);
      setProgress(null);
      setError('Connection Check was cancelled.');
    });
    const removeFailed = desktopBridge.on<DiagnosticFailure>('diagnostic.failed', (next) => {
      if (activeRunId.current && next.runId !== activeRunId.current) return;
      activeRunId.current = null;
      setRunning(false);
      setProgress(null);
      setError(next.message);
    });

    return () => {
      removeProgress();
      removeCompleted();
      removeCancelled();
      removeFailed();
    };
  }, []);

  const progressPercent = useMemo(() => Math.round(overallProgress(progress) * 100), [progress]);
  const livePrimary = progress?.liveMbps != null
    ? `${formatNumber(progress.liveMbps)} Mbps`
    : progress?.liveLatencyMs != null
      ? `${formatNumber(progress.liveLatencyMs)} ms`
      : running
        ? 'Measuring'
        : result
          ? 'Complete'
          : 'Ready';

  async function runDiagnostic() {
    setError(null);
    setResult(null);
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
        profile: 'connection-check',
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
        <div className={`host-state ${host ? 'connected' : ''}`}>
          <span className="status-dot" aria-hidden="true" />
          {host ? `Native engine · ${host.architecture}` : desktopBridge.available ? 'Connecting to engine' : 'Browser preview'}
        </div>
      </header>

      <main>
        <section className="intro-row">
          <div>
            <span className="eyebrow">Diagnostics</span>
            <h1>Connection Check</h1>
            <p>Fast first-party measurements for latency, loss, download, and upload—without turning the screen into a table.</p>
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
                <small>{running ? `${progressPercent}%` : result ? 'Connection Check' : 'Native test'}</small>
              </div>
            </div>

            <div className="run-copy">
              <div className="run-status-line">
                <span className={running ? 'pulse' : ''} aria-hidden="true" />
                {running ? phaseLabel(progress?.phase) : result ? 'Measurement complete' : 'Ready to measure'}
              </div>
              <h2>{running ? progress?.message || 'Preparing the test…' : result ? 'Your connection check is ready.' : 'Measure the connection you are on now.'}</h2>
              <p>
                {running
                  ? 'The same C# diagnostics engine used by the existing desktop and CLI is running behind this Photino shell.'
                  : result
                    ? `Completed ${new Date(result.generatedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}. Report ${result.reportId.slice(0, 8)} is available in memory.`
                    : 'Connection Check has the lowest transfer ceiling and does not collect local interface identifiers.'}
              </p>

              <div className="actions">
                <button
                  type="button"
                  className="primary-action"
                  onClick={() => void runDiagnostic()}
                  disabled={running || !desktopBridge.available}
                >
                  {result ? 'Run again' : 'Run connection check'}
                </button>
                {running && (
                  <button type="button" className="secondary-action" onClick={() => void cancelDiagnostic()}>
                    Cancel
                  </button>
                )}
              </div>
            </div>
          </div>

          <div className="metric-strip" aria-label="Connection metrics">
            <Metric label="Latency" value={metric(result?.latencyMs ?? progress?.liveLatencyMs, 'ms')} detail="Median / live" />
            <Metric label="Download" value={metric(result?.downloadMbps, 'Mbps')} liveValue={progress?.phase === 'download' ? metric(progress.liveMbps, 'Mbps') : undefined} detail="Steady throughput" />
            <Metric label="Upload" value={metric(result?.uploadMbps, 'Mbps')} liveValue={progress?.phase === 'upload' ? metric(progress.liveMbps, 'Mbps') : undefined} detail="Steady throughput" />
            <Metric label="Request loss" value={metric(result?.requestLossPercent, '%')} detail="First-party requests" />
          </div>
        </section>

        <section className="detail-row">
          <div className="plan-card">
            <span className="section-kicker">Current test plan</span>
            <div className="plan-main">
              <strong>{plan?.profileName || 'Connection Check'}</strong>
              <span>{methodLabel(method)}</span>
            </div>
            <div className="plan-facts">
              <span><b>{plan ? formatBytes(plan.transferCapBytes) : '—'}</b> maximum transfer</span>
              <span><b>{plan ? `${plan.downloadStages + plan.uploadStages}` : '—'}</b> transfer stages</span>
              <span><b>Local</b> report handling</span>
            </div>
          </div>

          <div className="evidence-card">
            <span className="section-kicker">Live evidence</span>
            <div className="evidence-line">
              <span>Phase</span>
              <strong>{running ? phaseLabel(progress?.phase) : result ? 'Complete' : 'Idle'}</strong>
            </div>
            <div className="evidence-line">
              <span>Measured payload</span>
              <strong>{formatBytes(result?.dataUsedBytes ?? progress?.bytesTransferred ?? 0)}</strong>
            </div>
            <div className="evidence-line">
              <span>Host</span>
              <strong>{host ? `${host.platform} · ${host.architecture}` : 'Photino'}</strong>
            </div>
          </div>
        </section>

        {error && <div className="error-banner" role="alert">{error}</div>}
      </main>
    </div>
  );
}

function Metric({
  label,
  value,
  liveValue,
  detail,
}: {
  label: string;
  value: string;
  liveValue?: string;
  detail: string;
}) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{liveValue || value}</strong>
      <small>{liveValue ? 'Live measurement' : detail}</small>
    </div>
  );
}

function overallProgress(progress: DiagnosticProgress | null): number {
  if (!progress) return 0;
  const fraction = Math.min(1, Math.max(0, progress.fraction || 0));
  switch (progress.phase) {
    case 'idle': return 0.08 + fraction * 0.17;
    case 'download': return 0.25 + fraction * 0.30;
    case 'upload': return 0.55 + fraction * 0.30;
    case 'diagnostics': return 0.85 + fraction * 0.12;
    case 'complete': return 1;
    default: return Math.min(0.08, fraction * 0.08);
  }
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
  return 'Single + aggregate comparison';
}

function phaseLabel(phase?: string): string {
  switch (phase) {
    case 'idle': return 'Baseline latency';
    case 'download': return 'Download measurement';
    case 'upload': return 'Upload measurement';
    case 'diagnostics': return 'Deep diagnostics';
    case 'complete': return 'Complete';
    case 'starting': return 'Preparing';
    default: return 'Measuring';
  }
}

export default App;

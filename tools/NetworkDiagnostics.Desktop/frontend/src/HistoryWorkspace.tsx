import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import './history.css';
import './report.css';

export type SavedReportSummary = {
  id: string;
  generatedAt: string;
  storedAt: string;
  profile: string;
  profileName: string;
  label?: string | null;
  tags: string[];
  latencyMs?: number | null;
  requestLossPercent?: number | null;
  downloadMbps?: number | null;
  uploadMbps?: number | null;
  dataUsedBytes?: number | null;
};

type SavedReportDetail = {
  report: SavedReportSummary;
  context: string;
  method: string;
  presentation: {
    outcome: string;
    label: string;
    verdict: string;
    summary: string;
    nextAction: string;
    metrics: Array<{ label: string; value: string; detail: string; wasMeasured: boolean }>;
    findings: Array<{ label: string; title: string; summary: string }>;
    technicalEvidence: string[];
  };
};

type SavedReportComparison = {
  baseline: SavedReportSummary;
  candidate: SavedReportSummary;
  baselineContext: string;
  candidateContext: string;
  comparable: boolean;
  warnings: string[];
  summary: string;
  metrics: Array<{
    id: string;
    label: string;
    baseline: string;
    candidate: string;
    change: string;
    numericChange?: number | null;
  }>;
};

type PanelView =
  | { kind: 'list' }
  | { kind: 'detail'; detail: SavedReportDetail }
  | { kind: 'compare-select'; baseline: SavedReportDetail }
  | { kind: 'comparison'; baseline: SavedReportDetail; comparison: SavedReportComparison };

export function HistoryPanel({
  open,
  reports,
  loading,
  error,
  onClose,
  onRefresh,
}: {
  open: boolean;
  reports: SavedReportSummary[];
  loading: boolean;
  error: string | null;
  onClose: () => void;
  onRefresh: () => void;
}) {
  const panelRef = useRef<HTMLElement>(null);
  const closeRef = useRef(onClose);
  const [view, setView] = useState<PanelView>({ kind: 'list' });
  const [viewLoading, setViewLoading] = useState(false);
  const [viewError, setViewError] = useState<string | null>(null);
  closeRef.current = onClose;

  useEffect(() => {
    if (!open) return;
    setView({ kind: 'list' });
    setViewLoading(false);
    setViewError(null);

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
      if (elements.length === 0) {
        event.preventDefault();
        return;
      }
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

  const header = headerFor(view);
  const wide = view.kind === 'detail' || view.kind === 'comparison';

  async function openDetail(report: SavedReportSummary) {
    setViewLoading(true);
    setViewError(null);
    try {
      const detail = await desktopBridge.request<SavedReportDetail>('reports.get', { id: report.id });
      setView({ kind: 'detail', detail });
    } catch (value) {
      setViewError(errorMessage(value, 'The saved report could not be opened.'));
    } finally {
      setViewLoading(false);
    }
  }

  async function compareWith(report: SavedReportSummary, baseline: SavedReportDetail) {
    if (report.id === baseline.report.id) return;
    setViewLoading(true);
    setViewError(null);
    try {
      const comparison = await desktopBridge.request<SavedReportComparison>('reports.compare', {
        baselineId: baseline.report.id,
        candidateId: report.id,
      });
      setView({ kind: 'comparison', baseline, comparison });
    } catch (value) {
      setViewError(errorMessage(value, 'The saved reports could not be compared.'));
    } finally {
      setViewLoading(false);
    }
  }

  function goBack() {
    setViewError(null);
    setView((current) => {
      if (current.kind === 'comparison' || current.kind === 'compare-select') {
        return { kind: 'detail', detail: current.baseline };
      }
      return { kind: 'list' };
    });
  }

  return (
    <div className="history-layer" role="presentation">
      <button className="history-backdrop" type="button" aria-label="Close saved runs" onClick={onClose} tabIndex={-1} />
      <aside
        ref={panelRef}
        className={`history-panel ${wide ? 'report-panel-wide' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="history-title"
      >
        <header className="history-header">
          <div>
            <span className="history-kicker">{header.kicker}</span>
            <h2 id="history-title">{header.title}</h2>
            <p>{header.subtitle}</p>
          </div>
          <div className="history-header-actions">
            {view.kind === 'list' ? (
              <IconButton label="Refresh saved runs" onClick={onRefresh} disabled={loading} icon="refresh" />
            ) : (
              <IconButton label="Back" onClick={goBack} icon="back" />
            )}
            <IconButton label="Close saved runs" onClick={onClose} icon="close" />
          </div>
        </header>

        {viewLoading ? (
          <LoadingView />
        ) : view.kind === 'list' ? (
          <ReportList reports={reports} loading={loading} error={error} onRefresh={onRefresh} onOpen={(report) => void openDetail(report)} />
        ) : view.kind === 'compare-select' ? (
          <CompareSelector reports={reports} baseline={view.baseline} error={viewError} onCompare={(report) => void compareWith(report, view.baseline)} />
        ) : view.kind === 'detail' ? (
          <ReportDetail
            detail={view.detail}
            error={viewError}
            onCompare={() => {
              setViewError(null);
              setView({ kind: 'compare-select', baseline: view.detail });
            }}
          />
        ) : (
          <ComparisonDetail comparison={view.comparison} error={viewError} />
        )}
      </aside>
    </div>
  );
}

function ReportList({ reports, loading, error, onRefresh, onOpen }: {
  reports: SavedReportSummary[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  onOpen: (report: SavedReportSummary) => void;
}) {
  if (loading && reports.length === 0) return <LoadingView label="Reading saved runs" />;
  if (error && reports.length === 0) {
    return (
      <div className="history-body">
        <div className="history-empty history-error" role="alert">
          <strong>Saved runs could not be read.</strong>
          <p>{error}</p>
          <button type="button" onClick={onRefresh}>Try again</button>
        </div>
      </div>
    );
  }
  if (reports.length === 0) {
    return (
      <div className="history-body">
        <div className="history-empty">
          <svg className="history-empty-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 7v5l3 2" /><circle cx="12" cy="12" r="8" /></svg>
          <strong>No saved runs yet</strong>
          <p>Finished diagnostics will appear here automatically.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="history-body">
      <div className="history-list" aria-live="polite">
        {reports.map((report) => <ReportRow key={report.id} report={report} onClick={() => onOpen(report)} />)}
      </div>
    </div>
  );
}

function CompareSelector({ reports, baseline, error, onCompare }: {
  reports: SavedReportSummary[];
  baseline: SavedReportDetail;
  error: string | null;
  onCompare: (report: SavedReportSummary) => void;
}) {
  return (
    <div className="history-body">
      <div className="compare-baseline-note">
        Baseline: <strong>{displayName(baseline.report)}</strong>. Choose a different saved run. Compatibility differences will be shown rather than hidden.
      </div>
      {error && <div className="report-error-banner" role="alert">{error}</div>}
      {reports.length < 2 ? (
        <div className="history-empty">
          <strong>Another saved run is required</strong>
          <p>Run and save the same profile again to establish a useful comparison.</p>
        </div>
      ) : (
        <div className="history-list">
          {reports.map((report) => (
            <ReportRow
              key={report.id}
              report={report}
              disabled={report.id === baseline.report.id}
              onClick={() => onCompare(report)}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function ReportDetail({ detail, error, onCompare }: {
  detail: SavedReportDetail;
  error: string | null;
  onCompare: () => void;
}) {
  const presentation = detail.presentation;
  return (
    <div className="report-body">
      {error && <div className="report-error-banner" role="alert">{error}</div>}
      <section className="report-verdict">
        <span className={`report-outcome ${presentation.outcome}`}>{presentation.label}</span>
        <h3>{presentation.verdict}</h3>
        <p>{presentation.summary}</p>
        <p className="report-next-action">{presentation.nextAction}</p>
      </section>

      <section className="report-section">
        <div className="report-section-heading"><h3>Headline measurements</h3><span>{methodLabel(detail.method)}</span></div>
        <div className="report-metric-grid">
          {presentation.metrics.map((metric) => (
            <div key={metric.label} className={`report-metric-card ${metric.wasMeasured ? '' : 'unmeasured'}`}>
              <span>{metric.label}</span><strong>{metric.value}</strong><small>{metric.detail}</small>
            </div>
          ))}
        </div>
      </section>

      <section className="report-section">
        <div className="report-section-heading"><h3>Findings</h3><span>{presentation.findings.length}</span></div>
        <div className="report-findings">
          {presentation.findings.map((finding, index) => (
            <div className="report-finding" key={`${finding.title}-${index}`}>
              <span>{finding.label}</span><strong>{finding.title}</strong><p>{finding.summary}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="report-section">
        <details className="report-evidence">
          <summary>Technical evidence · {presentation.technicalEvidence.length} items</summary>
          <ul className="report-evidence-list">
            {presentation.technicalEvidence.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}
          </ul>
        </details>
      </section>

      <div className="report-actions">
        <button type="button" className="report-action primary" onClick={onCompare}>Compare with another run</button>
      </div>
    </div>
  );
}

function ComparisonDetail({ comparison, error }: { comparison: SavedReportComparison; error: string | null }) {
  return (
    <div className="report-body">
      {error && <div className="report-error-banner" role="alert">{error}</div>}
      {comparison.warnings.length > 0 && (
        <div className="report-warning-banner">
          <strong>{comparison.comparable ? 'Comparison note' : 'These runs are not directly equivalent'}</strong>
          <ul>{comparison.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>
        </div>
      )}

      <div className="comparison-pair">
        <ComparisonReportCard label="Baseline" report={comparison.baseline} context={comparison.baselineContext} />
        <ComparisonReportCard label="Candidate" report={comparison.candidate} context={comparison.candidateContext} />
      </div>
      <p className="comparison-summary">{comparison.summary}</p>

      <section className="report-section">
        <div className="report-section-heading"><h3>Measurement deltas</h3><span>{comparison.metrics.length}</span></div>
        <div className="comparison-metrics">
          {comparison.metrics.map((metric) => (
            <div className="comparison-metric" key={metric.id}>
              <div className="comparison-metric-label"><span>Measurement</span><strong>{metric.label}</strong></div>
              <div className="comparison-value">{metric.baseline}</div>
              <div className="comparison-value">{metric.candidate}</div>
              <div className="comparison-change">{metric.change}</div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

function ComparisonReportCard({ label, report, context }: { label: string; report: SavedReportSummary; context: string }) {
  return (
    <div className="comparison-report-card">
      <span>{label}</span><strong>{displayName(report)}</strong>
      <small>{formatDateTime(report.generatedAt)}<br />{context || report.profileName}</small>
    </div>
  );
}

function ReportRow({ report, disabled = false, onClick }: {
  report: SavedReportSummary;
  disabled?: boolean;
  onClick: () => void;
}) {
  const generatedAt = new Date(report.generatedAt);
  const date = generatedAt.toLocaleDateString(undefined, {
    month: 'short', day: 'numeric',
    year: generatedAt.getFullYear() === new Date().getFullYear() ? undefined : 'numeric',
  });
  const time = generatedAt.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  const hasLoss = (report.requestLossPercent ?? 0) > 0;

  return (
    <button type="button" className="history-row history-row-button" onClick={onClick} disabled={disabled}>
      <div className="history-row-heading">
        <div><strong>{displayName(report)}</strong><span>{date} · {time}</span></div>
        <div className="history-row-meta">
          {hasLoss && <span className="history-loss">{formatMetric(report.requestLossPercent, '%')} loss</span>}
          <span>{formatBytes(report.dataUsedBytes)}</span>
        </div>
      </div>
      <div className="history-metrics">
        <HistoryMetric label="Latency" value={formatMetric(report.latencyMs, 'ms')} />
        <HistoryMetric label="Download" value={formatMetric(report.downloadMbps, 'Mbps')} />
        <HistoryMetric label="Upload" value={formatMetric(report.uploadMbps, 'Mbps')} />
      </div>
      {report.tags.length > 0 && <div className="history-tags" aria-label="Report tags">{report.tags.map((tag) => <span key={tag}>{tag}</span>)}</div>}
    </button>
  );
}

function HistoryMetric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function IconButton({ label, onClick, disabled = false, icon }: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  icon: 'refresh' | 'back' | 'close';
}) {
  return (
    <button type="button" className="history-icon-button" onClick={onClick} aria-label={label} disabled={disabled}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        {icon === 'refresh' && <><path d="M20 11a8 8 0 1 0-2.34 5.66" /><path d="M20 5v6h-6" /></>}
        {icon === 'back' && <path d="M19 12H5M11 18l-6-6 6-6" />}
        {icon === 'close' && <path d="M6 6l12 12M18 6 6 18" />}
      </svg>
    </button>
  );
}

function LoadingView({ label = 'Reading local report data' }: { label?: string }) {
  return (
    <div className="history-body">
      <div className="history-empty report-loading">
        <span className="history-loader" aria-hidden="true" />
        <strong>{label}</strong>
        <p>The report files stay local to this computer.</p>
      </div>
    </div>
  );
}

function headerFor(view: PanelView): { kicker: string; title: string; subtitle: string } {
  if (view.kind === 'detail') return {
    kicker: 'Saved report', title: displayName(view.detail.report),
    subtitle: `${formatDateTime(view.detail.report.generatedAt)} · ${view.detail.context || view.detail.report.profileName}`,
  };
  if (view.kind === 'compare-select') return {
    kicker: 'Compare reports', title: 'Choose another run', subtitle: 'The selected report stays as the baseline.',
  };
  if (view.kind === 'comparison') return {
    kicker: 'Comparison', title: `${view.comparison.baseline.profileName} vs ${view.comparison.candidate.profileName}`,
    subtitle: 'Baseline and candidate measurements are kept side by side.',
  };
  return { kicker: 'Local reports', title: 'Saved runs', subtitle: 'Completed diagnostics stored on this computer.' };
}

function focusableElements(root: HTMLElement | null): HTMLElement[] {
  if (!root) return [];
  return Array.from(root.querySelectorAll<HTMLElement>(
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )).filter((element) => !element.hasAttribute('hidden'));
}

function errorMessage(value: unknown, fallback: string): string {
  return value instanceof Error ? value.message : fallback;
}

function displayName(report: SavedReportSummary): string {
  return report.label || report.profileName;
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString(undefined, {
    month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit',
  });
}

function methodLabel(method: string): string {
  if (method === 'single') return 'Single flow';
  if (method === 'aggregate') return 'Aggregate flows';
  return 'Single + aggregate';
}

function formatMetric(value: number | null | undefined, unit: string): string {
  if (value == null || !Number.isFinite(value)) return '—';
  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value)} ${unit}`;
}

function formatBytes(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return '—';
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)} GB`;
  return `${(value / 1_000_000).toFixed(value >= 100_000_000 ? 0 : 1)} MB`;
}

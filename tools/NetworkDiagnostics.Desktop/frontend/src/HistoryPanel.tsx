import { useEffect, useRef } from 'react';
import './history.css';

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

  useEffect(() => {
    if (!open) return;

    const previousFocus = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const frame = window.requestAnimationFrame(() => {
      const first = focusableElements(panelRef.current)[0];
      first?.focus();
    });

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onClose();
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
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="history-layer" role="presentation">
      <button className="history-backdrop" type="button" aria-label="Close saved runs" onClick={onClose} tabIndex={-1} />
      <aside
        ref={panelRef}
        className="history-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="history-title"
      >
        <header className="history-header">
          <div>
            <span className="history-kicker">Local reports</span>
            <h2 id="history-title">Saved runs</h2>
            <p>Completed diagnostics stored on this computer.</p>
          </div>
          <div className="history-header-actions">
            <button type="button" className="history-icon-button" onClick={onRefresh} aria-label="Refresh saved runs" disabled={loading}>
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M20 11a8 8 0 1 0-2.34 5.66" />
                <path d="M20 5v6h-6" />
              </svg>
            </button>
            <button type="button" className="history-icon-button" onClick={onClose} aria-label="Close saved runs">
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </button>
          </div>
        </header>

        <div className="history-body">
          {loading && reports.length === 0 && (
            <div className="history-empty">
              <span className="history-loader" aria-hidden="true" />
              <strong>Reading saved runs</strong>
              <p>The report files stay local to this device.</p>
            </div>
          )}

          {!loading && error && reports.length === 0 && (
            <div className="history-empty history-error" role="alert">
              <strong>Saved runs could not be read.</strong>
              <p>{error}</p>
              <button type="button" onClick={onRefresh}>Try again</button>
            </div>
          )}

          {!loading && !error && reports.length === 0 && (
            <div className="history-empty">
              <svg className="history-empty-icon" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 7v5l3 2" />
                <circle cx="12" cy="12" r="8" />
              </svg>
              <strong>No saved runs yet</strong>
              <p>Finished diagnostics will appear here automatically.</p>
            </div>
          )}

          {reports.length > 0 && (
            <div className="history-list" aria-live="polite">
              {reports.map((report) => (
                <ReportRow key={report.id} report={report} />
              ))}
            </div>
          )}
        </div>
      </aside>
    </div>
  );
}

function ReportRow({ report }: { report: SavedReportSummary }) {
  const generatedAt = new Date(report.generatedAt);
  const date = generatedAt.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: generatedAt.getFullYear() === new Date().getFullYear() ? undefined : 'numeric',
  });
  const time = generatedAt.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  const hasLoss = (report.requestLossPercent ?? 0) > 0;

  return (
    <article className="history-row">
      <div className="history-row-heading">
        <div>
          <strong>{report.label || report.profileName}</strong>
          <span>{date} · {time}</span>
        </div>
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

      {report.tags.length > 0 && (
        <div className="history-tags" aria-label="Report tags">
          {report.tags.map((tag) => <span key={tag}>{tag}</span>)}
        </div>
      )}
    </article>
  );
}

function HistoryMetric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function focusableElements(root: HTMLElement | null): HTMLElement[] {
  if (!root) return [];
  return Array.from(root.querySelectorAll<HTMLElement>(
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )).filter((element) => !element.hasAttribute('hidden'));
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

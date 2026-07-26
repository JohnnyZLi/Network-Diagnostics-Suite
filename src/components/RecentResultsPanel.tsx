import { formatLatency, formatRate } from "../core/format";
import type { DiagnosticResult, DownloadImplementation } from "../types/diagnostics";

interface RecentResultsPanelProps {
  results: DiagnosticResult[];
  currentResultId: string | null;
  onOpen: (result: DiagnosticResult) => void;
  onExport: (result: DiagnosticResult) => void;
  onClear: () => void;
}

interface PathStatistics {
  count: number;
  downloadMbps: number;
  stabilityPercent: number;
  loadedLatencyMs: number | null;
}

function median(values: number[]): number | null {
  if (values.length === 0) return null;
  const sorted = [...values].sort((left, right) => left - right);
  const middle = Math.floor(sorted.length / 2);
  if (sorted.length % 2 === 1) return sorted[middle] ?? null;
  return ((sorted[middle - 1] ?? 0) + (sorted[middle] ?? 0)) / 2;
}

function pathStatistics(results: DiagnosticResult[], path: DownloadImplementation): PathStatistics | null {
  const matching = results.filter((result) => result.download.delivery?.selectedPath === path);
  if (matching.length === 0) return null;
  const loadedLatency = matching
    .map((result) => result.downloadLatency.increaseMs)
    .filter((value): value is number => value !== null && Number.isFinite(value));
  return {
    count: matching.length,
    downloadMbps: median(matching.map((result) => result.download.steadyMbps)) ?? 0,
    stabilityPercent: median(matching.map((result) => result.download.stabilityPercent)) ?? 0,
    loadedLatencyMs: median(loadedLatency)
  };
}

function pathLabel(result: DiagnosticResult): string {
  return result.download.delivery?.selectedPath === "r2-direct-v1" ? "R2 direct" : "Worker";
}

function ComparisonCard({ name, statistics }: { name: string; statistics: PathStatistics | null }) {
  return (
    <article className="comparison-card">
      <div className="comparison-card__heading">
        <span>{name}</span>
        <small>{statistics ? `${statistics.count} saved run${statistics.count === 1 ? "" : "s"}` : "No saved runs"}</small>
      </div>
      {statistics ? (
        <dl>
          <div><dt>Median download</dt><dd>{formatRate(statistics.downloadMbps)} Mbps</dd></div>
          <div><dt>Median stability</dt><dd>{statistics.stabilityPercent.toFixed(0)}%</dd></div>
          <div><dt>Median loaded delay</dt><dd>+{formatLatency(statistics.loadedLatencyMs)} ms</dd></div>
        </dl>
      ) : (
        <p>Run this path once to add it to the local comparison.</p>
      )}
    </article>
  );
}

export function RecentResultsPanel({
  results,
  currentResultId,
  onOpen,
  onExport,
  onClear
}: RecentResultsPanelProps) {
  if (results.length === 0) return null;
  const r2 = pathStatistics(results, "r2-direct-v1");
  const worker = pathStatistics(results, "worker-stream-v4");
  const r2Advantage = r2 && worker && worker.downloadMbps > 0
    ? ((r2.downloadMbps / worker.downloadMbps) - 1) * 100
    : null;

  return (
    <section className="recent-results" aria-labelledby="recent-results-title">
      <div className="section-heading section-heading--actions">
        <div>
          <span className="eyebrow">Stored only in this browser</span>
          <h2 id="recent-results-title">Recent reports and path comparison</h2>
          <p>The newest 12 completed reports stay in local storage on this device. Nothing is uploaded to an application database.</p>
        </div>
        <button className="history-clear" type="button" onClick={onClear}>Clear local history</button>
      </div>

      <div className="comparison-grid">
        <ComparisonCard name="R2 direct" statistics={r2} />
        <ComparisonCard name="Worker comparison" statistics={worker} />
      </div>
      {r2Advantage !== null && (
        <p className="comparison-note">
          The saved median R2 download is {Math.abs(r2Advantage).toFixed(0)}% {r2Advantage >= 0 ? "faster" : "slower"} than the saved Worker median.
        </p>
      )}

      <div className="history-list" role="list">
        {results.map((saved) => (
          <article
            className={saved.id === currentResultId ? "history-row history-row--current" : "history-row"}
            key={saved.id}
            role="listitem"
          >
            <div>
              <strong>{new Date(saved.startedAt).toLocaleString([], { dateStyle: "medium", timeStyle: "short" })}</strong>
              <span>{pathLabel(saved)} · {saved.mode}</span>
            </div>
            <dl>
              <div><dt>Down</dt><dd>{formatRate(saved.download.steadyMbps)} Mbps</dd></div>
              <div><dt>Stability</dt><dd>{saved.download.stabilityPercent.toFixed(0)}%</dd></div>
              <div><dt>Loaded</dt><dd>+{formatLatency(saved.downloadLatency.increaseMs)} ms</dd></div>
            </dl>
            <div className="history-row__actions">
              <button type="button" onClick={() => onOpen(saved)}>Open</button>
              <button type="button" onClick={() => onExport(saved)}>Export</button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

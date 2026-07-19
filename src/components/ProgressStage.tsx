import { formatBytes, formatLatency, formatRate } from "../core/format";
import type { TestProgress } from "../types/diagnostics";

const PHASE_LABELS: Record<TestProgress["phase"], string> = {
  idle: "Measuring idle latency",
  download: "Loading downstream",
  upload: "Loading upstream",
  services: "Checking service reachability",
  complete: "Analysis complete"
};

interface ProgressStageProps {
  progress: TestProgress;
}

export function ProgressStage({ progress }: ProgressStageProps) {
  const percentage = Math.round(Math.min(1, Math.max(0, progress.fraction)) * 100);
  return (
    <section className="progress-stage" aria-live="polite">
      <div className="progress-stage__topline">
        <span className="activity-indicator" aria-hidden="true"><i /><i /><i /></span>
        <span>{PHASE_LABELS[progress.phase]}</span>
        <strong>{percentage}%</strong>
      </div>
      <div className="progress-track" aria-label={`${percentage}% complete`}>
        <span style={{ width: `${percentage}%` }} />
      </div>
      <div className="progress-readings">
        <div>
          <span>Live throughput</span>
          <strong>{formatRate(progress.liveMbps)} <small>Mbps</small></strong>
        </div>
        <div>
          <span>Live latency</span>
          <strong>{formatLatency(progress.liveLatencyMs)} <small>ms</small></strong>
        </div>
        <div>
          <span>Transferred</span>
          <strong>{formatBytes(progress.bytesTransferred)}</strong>
        </div>
      </div>
    </section>
  );
}

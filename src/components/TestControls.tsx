import { formatBytes } from "../core/format";
import { TEST_MODES } from "../diagnostics/config";
import type { DownloadPathPreference, TestMode } from "../types/diagnostics";

interface TestControlsProps {
  mode: TestMode;
  downloadPath: DownloadPathPreference;
  running: boolean;
  dataConfirmed: boolean;
  onModeChange: (mode: TestMode) => void;
  onDownloadPathChange: (path: DownloadPathPreference) => void;
  onDataConfirmed: (confirmed: boolean) => void;
  onStart: () => void;
  onCancel: () => void;
}

const DOWNLOAD_PATHS: Record<DownloadPathPreference, { name: string; detail: string }> = {
  auto: { name: "Automatic", detail: "R2 + fallback" },
  "r2-direct": { name: "Direct R2", detail: "R2 only" },
  "worker-stream": { name: "Worker", detail: "Worker only" }
};

function compactEstimatedTime(value: string): string {
  return value
    .replace(/^about\s+/i, "≈")
    .replace(/\s+seconds?$/i, " sec");
}

export function TestControls({
  mode,
  downloadPath,
  running,
  dataConfirmed,
  onModeChange,
  onDownloadPathChange,
  onDataConfirmed,
  onStart,
  onCancel
}: TestControlsProps) {
  const config = TEST_MODES[mode];
  const transferCap = config.downloadCapBytes + config.uploadCapBytes;
  const requiresConfirmation = mode !== "quick";

  return (
    <section className="test-controls" aria-labelledby="test-controls-title">
      <div className="eyebrow" id="test-controls-title">Test profile</div>
      <div className="mode-selector" role="radiogroup" aria-label="Diagnostic test profile">
        {(Object.keys(TEST_MODES) as TestMode[]).map((option) => {
          const optionConfig = TEST_MODES[option];
          return (
            <button
              className={mode === option ? "mode-option mode-option--active" : "mode-option"}
              type="button"
              role="radio"
              aria-checked={mode === option}
              aria-label={`${optionConfig.name}, ${optionConfig.estimatedTime}`}
              disabled={running}
              onClick={() => onModeChange(option)}
              key={option}
            >
              <span>{optionConfig.name}</span>
              <small aria-hidden="true">{compactEstimatedTime(optionConfig.estimatedTime)}</small>
            </button>
          );
        })}
      </div>

      <div className="test-controls__summary">
        <p>{config.description}</p>
        <dl>
          <div><dt>Transfer cap</dt><dd>{formatBytes(transferCap)}</dd></div>
          <div><dt>Download</dt><dd className={downloadPath === "auto" ? "path-recommendation" : ""}>{DOWNLOAD_PATHS[downloadPath].name}</dd></div>
          <div><dt>Samples</dt><dd>Median of {config.downloadSamples} downloads</dd></div>
          <div><dt>Services</dt><dd>{config.includeServices ? "6 destinations" : "Not contacted"}</dd></div>
          <div><dt>Storage</dt><dd>12 reports · this browser</dd></div>
        </dl>
      </div>

      <details className="advanced-path">
        <summary>Advanced download path</summary>
        <div className="mode-selector path-selector" role="radiogroup" aria-label="Download measurement path">
          {(Object.keys(DOWNLOAD_PATHS) as DownloadPathPreference[]).map((option) => (
            <button
              className={downloadPath === option ? "mode-option mode-option--active" : "mode-option"}
              type="button"
              role="radio"
              aria-checked={downloadPath === option}
              disabled={running}
              onClick={() => onDownloadPathChange(option)}
              key={option}
            >
              <span>{DOWNLOAD_PATHS[option].name}</span>
              <small>{DOWNLOAD_PATHS[option].detail}</small>
            </button>
          ))}
        </div>
      </details>

      {!running && (
        <div className={`data-use-note data-use-note--${mode}`}>
          <strong>Data use</strong>
          <span>May transfer up to {formatBytes(transferCap)}. Avoid running on metered or cellular connections.</span>
        </div>
      )}

      <div className="data-confirmation-slot">
        {requiresConfirmation && !running && (
          <label className="data-confirmation">
            <input
              type="checkbox"
              checked={dataConfirmed}
              onChange={(event) => onDataConfirmed(event.target.checked)}
            />
            <span>I understand this {config.name.toLowerCase()} test uses significant data.</span>
          </label>
        )}
      </div>

      {running ? (
        <button type="button" className="run-button run-button--cancel" onClick={onCancel}>
          Stop test
        </button>
      ) : (
        <button
          type="button"
          className="run-button"
          onClick={onStart}
          disabled={requiresConfirmation && !dataConfirmed}
        >
          Run {config.name.toLowerCase()} test
          <span aria-hidden="true">→</span>
        </button>
      )}
    </section>
  );
}

import { formatBytes } from "../core/format";
import { TEST_MODES } from "../diagnostics/config";
import type { DownloadPathPreference, TestMode } from "../types/diagnostics";

interface TestControlsProps {
  mode: TestMode;
  downloadPath: DownloadPathPreference;
  running: boolean;
  stressConfirmed: boolean;
  onModeChange: (mode: TestMode) => void;
  onDownloadPathChange: (path: DownloadPathPreference) => void;
  onStressConfirmed: (confirmed: boolean) => void;
  onStart: () => void;
  onCancel: () => void;
}

const DOWNLOAD_PATHS: Record<DownloadPathPreference, { name: string; detail: string }> = {
  auto: { name: "Automatic", detail: "R2 with safe fallback" },
  "r2-direct": { name: "R2 direct", detail: "Require direct edge object" },
  "worker-stream": { name: "Worker", detail: "Comparison only" }
};

export function TestControls({
  mode,
  downloadPath,
  running,
  stressConfirmed,
  onModeChange,
  onDownloadPathChange,
  onStressConfirmed,
  onStart,
  onCancel
}: TestControlsProps) {
  const config = TEST_MODES[mode];
  const transferCap = config.downloadCapBytes + config.uploadCapBytes;
  const requiresConfirmation = mode === "extended";

  return (
    <section className="test-controls" aria-labelledby="test-controls-title">
      <div className="eyebrow" id="test-controls-title">Test profile</div>
      <div className="mode-selector" role="radiogroup" aria-label="Diagnostic test profile">
        {(Object.keys(TEST_MODES) as TestMode[]).map((option) => (
          <button
            className={mode === option ? "mode-option mode-option--active" : "mode-option"}
            type="button"
            role="radio"
            aria-checked={mode === option}
            disabled={running}
            onClick={() => onModeChange(option)}
            key={option}
          >
            <span>{TEST_MODES[option].name}</span>
            <small>{TEST_MODES[option].estimatedTime}</small>
          </button>
        ))}
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

      {requiresConfirmation && !running && (
        <label className="data-confirmation">
          <input
            type="checkbox"
            checked={stressConfirmed}
            onChange={(event) => onStressConfirmed(event.target.checked)}
          />
          <span>I understand this test may transfer up to {formatBytes(transferCap)}.</span>
        </label>
      )}

      {running ? (
        <button type="button" className="run-button run-button--cancel" onClick={onCancel}>
          Stop test
        </button>
      ) : (
        <button
          type="button"
          className="run-button"
          onClick={onStart}
          disabled={requiresConfirmation && !stressConfirmed}
        >
          Run {config.name.toLowerCase()} test
          <span aria-hidden="true">↗</span>
        </button>
      )}
    </section>
  );
}

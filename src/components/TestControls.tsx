import { useRef } from "react";
import { formatBytes } from "../core/format";
import { TEST_MODES } from "../diagnostics/config";
import type { DownloadPathPreference, TestMode } from "../types/diagnostics";

interface TestControlsProps {
  mode: TestMode;
  downloadPath: DownloadPathPreference;
  running: boolean;
  onModeChange: (mode: TestMode) => void;
  onDownloadPathChange: (path: DownloadPathPreference) => void;
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
  onModeChange,
  onDownloadPathChange,
  onStart,
  onCancel
}: TestControlsProps) {
  const config = TEST_MODES[mode];
  const transferCap = config.downloadCapBytes + config.uploadCapBytes;
  const requiresConfirmation = mode !== "quick";
  const confirmationDialogRef = useRef<HTMLDialogElement | null>(null);
  const runButtonRef = useRef<HTMLButtonElement | null>(null);
  const restoreRunButtonFocusRef = useRef(true);

  const closeConfirmationDialog = () => {
    confirmationDialogRef.current?.close();
  };

  const requestStart = () => {
    if (!requiresConfirmation) {
      onStart();
      return;
    }

    const dialog = confirmationDialogRef.current;
    if (dialog && !dialog.open) {
      restoreRunButtonFocusRef.current = true;
      dialog.showModal();
    }
  };

  const confirmStart = () => {
    restoreRunButtonFocusRef.current = false;
    confirmationDialogRef.current?.close();
    onStart();
  };

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

      <div className={`data-use-note data-use-note--${mode}`}>
        <strong>Data use</strong>
        <span>May transfer up to {formatBytes(transferCap)}. Avoid running on metered or cellular connections.</span>
      </div>

      {running ? (
        <button type="button" className="run-button run-button--cancel" onClick={onCancel}>
          Stop test
        </button>
      ) : (
        <button
          ref={runButtonRef}
          type="button"
          className="run-button"
          aria-haspopup={requiresConfirmation ? "dialog" : undefined}
          aria-controls={requiresConfirmation ? "data-confirmation-dialog" : undefined}
          onClick={requestStart}
        >
          Run {config.name.toLowerCase()} test
          <span aria-hidden="true">→</span>
        </button>
      )}

      <dialog
        className={`data-confirmation-dialog data-confirmation-dialog--${mode}`}
        id="data-confirmation-dialog"
        ref={confirmationDialogRef}
        aria-labelledby="data-confirmation-dialog-title"
        aria-describedby="data-confirmation-dialog-description"
        onClick={(event) => {
          if (event.target === event.currentTarget) closeConfirmationDialog();
        }}
        onClose={() => {
          if (restoreRunButtonFocusRef.current) runButtonRef.current?.focus();
          restoreRunButtonFocusRef.current = true;
        }}
      >
        <div className="data-confirmation-dialog__content">
          <span className="eyebrow">Confirm data use</span>
          <h2 id="data-confirmation-dialog-title">Run the {config.name} test?</h2>
          <p id="data-confirmation-dialog-description">
            This test may transfer up to {formatBytes(transferCap)}. Avoid running it on metered or cellular connections.
          </p>
          <div className="data-confirmation-dialog__actions">
            <button type="button" className="data-confirmation-dialog__button" onClick={closeConfirmationDialog}>
              Cancel
            </button>
            <button type="button" className="data-confirmation-dialog__button data-confirmation-dialog__button--primary" onClick={confirmStart}>
              Run {config.name.toLowerCase()} test
            </button>
          </div>
        </div>
      </dialog>
    </section>
  );
}

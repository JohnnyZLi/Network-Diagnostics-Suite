import { useRef, useState } from "react";
import { formatBytes } from "../core/format";
import { TEST_MODES } from "../diagnostics/config";
import { buildDiagnosticTestPlan } from "../diagnostics/flow-plan";
import type { DownloadPathPreference, TestMode, TransferMode } from "../types/diagnostics";

interface TestControlsProps {
  mode: TestMode;
  transferMode: TransferMode;
  downloadPath: DownloadPathPreference;
  running: boolean;
  onModeChange: (mode: TestMode) => void;
  onTransferModeChange: (mode: TransferMode) => void;
  onDownloadPathChange: (path: DownloadPathPreference) => void;
  onStart: () => void;
  onCancel: () => void;
}

type ConfirmedTestMode = Exclude<TestMode, "quick">;
type ConfirmationRecord = Partial<Record<ConfirmedTestMode, number>>;

const DATA_CONFIRMATION_STORAGE_KEY = "network-diagnostics.data-confirmations.v1";
const CONFIRMED_TEST_MODES: ConfirmedTestMode[] = ["standard", "extended"];

const TRANSFER_MODES: Record<TransferMode, { name: string; detail: string }> = {
  compare: { name: "Compare", detail: "Both" },
  single: { name: "Single", detail: "1 connection" },
  aggregate: { name: "Aggregate", detail: "Parallel" }
};

const DOWNLOAD_PATHS: Record<DownloadPathPreference, { name: string; detail: string }> = {
  auto: { name: "Automatic", detail: "R2 + fallback" },
  "r2-direct": { name: "Direct R2", detail: "R2 only" },
  "worker-stream": { name: "Worker", detail: "Worker only" }
};

function loadConfirmationRecord(): ConfirmationRecord {
  if (typeof window === "undefined") return {};

  try {
    const stored = window.localStorage.getItem(DATA_CONFIRMATION_STORAGE_KEY);
    if (!stored) return {};
    const parsed = JSON.parse(stored) as Record<string, unknown>;
    const record: ConfirmationRecord = {};

    for (const mode of CONFIRMED_TEST_MODES) {
      const value = parsed[mode];
      if (typeof value === "number" && Number.isFinite(value) && value >= 0) {
        record[mode] = value;
      }
    }

    return record;
  } catch {
    return {};
  }
}

function saveConfirmationRecord(record: ConfirmationRecord): void {
  if (typeof window === "undefined") return;

  try {
    window.localStorage.setItem(DATA_CONFIRMATION_STORAGE_KEY, JSON.stringify(record));
  } catch {
    // Storage can be unavailable in private or restricted browser contexts.
  }
}

function compactEstimatedTime(value: string): string {
  return value
    .replace(/^about\s+/i, "≈")
    .replace(/\s+seconds?$/i, " sec");
}

export function TestControls({
  mode,
  transferMode,
  downloadPath,
  running,
  onModeChange,
  onTransferModeChange,
  onDownloadPathChange,
  onStart,
  onCancel
}: TestControlsProps) {
  const profileConfig = TEST_MODES[mode];
  const plan = buildDiagnosticTestPlan(profileConfig, transferMode);
  const config = { ...profileConfig, estimatedTime: plan.estimatedTime };
  const transferCap = plan.transferCapBytes;
  const transferCapLabel = formatBytes(transferCap);
  const confirmationMode: ConfirmedTestMode | null = mode === "quick" ? null : mode;
  const [acknowledgedCaps, setAcknowledgedCaps] = useState<ConfirmationRecord>(loadConfirmationRecord);
  const [rememberChoice, setRememberChoice] = useState(true);
  const rememberedCap = confirmationMode ? acknowledgedCaps[confirmationMode] ?? 0 : transferCap;
  const requiresConfirmation = confirmationMode !== null && rememberedCap < transferCap;
  const confirmationDialogRef = useRef<HTMLDialogElement | null>(null);
  const cancelButtonRef = useRef<HTMLButtonElement | null>(null);
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
      setRememberChoice(true);
      restoreRunButtonFocusRef.current = true;
      dialog.showModal();
      cancelButtonRef.current?.focus();
      window.requestAnimationFrame(() => cancelButtonRef.current?.focus());
    }
  };

  const confirmStart = () => {
    if (rememberChoice && confirmationMode) {
      const nextRecord: ConfirmationRecord = {
        ...acknowledgedCaps,
        [confirmationMode]: Math.max(acknowledgedCaps[confirmationMode] ?? 0, transferCap)
      };
      setAcknowledgedCaps(nextRecord);
      saveConfirmationRecord(nextRecord);
    }

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
            </button>
          );
        })}
      </div>

      <div className="test-controls__subheading">Transfer method</div>
      <div className="mode-selector transfer-method-selector" role="radiogroup" aria-label="Transfer method">
        {(Object.keys(TRANSFER_MODES) as TransferMode[]).map((option) => (
          <button
            className={transferMode === option ? "mode-option mode-option--active" : "mode-option"}
            type="button"
            role="radio"
            aria-checked={transferMode === option}
            disabled={running}
            onClick={() => onTransferModeChange(option)}
            key={option}
          >
            <span>{TRANSFER_MODES[option].name}</span>
            <small>{TRANSFER_MODES[option].detail}</small>
          </button>
        ))}
      </div>

      <div className="test-controls__summary">
        <div className="test-controls__summary-group">
          <div className="test-controls__summary-label">Test limits</div>
          <dl>
            <div><dt>Estimated time</dt><dd>{compactEstimatedTime(config.estimatedTime)}</dd></div>
            <div><dt>Transfer cap</dt><dd>{formatBytes(transferCap)}</dd></div>
            <div><dt>Download path</dt><dd className={downloadPath === "auto" ? "path-recommendation" : ""}>{DOWNLOAD_PATHS[downloadPath].name}</dd></div>
          </dl>
        </div>

        <div className="test-controls__summary-group">
          <div className="test-controls__summary-label">Transfer plan</div>
          <dl>
            <div className="test-controls__variable-row"><dt>Download connections</dt><dd>{plan.downloadConnectionLabel}</dd></div>
            <div className="test-controls__variable-row"><dt>Upload connections</dt><dd>{plan.uploadConnectionLabel}</dd></div>
            <div className="test-controls__variable-row"><dt>Download runs</dt><dd>{plan.sampleLabel}</dd></div>
          </dl>
        </div>

        <div className="test-controls__summary-group">
          <div className="test-controls__summary-label">Local</div>
          <dl>
            <div><dt>Service checks</dt><dd>{config.includeServices ? "6 destinations" : "Off"}</dd></div>
            <div><dt>Saved reports</dt><dd>12 reports · this browser</dd></div>
          </dl>
        </div>
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
        <span>Transfers up to {transferCapLabel}. Avoid metered or cellular connections.</span>
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
        className={`data-confirmation-dialog data-confirmation-dialog--${mode} jl-dialog`}
        id="data-confirmation-dialog"
        ref={confirmationDialogRef}
        aria-labelledby="data-confirmation-dialog-title"
        aria-describedby="data-confirmation-dialog-description data-confirmation-dialog-note"
        onClick={(event) => {
          if (event.target === event.currentTarget) closeConfirmationDialog();
        }}
        onClose={() => {
          if (restoreRunButtonFocusRef.current) runButtonRef.current?.focus();
          restoreRunButtonFocusRef.current = true;
        }}
      >
        <div className="data-confirmation-dialog__content jl-dialog__surface">
          <span className="eyebrow">Confirm data use</span>
          <h2 className="jl-dialog__title" id="data-confirmation-dialog-title">Run the {config.name} test?</h2>
          <p className="jl-dialog__message" id="data-confirmation-dialog-description">
            This test may transfer up to {transferCapLabel}. The selected {TRANSFER_MODES[transferMode].name.toLowerCase()} method determines which transfer stages run. Avoid running it on metered or cellular connections.
          </p>
          <label className="data-confirmation-dialog__remember">
            <input
              type="checkbox"
              checked={rememberChoice}
              onChange={(event) => setRememberChoice(event.target.checked)}
            />
            <span>Remember this choice for the {config.name} profile on this browser.</span>
          </label>
          <p className="data-confirmation-dialog__note" id="data-confirmation-dialog-note">
            You’ll be asked again if this profile’s transfer cap increases.
          </p>
          <div className="data-confirmation-dialog__actions jl-dialog__actions jl-actions">
            <button ref={cancelButtonRef} type="button" className="data-confirmation-dialog__button jl-button" onClick={closeConfirmationDialog}>
              Cancel
            </button>
            <button type="button" className="data-confirmation-dialog__button data-confirmation-dialog__button--primary jl-button jl-button--primary" onClick={confirmStart}>
              Run {config.name.toLowerCase()} test
            </button>
          </div>
        </div>
      </dialog>
    </section>
  );
}

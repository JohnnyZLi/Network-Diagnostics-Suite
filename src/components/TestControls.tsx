import { formatBytes } from "../core/format";
import { TEST_MODES } from "../diagnostics/config";
import type { TestMode } from "../types/diagnostics";

interface TestControlsProps {
  mode: TestMode;
  running: boolean;
  stressConfirmed: boolean;
  onModeChange: (mode: TestMode) => void;
  onStressConfirmed: (confirmed: boolean) => void;
  onStart: () => void;
  onCancel: () => void;
}

export function TestControls({
  mode,
  running,
  stressConfirmed,
  onModeChange,
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
          <div><dt>Services</dt><dd>{config.includeServices ? "6 destinations" : "Not contacted"}</dd></div>
          <div><dt>Storage</dt><dd>None</dd></div>
        </dl>
      </div>

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

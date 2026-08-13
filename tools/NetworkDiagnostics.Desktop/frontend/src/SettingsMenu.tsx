import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import type { AppearanceMode, AppSettings } from './contracts';
import './settings.css';

export type { AppearanceMode } from './contracts';

export function SettingsMenu({
  appearance,
  onAppearanceChange,
  onReportsChanged,
  disabled = false,
  initialOpen = false,
}: {
  appearance: AppearanceMode;
  onAppearanceChange: (appearance: AppearanceMode) => void;
  onReportsChanged?: () => void;
  disabled?: boolean;
  initialOpen?: boolean;
}) {
  const [open, setOpen] = useState(initialOpen);
  const [download, setDownload] = useState('100');
  const [upload, setUpload] = useState('20');
  const [monitoringInterval, setMonitoringInterval] = useState('5');
  const [alertThreshold, setAlertThreshold] = useState('70');
  const [reportsDirectory, setReportsDirectory] = useState('');
  const [effectiveReportsDirectory, setEffectiveReportsDirectory] = useState('');
  const [retentionDays, setRetentionDays] = useState('0');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState<'capacity' | 'preferences' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : triggerRef.current;
    const frame = window.requestAnimationFrame(() => {
      const selectedAppearance = popoverRef.current?.querySelector<HTMLElement>('.appearance-control button.active');
      const firstControl = popoverRef.current?.querySelector<HTMLElement>('button:not([disabled]), input:not([disabled]), select:not([disabled])');
      (selectedAppearance ?? firstControl)?.focus();
    });

    const onPointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        setOpen(false);
      }
    };
    const onFocusIn = (event: FocusEvent) => {
      const container = containerRef.current;
      const nextTarget = event.target as Node;
      const previousTarget = event.relatedTarget as Node | null;
      if (container && !container.contains(nextTarget) && previousTarget && container.contains(previousTarget)) {
        setOpen(false);
      }
    };

    window.addEventListener('pointerdown', onPointerDown);
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('focusin', onFocusIn);
    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener('pointerdown', onPointerDown);
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('focusin', onFocusIn);
      previousFocus?.focus({ preventScroll: true });
    };
  }, [open]);

  useEffect(() => {
    if (!open || !desktopBridge.available) return;
    setLoading(true);
    setError(null);
    setNotice(null);
    void desktopBridge.request<AppSettings>('settings.get')
      .then((settings) => {
        setDownload(String(settings.expectedDownloadMbps ?? 100));
        setUpload(String(settings.expectedUploadMbps ?? 20));
        setMonitoringInterval(String(settings.monitoringIntervalSeconds ?? 5));
        setAlertThreshold(String(settings.monitoringAlertScoreThreshold ?? 70));
        setReportsDirectory(settings.reportsDirectory ?? '');
        setEffectiveReportsDirectory(settings.effectiveReportsDirectory ?? settings.reportsDirectory ?? 'Default application data folder');
        setRetentionDays(String(settings.reportRetentionDays ?? 0));
      })
      .catch((value) => setError(value instanceof Error ? value.message : 'Settings could not be read.'))
      .finally(() => setLoading(false));
  }, [open]);

  async function saveExpectedCapacity() {
    const downloadMbps = Number(download);
    const uploadMbps = Number(upload);
    if (!Number.isFinite(downloadMbps) || downloadMbps < 1 || downloadMbps > 100000
      || !Number.isFinite(uploadMbps) || uploadMbps < 1 || uploadMbps > 100000) {
      setError('Expected download and upload must be between 1 and 100,000 Mbps.');
      return;
    }
    setSaving('capacity');
    setError(null);
    setNotice(null);
    try {
      const settings = await desktopBridge.request<AppSettings>('settings.setExpectedCapacity', { downloadMbps, uploadMbps });
      setDownload(String(settings.expectedDownloadMbps));
      setUpload(String(settings.expectedUploadMbps));
      setNotice('Capacity expectation saved.');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Capacity expectation could not be saved.');
    } finally {
      setSaving(null);
    }
  }

  async function chooseReportsDirectory() {
    setError(null);
    setNotice(null);
    try {
      const result = await desktopBridge.request<{ cancelled: boolean; path?: string | null }>('settings.chooseReportsDirectory');
      if (!result.cancelled && result.path) setReportsDirectory(result.path);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'A reports folder could not be selected.');
    }
  }

  async function savePreferences() {
    const monitoringIntervalSeconds = Number(monitoringInterval);
    const monitoringAlertScoreThreshold = Number(alertThreshold);
    const reportRetentionDays = Number(retentionDays);
    if (!Number.isInteger(monitoringIntervalSeconds) || monitoringIntervalSeconds < 2 || monitoringIntervalSeconds > 60
      || !Number.isInteger(monitoringAlertScoreThreshold) || monitoringAlertScoreThreshold < 1 || monitoringAlertScoreThreshold > 100
      || !Number.isInteger(reportRetentionDays) || reportRetentionDays < 0 || reportRetentionDays > 3650) {
      setError('Monitoring interval must be 2–60 seconds, alert score 1–100, and retention 0–3,650 days.');
      return;
    }
    setSaving('preferences');
    setError(null);
    setNotice(null);
    try {
      const result = await desktopBridge.request<{ settings: AppSettings; prunedReports: number; effectiveReportsDirectory: string }>('settings.setPreferences', {
        monitoringIntervalSeconds,
        monitoringAlertScoreThreshold,
        reportsDirectory: reportsDirectory.trim() || null,
        reportRetentionDays,
      });
      setMonitoringInterval(String(result.settings.monitoringIntervalSeconds));
      setAlertThreshold(String(result.settings.monitoringAlertScoreThreshold));
      setReportsDirectory(result.settings.reportsDirectory ?? '');
      setRetentionDays(String(result.settings.reportRetentionDays));
      setEffectiveReportsDirectory(result.effectiveReportsDirectory);
      setNotice(result.prunedReports > 0 ? `Settings saved. Removed ${result.prunedReports} expired report${result.prunedReports === 1 ? '' : 's'}.` : 'Monitoring and report settings saved.');
      onReportsChanged?.();
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Monitoring and report settings could not be saved.');
    } finally {
      setSaving(null);
    }
  }

  async function openReportsFolder() {
    setError(null);
    try {
      await desktopBridge.request('reports.openFolder');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The reports folder could not be opened.');
    }
  }

  return (
    <div className="settings-menu" ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        className={`settings-trigger ${open ? 'active' : ''}`}
        aria-label="Settings"
        aria-expanded={open}
        aria-haspopup="dialog"
        disabled={disabled}
        onClick={() => setOpen((current) => !current)}
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M9.6 3h4.8l.6 2.4 1.7 1 2.4-.7 2.4 4.2-1.8 1.7v2l1.8 1.7-2.4 4.2-2.4-.7-1.7 1-.6 2.4H9.6L9 19.8l-1.7-1-2.4.7-2.4-4.2 1.8-1.7v-2L2.5 9.9l2.4-4.2 2.4.7 1.7-1L9.6 3Z" />
          <circle cx="12" cy="12" r="3.1" />
        </svg>
      </button>

      {open && (
        <div ref={popoverRef} className="settings-popover" role="dialog" aria-modal="false" aria-label="Application settings">
          <section className="settings-section">
            <div className="settings-copy">
              <strong>Appearance</strong>
              <span>Follow the system or keep a fixed theme.</span>
            </div>
            <div className="appearance-control" aria-label="Appearance">
              {(['system', 'light', 'dark'] as const).map((item) => (
                <button
                  key={item}
                  type="button"
                  className={appearance === item ? 'active' : ''}
                  aria-pressed={appearance === item}
                  onClick={() => onAppearanceChange(item)}
                >
                  {item[0].toUpperCase() + item.slice(1)}
                </button>
              ))}
            </div>
          </section>

          <section className="settings-section capacity-preference">
            <div className="settings-copy">
              <strong>Expected connection capacity</strong>
              <span>Used only to judge the most recent capacity measurement. It does not change how a diagnostic runs.</span>
            </div>
            <div className="capacity-preference-fields">
              <label><span>Download</span><div><input type="number" min="1" max="100000" step="1" value={download} disabled={loading || saving !== null} onChange={(event) => { setDownload(event.target.value); setNotice(null); }} /><b>Mbps</b></div></label>
              <label><span>Upload</span><div><input type="number" min="1" max="100000" step="1" value={upload} disabled={loading || saving !== null} onChange={(event) => { setUpload(event.target.value); setNotice(null); }} /><b>Mbps</b></div></label>
            </div>
            <button type="button" className="settings-save" disabled={loading || saving !== null} onClick={() => void saveExpectedCapacity()}>{saving === 'capacity' ? 'Saving…' : 'Save capacity'}</button>
          </section>

          <section className="settings-section monitoring-preferences">
            <div className="settings-copy">
              <strong>Monitoring</strong>
              <span>Set passive sample frequency and the score that creates a degradation alert.</span>
            </div>
            <div className="settings-number-grid">
              <label><span>Sample interval</span><div><input aria-label="Sample interval" type="number" min="2" max="60" step="1" value={monitoringInterval} disabled={loading || saving !== null} onChange={(event) => setMonitoringInterval(event.target.value)} /><b>sec</b></div></label>
              <label><span>Alert below</span><div><input aria-label="Alert score threshold" type="number" min="1" max="100" step="1" value={alertThreshold} disabled={loading || saving !== null} onChange={(event) => setAlertThreshold(event.target.value)} /><b>score</b></div></label>
            </div>
          </section>

          <section className="settings-section report-preferences">
            <div className="settings-copy">
              <strong>Saved reports</strong>
              <span>Choose local storage and optional automatic retention. Zero days keeps reports until you delete them.</span>
            </div>
            <label className="reports-directory-field"><span>Reports folder</span><input aria-label="Reports folder" type="text" value={reportsDirectory} placeholder="Default application data folder" disabled={loading || saving !== null} onChange={(event) => setReportsDirectory(event.target.value)} /></label>
            <div className="reports-directory-actions">
              <button type="button" onClick={() => void chooseReportsDirectory()} disabled={loading || saving !== null}>Choose folder</button>
              <button type="button" onClick={() => setReportsDirectory('')} disabled={loading || saving !== null}>Use default</button>
              <button type="button" onClick={() => void openReportsFolder()} disabled={loading}>Open folder</button>
            </div>
            <small className="effective-report-path" title={effectiveReportsDirectory}>Current: {effectiveReportsDirectory}</small>
            <label className="retention-field"><span>Delete reports older than</span><div><input aria-label="Report retention days" type="number" min="0" max="3650" step="1" value={retentionDays} disabled={loading || saving !== null} onChange={(event) => setRetentionDays(event.target.value)} /><b>days</b></div></label>
            <button type="button" className="settings-save" disabled={loading || saving !== null} onClick={() => void savePreferences()}>{saving === 'preferences' ? 'Saving…' : 'Save monitoring & reports'}</button>
          </section>

          {error && <p className="settings-message error" role="alert">{error}</p>}
          {notice && <p className="settings-message" role="status">{notice}</p>}
        </div>
      )}
    </div>
  );
}

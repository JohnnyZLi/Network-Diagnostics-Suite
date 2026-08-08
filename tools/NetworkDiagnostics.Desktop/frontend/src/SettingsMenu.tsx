import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import './settings.css';

export type AppearanceMode = 'system' | 'light' | 'dark';

type AppSettings = {
  appearance: AppearanceMode;
  monitoringEnabled: boolean;
  monitoringWindow: string;
  expectedDownloadMbps: number;
  expectedUploadMbps: number;
};

export function SettingsMenu({
  appearance,
  onAppearanceChange,
  disabled = false,
  initialOpen = false,
}: {
  appearance: AppearanceMode;
  onAppearanceChange: (appearance: AppearanceMode) => void;
  disabled?: boolean;
  initialOpen?: boolean;
}) {
  const [open, setOpen] = useState(initialOpen);
  const [download, setDownload] = useState('100');
  const [upload, setUpload] = useState('20');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };

    window.addEventListener('pointerdown', onPointerDown);
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('pointerdown', onPointerDown);
      window.removeEventListener('keydown', onKeyDown);
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
    setSaving(true);
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
      setSaving(false);
    }
  }

  return (
    <div className="settings-menu" ref={containerRef}>
      <button
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
        <div className="settings-popover" role="dialog" aria-label="Application settings">
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
              <label><span>Download</span><div><input type="number" min="1" max="100000" step="1" value={download} disabled={loading || saving} onChange={(event) => { setDownload(event.target.value); setNotice(null); }} /><b>Mbps</b></div></label>
              <label><span>Upload</span><div><input type="number" min="1" max="100000" step="1" value={upload} disabled={loading || saving} onChange={(event) => { setUpload(event.target.value); setNotice(null); }} /><b>Mbps</b></div></label>
            </div>
            <button type="button" className="settings-save" disabled={loading || saving} onClick={() => void saveExpectedCapacity()}>{saving ? 'Saving…' : 'Save capacity'}</button>
          </section>

          {error && <p className="settings-message error" role="alert">{error}</p>}
          {notice && <p className="settings-message" role="status">{notice}</p>}
        </div>
      )}
    </div>
  );
}

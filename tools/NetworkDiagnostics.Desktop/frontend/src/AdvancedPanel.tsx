import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import './advanced.css';

type TransferMethod = 'compare' | 'single' | 'aggregate';
type DiagnosticProfile = 'connection-check' | 'quick' | 'full' | 'stress';

type AdvancedSettings = {
  endpointCandidates: string[];
  interfaceId?: string | null;
  includeLocalIdentifiers: boolean;
  lanTarget?: string | null;
  lanPort: number;
  lanDurationSeconds: number;
  lanConnections: number;
};

type InterfaceChoice = Record<string, unknown>;

type PreflightResult = {
  measurement?: unknown;
  interfaces?: InterfaceChoice[];
};

type LanServerStatus = {
  running: boolean;
  port?: number;
};

type LanServerStart = {
  running: boolean;
  port: number;
};

const defaultSettings: AdvancedSettings = {
  endpointCandidates: [],
  interfaceId: null,
  includeLocalIdentifiers: false,
  lanTarget: null,
  lanPort: 8765,
  lanDurationSeconds: 8,
  lanConnections: 4,
};

export function AdvancedPanel({
  open,
  profile,
  method,
  onClose,
}: {
  open: boolean;
  profile: DiagnosticProfile;
  method: TransferMethod;
  onClose: () => void;
}) {
  const panelRef = useRef<HTMLElement>(null);
  const closeRef = useRef(onClose);
  const [settings, setSettings] = useState<AdvancedSettings>(defaultSettings);
  const [endpointText, setEndpointText] = useState('');
  const [interfaces, setInterfaces] = useState<InterfaceChoice[]>([]);
  const [preflight, setPreflight] = useState<PreflightResult | null>(null);
  const [serverRunning, setServerRunning] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  closeRef.current = onClose;

  useEffect(() => {
    if (!open || !desktopBridge.available) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setNotice(null);
    setPreflight(null);

    void Promise.all([
      desktopBridge.request<AdvancedSettings>('settings.getAdvanced'),
      desktopBridge.request<InterfaceChoice[]>('diagnostic.interfaces'),
      desktopBridge.request<LanServerStatus>('lan.server.status'),
    ]).then(([saved, choices, server]) => {
      if (cancelled) return;
      setSettings(saved);
      setEndpointText(saved.endpointCandidates.join('\n'));
      setInterfaces(choices);
      setServerRunning(server.running);
    }).catch((value: Error) => {
      if (!cancelled) setError(value.message);
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });

    return () => { cancelled = true; };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const frame = window.requestAnimationFrame(() => focusableElements(panelRef.current)[0]?.focus());

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeRef.current();
        return;
      }
      if (event.key !== 'Tab') return;
      const elements = focusableElements(panelRef.current);
      if (elements.length === 0) return;
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
  }, [open]);

  useEffect(() => {
    const removeStarted = desktopBridge.on<{ port: number }>('lan.server.started', () => {
      setServerRunning(true);
      setNotice('LAN throughput server started.');
    });
    const removeStopped = desktopBridge.on<{ port: number }>('lan.server.stopped', () => {
      setServerRunning(false);
    });
    const removeFailed = desktopBridge.on<{ message: string }>('lan.server.failed', (next) => {
      setServerRunning(false);
      setError(next.message);
    });
    return () => {
      removeStarted();
      removeStopped();
      removeFailed();
    };
  }, []);

  if (!open) return null;

  function patchSettings<K extends keyof AdvancedSettings>(key: K, value: AdvancedSettings[K]) {
    setSettings((current) => ({ ...current, [key]: value }));
    setNotice(null);
  }

  function normalizedEndpoints(): string[] {
    return [...new Set(endpointText
      .split(/\r?\n/)
      .map((value) => value.trim())
      .filter(Boolean))];
  }

  function validate(): string | null {
    const endpoints = normalizedEndpoints();
    if (endpoints.length > 8) return 'Use no more than eight endpoint candidates.';
    for (const endpoint of endpoints) {
      try {
        const url = new URL(endpoint);
        if (url.protocol !== 'https:' && url.protocol !== 'http:') return `Unsupported endpoint protocol: ${endpoint}`;
      } catch {
        return `Endpoint is not a valid URL: ${endpoint}`;
      }
    }
    if (!Number.isInteger(settings.lanPort) || settings.lanPort < 1024 || settings.lanPort > 65535) return 'LAN port must be between 1024 and 65535.';
    if (!Number.isInteger(settings.lanDurationSeconds) || settings.lanDurationSeconds < 3 || settings.lanDurationSeconds > 30) return 'LAN duration must be between 3 and 30 seconds.';
    if (!Number.isInteger(settings.lanConnections) || settings.lanConnections < 1 || settings.lanConnections > 16) return 'LAN connections must be between 1 and 16.';
    return null;
  }

  async function persist(): Promise<AdvancedSettings | null> {
    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return null;
    }

    const next = await desktopBridge.request<AdvancedSettings>('settings.setAdvanced', {
      endpointCandidates: normalizedEndpoints(),
      interfaceId: settings.interfaceId || null,
      includeLocalIdentifiers: settings.includeLocalIdentifiers,
      lanTarget: settings.lanTarget?.trim() || null,
      lanPort: settings.lanPort,
      lanDurationSeconds: settings.lanDurationSeconds,
      lanConnections: settings.lanConnections,
    });
    setSettings(next);
    setEndpointText(next.endpointCandidates.join('\n'));
    return next;
  }

  async function save() {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (await persist()) setNotice('Advanced configuration saved locally.');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Advanced configuration could not be saved.');
    } finally {
      setBusy(false);
    }
  }

  async function runPreflight() {
    setBusy(true);
    setError(null);
    setNotice(null);
    setPreflight(null);
    try {
      if (!await persist()) return;
      const result = await desktopBridge.request<PreflightResult>('diagnostic.preflight', { profile, method });
      setPreflight(result);
      if (result.interfaces?.length) setInterfaces(result.interfaces);
      setNotice('Native preflight completed with the saved configuration.');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Native preflight failed.');
    } finally {
      setBusy(false);
    }
  }

  async function toggleLanServer() {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (serverRunning) {
        await desktopBridge.request<{ stopped: boolean }>('lan.server.stop');
        setServerRunning(false);
        setNotice('LAN throughput server stopped.');
      } else {
        const validationError = validate();
        if (validationError) {
          setError(validationError);
          return;
        }
        const started = await desktopBridge.request<LanServerStart>('lan.server.start', { port: settings.lanPort });
        setServerRunning(started.running);
        setNotice(`LAN throughput server listening on port ${started.port}.`);
      }
    } catch (value) {
      setError(value instanceof Error ? value.message : 'LAN throughput server could not be updated.');
    } finally {
      setBusy(false);
    }
  }

  const endpoint = preflight ? firstUrl(preflight.measurement) : null;
  const measuredInterface = preflight ? findString(preflight.measurement, ['interfaceName', 'interfaceId', 'interface']) : null;

  return (
    <div className="advanced-layer" role="presentation">
      <button className="advanced-backdrop" type="button" aria-label="Close advanced diagnostics" onClick={onClose} tabIndex={-1} />
      <aside ref={panelRef} className="advanced-panel" role="dialog" aria-modal="true" aria-labelledby="advanced-title">
        <header className="advanced-header">
          <div>
            <span className="advanced-kicker">Native configuration</span>
            <h2 id="advanced-title">Advanced diagnostics</h2>
            <p>Endpoint selection, interface binding, privacy-sensitive identifiers, and LAN throughput controls.</p>
          </div>
          <button type="button" className="advanced-icon-button" onClick={onClose} aria-label="Close advanced diagnostics">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18" /></svg>
          </button>
        </header>

        <div className="advanced-body">
          {loading ? (
            <div className="advanced-empty"><span className="advanced-loader" /><strong>Reading native configuration</strong><p>Settings and interface choices stay on this computer.</p></div>
          ) : (
            <>
              {error && <div className="advanced-error" role="alert">{error}</div>}
              {notice && <div className="advanced-notice" role="status">{notice}</div>}

              <section className="advanced-section">
                <div className="advanced-section-heading">
                  <div><strong>Measurement endpoints</strong><span>Ordered candidates · maximum 8</span></div>
                  <span>{normalizedEndpoints().length} configured</span>
                </div>
                <label className="advanced-field">
                  <span>Endpoint candidates</span>
                  <textarea
                    value={endpointText}
                    rows={4}
                    spellCheck={false}
                    placeholder="https://network.johnnyli.dev/"
                    onChange={(event) => { setEndpointText(event.target.value); setNotice(null); }}
                  />
                  <small>One HTTP(S) origin per line. Leave blank to use the built-in first-party endpoint.</small>
                </label>
              </section>

              <section className="advanced-section">
                <div className="advanced-section-heading">
                  <div><strong>Network interface</strong><span>Optional native binding</span></div>
                  <span>{interfaces.length} detected</span>
                </div>
                <label className="advanced-field">
                  <span>Bind diagnostic traffic</span>
                  <select value={settings.interfaceId ?? ''} onChange={(event) => patchSettings('interfaceId', event.target.value || null)}>
                    <option value="">Automatic routing</option>
                    {interfaces.map((choice, index) => {
                      const id = interfaceId(choice);
                      if (!id) return null;
                      return <option key={`${id}-${index}`} value={id}>{interfaceLabel(choice, id)}</option>;
                    })}
                    {settings.interfaceId && !interfaces.some((choice) => interfaceId(choice) === settings.interfaceId) && (
                      <option value={settings.interfaceId}>{settings.interfaceId}</option>
                    )}
                  </select>
                  <small>Automatic routing follows the operating system. Binding forces supported probes through the selected interface.</small>
                </label>

                <label className="advanced-toggle-row">
                  <span>
                    <strong>Include local identifiers</strong>
                    <small>Allow local interface addresses and related identifiers in native measurement context and saved reports.</small>
                  </span>
                  <input
                    type="checkbox"
                    checked={settings.includeLocalIdentifiers}
                    onChange={(event) => patchSettings('includeLocalIdentifiers', event.target.checked)}
                  />
                </label>
              </section>

              <section className="advanced-section">
                <div className="advanced-section-heading">
                  <div><strong>LAN throughput client</strong><span>Optional peer measurement</span></div>
                  <span>{settings.lanTarget?.trim() ? 'Configured' : 'Off'}</span>
                </div>
                <div className="advanced-grid two">
                  <label className="advanced-field wide">
                    <span>LAN target</span>
                    <input
                      value={settings.lanTarget ?? ''}
                      spellCheck={false}
                      placeholder="192.168.1.50"
                      onChange={(event) => patchSettings('lanTarget', event.target.value || null)}
                    />
                    <small>Hostname or local IP of another Network Diagnostics LAN server. Leave blank to skip LAN throughput.</small>
                  </label>
                  <label className="advanced-field">
                    <span>Port</span>
                    <input type="number" min={1024} max={65535} value={settings.lanPort} onChange={(event) => patchSettings('lanPort', Number(event.target.value))} />
                  </label>
                  <label className="advanced-field">
                    <span>Duration</span>
                    <div className="advanced-input-unit"><input type="number" min={3} max={30} value={settings.lanDurationSeconds} onChange={(event) => patchSettings('lanDurationSeconds', Number(event.target.value))} /><span>sec</span></div>
                  </label>
                  <label className="advanced-field">
                    <span>Connections</span>
                    <input type="number" min={1} max={16} value={settings.lanConnections} onChange={(event) => patchSettings('lanConnections', Number(event.target.value))} />
                  </label>
                </div>
              </section>

              <section className="advanced-section advanced-server-section">
                <div className="advanced-section-heading">
                  <div><strong>LAN throughput server</strong><span>Native listener for another device</span></div>
                  <span className={`advanced-server-state ${serverRunning ? 'running' : ''}`}><i />{serverRunning ? 'Listening' : 'Stopped'}</span>
                </div>
                <div className="advanced-server-card">
                  <div><strong>TCP port {settings.lanPort}</strong><p>Start a local native listener, then point another Network Diagnostics client at this machine.</p></div>
                  <button type="button" disabled={busy} className={serverRunning ? 'stop' : ''} onClick={() => void toggleLanServer()}>{serverRunning ? 'Stop server' : 'Start server'}</button>
                </div>
              </section>

              <section className="advanced-section advanced-preflight-section">
                <div className="advanced-section-heading">
                  <div><strong>Native preflight</strong><span>{profileLabel(profile)} · {methodLabel(method)}</span></div>
                  <span>{preflight ? 'Complete' : 'Not run'}</span>
                </div>
                {preflight ? (
                  <div className="advanced-preflight-result">
                    <div><span>Endpoint</span><strong>{endpoint ?? 'Native selection complete'}</strong></div>
                    <div><span>Interface</span><strong>{measuredInterface ?? settings.interfaceId ?? 'Automatic routing'}</strong></div>
                    <div><span>Interfaces seen</span><strong>{preflight.interfaces?.length ?? interfaces.length}</strong></div>
                    <details>
                      <summary>Technical preflight payload</summary>
                      <pre>{JSON.stringify(preflight.measurement ?? preflight, null, 2)}</pre>
                    </details>
                  </div>
                ) : (
                  <p className="advanced-muted">Preflight applies the saved endpoint and interface configuration without starting the full throughput run.</p>
                )}
              </section>
            </>
          )}
        </div>

        {!loading && (
          <footer className="advanced-footer">
            <button type="button" className="advanced-secondary" disabled={busy} onClick={() => void runPreflight()}>Run preflight</button>
            <button type="button" className="advanced-primary" disabled={busy} onClick={() => void save()}>{busy ? 'Working…' : 'Save configuration'}</button>
          </footer>
        )}
      </aside>
    </div>
  );
}

function focusableElements(root: HTMLElement | null): HTMLElement[] {
  if (!root) return [];
  return Array.from(root.querySelectorAll<HTMLElement>('button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])'));
}

function interfaceId(choice: InterfaceChoice): string | null {
  return stringValue(choice, ['id', 'interfaceId', 'adapterId', 'name']);
}

function interfaceLabel(choice: InterfaceChoice, fallback: string): string {
  const name = stringValue(choice, ['displayName', 'name', 'description', 'interfaceName']);
  const type = stringValue(choice, ['type', 'interfaceType']);
  return [name ?? fallback, type].filter(Boolean).join(' · ');
}

function stringValue(record: Record<string, unknown>, keys: string[]): string | null {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'string' && value.trim()) return value;
  }
  return null;
}

function findString(value: unknown, keys: string[]): string | null {
  if (!value || typeof value !== 'object') return null;
  const record = value as Record<string, unknown>;
  const direct = stringValue(record, keys);
  if (direct) return direct;
  for (const child of Object.values(record)) {
    if (Array.isArray(child)) {
      for (const item of child) {
        const found = findString(item, keys);
        if (found) return found;
      }
    } else if (child && typeof child === 'object') {
      const found = findString(child, keys);
      if (found) return found;
    }
  }
  return null;
}

function firstUrl(value: unknown): string | null {
  if (typeof value === 'string' && /^https?:\/\//i.test(value)) return value;
  if (!value || typeof value !== 'object') return null;
  for (const child of Object.values(value as Record<string, unknown>)) {
    if (Array.isArray(child)) {
      for (const item of child) {
        const found = firstUrl(item);
        if (found) return found;
      }
    } else {
      const found = firstUrl(child);
      if (found) return found;
    }
  }
  return null;
}

function profileLabel(profile: DiagnosticProfile): string {
  switch (profile) {
    case 'connection-check': return 'Connection Check';
    case 'quick': return 'Quick';
    case 'full': return 'Full';
    case 'stress': return 'Stress';
  }
}

function methodLabel(method: TransferMethod): string {
  switch (method) {
    case 'single': return 'Single flow';
    case 'aggregate': return 'Aggregate flows';
    case 'compare': return 'Single + aggregate';
  }
}

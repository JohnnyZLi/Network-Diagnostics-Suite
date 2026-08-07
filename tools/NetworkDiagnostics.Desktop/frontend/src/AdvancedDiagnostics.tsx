import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import './advanced.css';
import './workbench.css';

type TransferMethod = 'compare' | 'single' | 'aggregate';
type DiagnosticProfile = 'connection-check' | 'quick' | 'full' | 'stress';
type AdvancedTool = 'targeting' | 'lan' | 'preflight';

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
type PreflightResult = { measurement?: unknown; interfaces?: InterfaceChoice[] };
type LanServerStatus = { running: boolean; port?: number };
type LanServerStart = { running: boolean; port: number };

export type AdvancedRuntimeStatus = {
  hasOverrides: boolean;
  summary: string;
  serverRunning: boolean;
  serverPort: number | null;
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

export function AdvancedDiagnostics({
  profile,
  method,
  onStatusChange,
  resetRequest = 0,
}: {
  profile: DiagnosticProfile;
  method: TransferMethod;
  onStatusChange?: (status: AdvancedRuntimeStatus) => void;
  resetRequest?: number;
}) {
  const preflightRef = useRef<HTMLElement>(null);
  const persistedSettings = useRef<AdvancedSettings>(defaultSettings);
  const handledResetRequest = useRef(0);
  const [tool, setTool] = useState<AdvancedTool | null>(null);
  const [settings, setSettings] = useState<AdvancedSettings>(defaultSettings);
  const [endpointText, setEndpointText] = useState('');
  const [interfaces, setInterfaces] = useState<InterfaceChoice[]>([]);
  const [preflight, setPreflight] = useState<PreflightResult | null>(null);
  const [serverRunning, setServerRunning] = useState(false);
  const [serverPort, setServerPort] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!desktopBridge.available) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);

    void Promise.all([
      desktopBridge.request<AdvancedSettings>('settings.getAdvanced'),
      desktopBridge.request<InterfaceChoice[]>('diagnostic.interfaces'),
      desktopBridge.request<LanServerStatus>('lan.server.status'),
    ]).then(([saved, choices, server]) => {
      if (cancelled) return;
      const activePort = server.running ? server.port ?? saved.lanPort : null;
      persistedSettings.current = saved;
      setSettings(saved);
      setEndpointText(saved.endpointCandidates.join('\n'));
      setInterfaces(choices);
      setServerRunning(server.running);
      setServerPort(activePort);
      setDirty(false);
      emitStatus(saved, server.running, activePort);
    }).catch((value: Error) => {
      if (!cancelled) setError(value.message);
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const removeStarted = desktopBridge.on<{ port: number }>('lan.server.started', (next) => {
      setServerRunning(true);
      setServerPort(next.port);
      setNotice('LAN throughput server started.');
      emitStatus(persistedSettings.current, true, next.port);
    });
    const removeStopped = desktopBridge.on<{ port: number }>('lan.server.stopped', () => {
      setServerRunning(false);
      setServerPort(null);
      emitStatus(persistedSettings.current, false, null);
    });
    const removeFailed = desktopBridge.on<{ message: string }>('lan.server.failed', (next) => {
      setServerRunning(false);
      setServerPort(null);
      setError(next.message);
      emitStatus(persistedSettings.current, false, null);
    });
    return () => {
      removeStarted();
      removeStopped();
      removeFailed();
    };
  }, []);

  useEffect(() => {
    if (!resetRequest || resetRequest === handledResetRequest.current || loading) return;
    handledResetRequest.current = resetRequest;
    void resetToDefaults();
  }, [resetRequest, loading]);

  function emitStatus(saved: AdvancedSettings, running: boolean, activePort: number | null) {
    onStatusChange?.(runtimeStatus(saved, running, activePort));
  }

  function patchSettings<K extends keyof AdvancedSettings>(key: K, value: AdvancedSettings[K]) {
    setSettings((current) => ({ ...current, [key]: value }));
    setDirty(true);
    setNotice(null);
  }

  function normalizedEndpoints(): string[] {
    return [...new Set(endpointText.split(/\r?\n/).map((value) => value.trim()).filter(Boolean))];
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
    persistedSettings.current = next;
    setSettings(next);
    setEndpointText(next.endpointCandidates.join('\n'));
    setDirty(false);
    emitStatus(next, serverRunning, serverPort);
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

  async function resetToDefaults() {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const next = await desktopBridge.request<AdvancedSettings>('settings.setAdvanced', defaultSettings);
      persistedSettings.current = next;
      setSettings(next);
      setEndpointText(next.endpointCandidates.join('\n'));
      setPreflight(null);
      setDirty(false);
      emitStatus(next, serverRunning, serverPort);
      setNotice(serverRunning
        ? `Advanced run configuration reset. The LAN server is still listening on :${serverPort ?? settings.lanPort}.`
        : 'Advanced run configuration reset to defaults.');
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Advanced configuration could not be reset.');
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
      setTool('preflight');
      window.requestAnimationFrame(() => preflightRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
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
        setServerPort(null);
        emitStatus(persistedSettings.current, false, null);
        setNotice('LAN throughput server stopped.');
      } else {
        const saved = await persist();
        if (!saved) return;
        const started = await desktopBridge.request<LanServerStart>('lan.server.start', { port: saved.lanPort });
        setServerRunning(started.running);
        setServerPort(started.running ? started.port : null);
        emitStatus(saved, started.running, started.running ? started.port : null);
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
  const displayedServerPort = serverPort ?? settings.lanPort;

  return (
    <section id="advanced-diagnostics" className="workbench-section advanced-workbench" aria-labelledby="advanced-workbench-title">
      <div className="workbench-section-header advanced-workbench-header">
        <div>
          <h2 id="advanced-workbench-title">ADVANCED DIAGNOSTICS</h2>
          <p>Target endpoints, test the LAN, or run a native preflight.</p>
        </div>
        <div className="advanced-header-status">
          {dirty && <span className="advanced-dirty-state">Unsaved changes</span>}
          {serverRunning && <span className="advanced-server-state running"><i />LAN server · :{displayedServerPort}</span>}
        </div>
      </div>

      {error && <div className="advanced-error" role="alert">{error}</div>}
      {notice && <div className="advanced-notice" role="status">{notice}</div>}

      {loading ? (
        <div className="workbench-loading"><span className="advanced-loader" /><strong>Reading native configuration</strong><p>Settings and interface choices stay on this computer.</p></div>
      ) : (
        <>
          <div className="advanced-tool-grid" aria-label="Advanced diagnostic tools">
            <AdvancedToolCard
              title="Targeting"
              description="Endpoints, interface binding, and report privacy."
              status={targetingSummary(persistedSettings.current)}
              active={tool === 'targeting'}
              onClick={() => setTool((current) => current === 'targeting' ? null : 'targeting')}
            />
            <AdvancedToolCard
              title="LAN diagnostics"
              description="Local peer throughput and native server testing."
              status={serverRunning ? `Server listening on :${displayedServerPort}` : settings.lanTarget?.trim() ? 'LAN peer configured' : 'Not configured'}
              active={tool === 'lan'}
              onClick={() => setTool((current) => current === 'lan' ? null : 'lan')}
            />
            <AdvancedToolCard
              title="Preflight"
              description="Verify route, endpoint, interface, and test readiness."
              status={preflight ? 'Last preflight complete' : `${profileLabel(profile)} · ${methodLabel(method)}`}
              active={tool === 'preflight'}
              onClick={() => setTool((current) => current === 'preflight' ? null : 'preflight')}
            />
          </div>

          {tool === 'targeting' && (
            <div className="advanced-workbench-pane targeting-pane">
              <section className="advanced-section">
                <div className="advanced-section-heading">
                  <div><strong>Measurement endpoints</strong><span>Ordered candidates · maximum 8</span></div>
                  <span>{normalizedEndpoints().length} configured</span>
                </div>
                <label className="advanced-field">
                  <span>Endpoint candidates</span>
                  <textarea value={endpointText} rows={3} spellCheck={false} placeholder="https://network.johnnyli.dev/" onChange={(event) => { setEndpointText(event.target.value); setDirty(true); setNotice(null); }} />
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
                  <div className="advanced-select-shell">
                    <select value={settings.interfaceId ?? ''} onChange={(event) => patchSettings('interfaceId', event.target.value || null)}>
                      <option value="">Automatic routing</option>
                      {interfaces.map((choice, index) => {
                        const id = interfaceId(choice);
                        if (!id) return null;
                        return <option key={`${id}-${index}`} value={id}>{interfaceLabel(choice, id)}</option>;
                      })}
                      {settings.interfaceId && !interfaces.some((choice) => interfaceId(choice) === settings.interfaceId) && <option value={settings.interfaceId}>{settings.interfaceId}</option>}
                    </select>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 9 5 5 5-5" /></svg>
                  </div>
                  <small>Automatic routing follows the operating system. Binding forces supported probes through the selected interface.</small>
                </label>

                <label className="advanced-toggle-row">
                  <span><strong>Include local identifiers</strong><small>Allow local interface addresses and related identifiers in native measurement context and saved reports.</small></span>
                  <input type="checkbox" checked={settings.includeLocalIdentifiers} onChange={(event) => patchSettings('includeLocalIdentifiers', event.target.checked)} />
                </label>
              </section>
              <div className="advanced-pane-actions"><button type="button" className="advanced-primary" disabled={busy} onClick={() => void save()}>{busy ? 'Working…' : 'Save targeting'}</button></div>
            </div>
          )}

          {tool === 'lan' && (
            <div className="advanced-workbench-pane lan-pane">
              <section className="advanced-section">
                <div className="advanced-section-heading">
                  <div><strong>LAN throughput client</strong><span>Optional peer measurement</span></div>
                  <span>{settings.lanTarget?.trim() ? 'Configured' : 'Off'}</span>
                </div>
                <div className="advanced-grid two">
                  <label className="advanced-field wide">
                    <span>LAN target</span>
                    <input value={settings.lanTarget ?? ''} spellCheck={false} placeholder="192.168.1.50" onChange={(event) => patchSettings('lanTarget', event.target.value || null)} />
                    <small>Hostname or local IP of another Network Diagnostics LAN server. Leave blank to skip LAN throughput.</small>
                  </label>
                  <label className="advanced-field"><span>Port</span><input type="number" min={1024} max={65535} value={settings.lanPort} onChange={(event) => patchSettings('lanPort', Number(event.target.value))} /></label>
                  <label className="advanced-field"><span>Duration</span><div className="advanced-input-unit"><input type="number" min={3} max={30} value={settings.lanDurationSeconds} onChange={(event) => patchSettings('lanDurationSeconds', Number(event.target.value))} /><span>sec</span></div></label>
                  <label className="advanced-field"><span>Connections</span><input type="number" min={1} max={16} value={settings.lanConnections} onChange={(event) => patchSettings('lanConnections', Number(event.target.value))} /></label>
                </div>
              </section>

              <section className="advanced-section advanced-server-section">
                <div className="advanced-section-heading">
                  <div><strong>LAN throughput server</strong><span>Native listener for another device</span></div>
                  <span className={`advanced-server-state ${serverRunning ? 'running' : ''}`}><i />{serverRunning ? `Listening · :${displayedServerPort}` : 'Stopped'}</span>
                </div>
                <div className="advanced-server-card">
                  <div><strong>{serverRunning ? `Listening on TCP ${displayedServerPort}` : `TCP port ${settings.lanPort}`}</strong><p>Start a local native listener, then point another Network Diagnostics client at this machine.</p></div>
                  <button type="button" disabled={busy} className={serverRunning ? 'stop' : ''} onClick={() => void toggleLanServer()}>{serverRunning ? 'Stop server' : 'Start server'}</button>
                </div>
              </section>
              <div className="advanced-pane-actions"><button type="button" className="advanced-primary" disabled={busy} onClick={() => void save()}>{busy ? 'Working…' : 'Save LAN settings'}</button></div>
            </div>
          )}

          {tool === 'preflight' && (
            <div ref={preflightRef as React.RefObject<HTMLDivElement>} className="advanced-workbench-pane preflight-pane">
              <section className="advanced-section advanced-preflight-section">
                <div className="advanced-section-heading">
                  <div><strong>Native preflight</strong><span>{profileLabel(profile)} · {methodLabel(method)}</span></div>
                  <span>{preflight ? 'Complete' : 'Not run'}</span>
                </div>
                <p className="advanced-muted">Apply the saved endpoint and interface configuration and inspect native target selection without starting the full throughput run.</p>
                <button type="button" className="advanced-secondary advanced-run-preflight" disabled={busy} onClick={() => void runPreflight()}>{busy ? 'Working…' : 'Run preflight'}</button>
                {preflight && (
                  <div className="advanced-preflight-result workbench-preflight-result">
                    <div><span>Endpoint</span><strong>{endpoint ?? 'Native selection complete'}</strong></div>
                    <div><span>Interface</span><strong>{measuredInterface ?? settings.interfaceId ?? 'Automatic routing'}</strong></div>
                    <div><span>Interfaces seen</span><strong>{preflight.interfaces?.length ?? interfaces.length}</strong></div>
                    <details><summary>Technical preflight payload</summary><pre>{JSON.stringify(preflight.measurement ?? preflight, null, 2)}</pre></details>
                  </div>
                )}
              </section>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function AdvancedToolCard({ title, description, status, active, onClick }: { title: string; description: string; status: string; active: boolean; onClick: () => void }) {
  return (
    <button type="button" className={`advanced-tool-card ${active ? 'active' : ''}`} aria-expanded={active} onClick={onClick}>
      <span><strong>{title}</strong><small>{description}</small></span>
      <b>{status}</b>
      <i aria-hidden="true">{active ? '−' : '+'}</i>
    </button>
  );
}

function runtimeStatus(settings: AdvancedSettings, serverRunning: boolean, serverPort: number | null): AdvancedRuntimeStatus {
  const parts: string[] = [];
  if (settings.interfaceId) parts.push(settings.interfaceId);
  if (settings.endpointCandidates.length > 0) parts.push(`${settings.endpointCandidates.length} custom endpoint${settings.endpointCandidates.length === 1 ? '' : 's'}`);
  if (settings.includeLocalIdentifiers) parts.push('identifiers included');
  if (settings.lanTarget?.trim()) parts.push('LAN peer configured');
  return {
    hasOverrides: parts.length > 0,
    summary: parts.length > 0 ? parts.join(' · ') : 'Default routing · first-party endpoint · identifiers excluded',
    serverRunning,
    serverPort: serverRunning ? serverPort : null,
  };
}

function targetingSummary(settings: AdvancedSettings): string {
  const parts: string[] = [];
  if (settings.interfaceId) parts.push(settings.interfaceId);
  if (settings.endpointCandidates.length > 0) parts.push(`${settings.endpointCandidates.length} endpoint${settings.endpointCandidates.length === 1 ? '' : 's'}`);
  if (settings.includeLocalIdentifiers) parts.push('Identifiers included');
  return parts.length > 0 ? parts.join(' · ') : 'Automatic · first-party · private';
}

function interfaceId(choice: InterfaceChoice): string | null { return stringValue(choice, ['id', 'interfaceId', 'adapterId', 'name']); }
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
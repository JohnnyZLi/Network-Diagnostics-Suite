import { useEffect, useRef, useState } from 'react';
import { desktopBridge } from './bridge';
import type {
  AdvancedSettings,
  DiagnosticProfile,
  DownloadPathPreference,
  InterfaceChoice,
  LanServerStatus,
  LanThroughputReport,
  PreflightResult,
  TransferMethod,
} from './contracts';
import './advanced.css';

type AdvancedTool = 'configuration' | 'lan';
type LanServerStart = LanServerStatus & { running: true; port: number };

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
  downloadPath,
  initialSettings,
  initialInterfaces,
  initialPreflight,
  onSettingsChange,
  onPreflightChange,
  onStatusChange,
  initialTool = 'configuration',
  resetRequest = 0,
  preflightRequest = 0,
}: {
  profile: DiagnosticProfile;
  method: TransferMethod;
  downloadPath: DownloadPathPreference;
  initialSettings?: AdvancedSettings | null;
  initialInterfaces?: InterfaceChoice[];
  initialPreflight?: PreflightResult | null;
  onSettingsChange?: (settings: AdvancedSettings) => void;
  onPreflightChange?: (preflight: PreflightResult) => void;
  onStatusChange?: (status: AdvancedRuntimeStatus) => void;
  initialTool?: AdvancedTool;
  resetRequest?: number;
  preflightRequest?: number;
}) {
  const preflightRef = useRef<HTMLElement>(null);
  const persistedSettings = useRef<AdvancedSettings>(initialSettings ?? defaultSettings);
  // A remount can happen when the normal Run Diagnostics interface control updates
  // persisted configuration. Treat the current reset token as already handled so a
  // historical Reset click cannot unexpectedly wipe a later interface selection.
  const handledResetRequest = useRef(resetRequest);
  const handledPreflightRequest = useRef(preflightRequest);
  const [tool, setTool] = useState<AdvancedTool>(initialTool);
  const [settings, setSettings] = useState<AdvancedSettings>(initialSettings ?? defaultSettings);
  const [endpointText, setEndpointText] = useState((initialSettings ?? defaultSettings).endpointCandidates.join('\n'));
  const [interfaces, setInterfaces] = useState<InterfaceChoice[]>(initialInterfaces ?? []);
  const [preflight, setPreflight] = useState<PreflightResult | null>(initialPreflight ?? null);
  const [serverRunning, setServerRunning] = useState(false);
  const [serverPort, setServerPort] = useState<number | null>(null);
  const [serverAddresses, setServerAddresses] = useState<string[]>([]);
  const [lanClientRunning, setLanClientRunning] = useState(false);
  const [lanClientMessage, setLanClientMessage] = useState<string | null>(null);
  const [lanResult, setLanResult] = useState<LanThroughputReport | null>(null);
  const [loading, setLoading] = useState(!initialSettings);
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
      initialSettings ? Promise.resolve(initialSettings) : desktopBridge.request<AdvancedSettings>('settings.getAdvanced'),
      initialInterfaces?.length ? Promise.resolve(initialInterfaces) : desktopBridge.request<InterfaceChoice[]>('diagnostic.interfaces'),
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
      setServerAddresses(server.addresses ?? []);
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
    const removeStarted = desktopBridge.on<LanServerStart>('lan.server.started', (next) => {
      setServerRunning(true);
      setServerPort(next.port);
      setServerAddresses(next.addresses ?? []);
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
    const removeClientProgress = desktopBridge.on<{ message: string }>('lan.client.progress', (next) => {
      setLanClientRunning(true);
      setLanClientMessage(next.message);
    });
    const removeClientCompleted = desktopBridge.on<{ report: LanThroughputReport }>('lan.client.completed', (next) => {
      setLanClientRunning(false);
      setLanClientMessage('LAN throughput measurement complete.');
      setLanResult(next.report);
      setNotice('LAN throughput measurement complete.');
    });
    const removeClientCancelled = desktopBridge.on('lan.client.cancelled', () => {
      setLanClientRunning(false);
      setLanClientMessage(null);
      setNotice('LAN throughput measurement cancelled.');
    });
    const removeClientFailed = desktopBridge.on<{ message: string }>('lan.client.failed', (next) => {
      setLanClientRunning(false);
      setLanClientMessage(null);
      setError(next.message);
    });
    return () => { removeStarted(); removeStopped(); removeFailed(); removeClientProgress(); removeClientCompleted(); removeClientCancelled(); removeClientFailed(); };
  }, []);

  useEffect(() => {
    if (!resetRequest || resetRequest === handledResetRequest.current || loading) return;
    handledResetRequest.current = resetRequest;
    void resetToDefaults();
  }, [resetRequest, loading]);

  useEffect(() => {
    if (!preflightRequest || preflightRequest === handledPreflightRequest.current || loading) return;
    handledPreflightRequest.current = preflightRequest;
    setTool('configuration');
    void runPreflight();
  }, [preflightRequest, loading]);

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
    onSettingsChange?.(next);
    emitStatus(next, serverRunning, serverPort);
    return next;
  }

  async function save() {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (await persist()) setNotice('Test configuration saved locally.');
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
      onSettingsChange?.(next);
      emitStatus(next, serverRunning, serverPort);
      setNotice(serverRunning
        ? `Run configuration reset. The LAN server is still listening on :${serverPort ?? settings.lanPort}.`
        : 'Advanced configuration reset to defaults.');
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
      const result = await desktopBridge.request<PreflightResult>('diagnostic.preflight', { profile, method, downloadPath });
      setPreflight(result);
      if (result.interfaces?.length) setInterfaces(result.interfaces);
      onPreflightChange?.(result);
      setNotice('Native preflight completed with the saved configuration.');
      setTool('configuration');
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
        setServerAddresses(started.addresses ?? []);
        emitStatus(saved, started.running, started.running ? started.port : null);
        setNotice(`LAN throughput server listening on port ${started.port}.`);
      }
    } catch (value) {
      setError(value instanceof Error ? value.message : 'LAN throughput server could not be updated.');
    } finally {
      setBusy(false);
    }
  }

  async function runLanClient() {
    if (lanClientRunning) {
      try {
        await desktopBridge.request('lan.client.cancel');
      } catch (value) {
        setError(value instanceof Error ? value.message : 'The LAN measurement could not be cancelled.');
      }
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    setLanResult(null);
    try {
      const saved = await persist();
      if (!saved) return;
      if (!saved.lanTarget?.trim()) {
        setError('Enter the hostname or local IP address of a Network Diagnostics LAN server.');
        return;
      }
      await desktopBridge.request('lan.client.run', {
        target: saved.lanTarget,
        port: saved.lanPort,
        durationSeconds: saved.lanDurationSeconds,
        connections: saved.lanConnections,
        interfaceId: saved.interfaceId ?? null,
      });
      setLanClientRunning(true);
      setLanClientMessage(`Connecting to ${saved.lanTarget}:${saved.lanPort}…`);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'The LAN throughput measurement could not start.');
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
          <h1 id="advanced-workbench-title">Advanced Diagnostics</h1>
          <p>Configure how native diagnostics reach the network, validate that setup, or use dedicated LAN tools.</p>
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
          <div className="advanced-tool-grid advanced-tool-grid-two" aria-label="Advanced diagnostic tools">
            <AdvancedToolCard
              title="Test configuration"
              description="Endpoints, interface binding, report privacy, and native preflight."
              status={targetingSummary(persistedSettings.current)}
              active={tool === 'configuration'}
              onClick={() => setTool('configuration')}
            />
            <AdvancedToolCard
              title="LAN tools"
              description="Local peer throughput and native server testing."
              status={serverRunning ? `Server listening on :${displayedServerPort}` : settings.lanTarget?.trim() ? 'LAN peer configured' : 'Not configured'}
              active={tool === 'lan'}
              onClick={() => setTool('lan')}
            />
          </div>

          {tool === 'configuration' && (
            <div className="advanced-workbench-pane targeting-pane">
              <div className="advanced-config-layout">
                <section className="advanced-section">
                  <div className="advanced-section-heading"><div><strong>Measurement endpoints</strong><span>Ordered candidates · maximum 8</span></div><span>{normalizedEndpoints().length} configured</span></div>
                  <label className="advanced-field"><span>Endpoint candidates</span><textarea value={endpointText} rows={4} spellCheck={false} placeholder="https://network.johnnyli.dev/" onChange={(event) => { setEndpointText(event.target.value); setDirty(true); setNotice(null); }} /><small>One HTTP(S) origin per line. Leave blank to use the built-in first-party endpoint.</small></label>
                </section>

                <section className="advanced-section">
                  <div className="advanced-section-heading"><div><strong>Network interface</strong><span>Optional native binding</span></div><span>{interfaces.length} detected</span></div>
                  <label className="advanced-field">
                    <span>Bind diagnostic traffic</span>
                    <div className="advanced-select-shell">
                      <select data-interface-picker aria-label="Advanced network interface" value={settings.interfaceId ?? ''} onChange={(event) => patchSettings('interfaceId', event.target.value || null)}>
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
                  <label className="advanced-toggle-row"><span><strong>Include local identifiers</strong><small>Allow local interface addresses and related identifiers in native measurement context and saved reports.</small></span><input type="checkbox" checked={settings.includeLocalIdentifiers} onChange={(event) => patchSettings('includeLocalIdentifiers', event.target.checked)} /></label>
                </section>
              </div>

              <section ref={preflightRef} className="advanced-section advanced-preflight-section embedded-preflight">
                <div className="advanced-section-heading"><div><strong>Validate setup</strong><span>Native preflight · {profileLabel(profile)} · {methodLabel(method)}</span></div><span>{preflight ? 'Last check complete' : 'Not run'}</span></div>
                <p className="advanced-muted">Verify endpoint selection, routing, and interface binding without starting the full throughput run.</p>
                <div className="advanced-preflight-actions">
                  <button type="button" className="advanced-primary" disabled={busy || !dirty} onClick={() => void save()}>{busy ? 'Working…' : 'Save configuration'}</button>
                  <button type="button" className="advanced-secondary advanced-run-preflight" disabled={busy} onClick={() => void runPreflight()}>{busy ? 'Working…' : dirty ? 'Save & run preflight' : 'Run preflight'}</button>
                </div>
                {preflight && (
                  <div className="advanced-preflight-result workbench-preflight-result">
                    <div><span>Endpoint</span><strong>{endpoint ?? 'Native selection complete'}</strong></div>
                    <div><span>Interface</span><strong>{measuredInterface ?? settings.interfaceId ?? 'Automatic routing'}</strong></div>
                    <div><span>Interfaces seen</span><strong>{preflight.interfaces?.length ?? interfaces.length}</strong></div>
                    <details><summary>Technical preflight data</summary><pre>{JSON.stringify(preflight.measurement ?? preflight, null, 2)}</pre></details>
                  </div>
                )}
              </section>
            </div>
          )}

          {tool === 'lan' && (
            <div className="advanced-workbench-pane lan-pane">
              <section className="advanced-section">
                <div className="advanced-section-heading"><div><strong>LAN throughput client</strong><span>Optional peer measurement</span></div><span>{settings.lanTarget?.trim() ? 'Configured' : 'Off'}</span></div>
                <div className="advanced-grid two">
                  <label className="advanced-field wide"><span>LAN target</span><input value={settings.lanTarget ?? ''} spellCheck={false} placeholder="192.168.1.50" onChange={(event) => patchSettings('lanTarget', event.target.value || null)} /><small>Hostname or local IP of another Network Diagnostics LAN server. Leave blank to skip LAN throughput.</small></label>
                  <label className="advanced-field"><span>Port</span><input type="number" min={1024} max={65535} value={settings.lanPort} onChange={(event) => patchSettings('lanPort', Number(event.target.value))} /></label>
                  <label className="advanced-field"><span>Duration</span><div className="advanced-input-unit"><input type="number" min={3} max={30} value={settings.lanDurationSeconds} onChange={(event) => patchSettings('lanDurationSeconds', Number(event.target.value))} /><span>sec</span></div></label>
                  <label className="advanced-field"><span>Connections</span><input type="number" min={1} max={16} value={settings.lanConnections} onChange={(event) => patchSettings('lanConnections', Number(event.target.value))} /></label>
                </div>
                <div className="advanced-lan-client-actions">
                  <button type="button" className={lanClientRunning ? 'advanced-secondary stop' : 'advanced-primary'} disabled={busy || (!lanClientRunning && !settings.lanTarget?.trim())} onClick={() => void runLanClient()}>{lanClientRunning ? 'Cancel LAN test' : dirty ? 'Save & run LAN test' : 'Run LAN test'}</button>
                  {lanClientMessage && <span className="lan-client-progress" role="status"><i />{lanClientMessage}</span>}
                </div>
                {lanResult && <div className="lan-result-instrument" aria-label="LAN throughput result"><div><span>Latency</span><strong>{lanResult.latency.medianMs == null ? '—' : `${formatNumber(lanResult.latency.medianMs)} ms`}</strong><small>{formatNumber(lanResult.latency.lossPercent)}% loss</small></div><div><span>Download</span><strong>{formatNumber(lanResult.downloadMbps)} Mbps</strong><small>{formatBytes(lanResult.downloadBytes)} received</small></div><div><span>Upload</span><strong>{formatNumber(lanResult.uploadMbps)} Mbps</strong><small>{formatBytes(lanResult.uploadBytes)} sent</small></div><div><span>Peer</span><strong>{lanResult.resolvedAddress || lanResult.target}</strong><small>{lanResult.concurrency} streams · {formatNumber(lanResult.durationMs / 1000)} sec each way</small></div></div>}
              </section>

              <section className="advanced-section advanced-server-section">
                <div className="advanced-section-heading"><div><strong>LAN throughput server</strong><span>Native listener for another device</span></div><span className={`advanced-server-state ${serverRunning ? 'running' : ''}`}><i />{serverRunning ? `Listening · :${displayedServerPort}` : 'Stopped'}</span></div>
                <div className="advanced-server-card"><div><strong>{serverRunning ? `Listening on TCP ${displayedServerPort}` : `TCP port ${settings.lanPort}`}</strong><p>Start the listener on this machine. On the other device, enter one of the targets below and run the LAN client test.</p></div><button type="button" disabled={busy} className={serverRunning ? 'stop' : ''} onClick={() => void toggleLanServer()}>{serverRunning ? 'Stop server' : dirty ? 'Save & start server' : 'Start server'}</button></div>
                {serverRunning && <div className="lan-pairing-targets"><span>PAIRING TARGETS</span>{serverAddresses.length > 0 ? serverAddresses.map((address) => <button type="button" key={address} title="Copy LAN target" onClick={() => void navigator.clipboard?.writeText(address)}><strong>{address}</strong><small>TCP {displayedServerPort} · Copy</small></button>) : <p>No usable IPv4 address was detected. Use this machine’s LAN hostname with port {displayedServerPort}.</p>}</div>}
              </section>
              <div className="advanced-pane-actions"><button type="button" className="advanced-primary" disabled={busy || !dirty} onClick={() => void save()}>{busy ? 'Working…' : 'Save LAN settings'}</button></div>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function AdvancedToolCard({ title, description, status, active, onClick }: { title: string; description: string; status: string; active: boolean; onClick: () => void }) {
  return <button type="button" className={`advanced-tool-card ${active ? 'active' : ''}`} aria-expanded={active} onClick={onClick}><span><strong>{title}</strong><small>{description}</small></span><b>{status}</b><i aria-hidden="true">{active ? '−' : '+'}</i></button>;
}

function runtimeStatus(settings: AdvancedSettings, serverRunning: boolean, serverPort: number | null): AdvancedRuntimeStatus {
  const parts: string[] = [];
  if (settings.interfaceId) parts.push(settings.interfaceId);
  if (settings.endpointCandidates.length > 0) parts.push(`${settings.endpointCandidates.length} custom endpoint${settings.endpointCandidates.length === 1 ? '' : 's'}`);
  if (settings.includeLocalIdentifiers) parts.push('identifiers included');
  if (settings.lanTarget?.trim()) parts.push('LAN peer configured');
  return { hasOverrides: parts.length > 0, summary: parts.length > 0 ? parts.join(' · ') : 'Default routing · first-party endpoint · identifiers excluded', serverRunning, serverPort: serverRunning ? serverPort : null };
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
  const name = stringValue(choice, ['displayName', 'name', 'interfaceName']) ?? fallback;
  const description = stringValue(choice, ['description']);
  const type = stringValue(choice, ['type', 'interfaceType']);
  const speed = numberValue(choice, ['linkSpeedMbps']);
  const families = [choice.supportsIpv4 === true ? 'IPv4' : null, choice.supportsIpv6 === true ? 'IPv6' : null].filter(Boolean).join(' + ');
  const speedLabel = speed == null ? null : speed >= 1000 ? `${formatNumber(speed / 1000)} Gbps link` : `${formatNumber(speed)} Mbps link`;
  return [name, description && description !== name ? description : null, type, speedLabel, families].filter(Boolean).join(' · ');
}
function stringValue(record: Record<string, unknown>, keys: string[]): string | null { for (const key of keys) { const value = record[key]; if (typeof value === 'string' && value.trim()) return value; } return null; }
function numberValue(record: Record<string, unknown>, keys: string[]): number | null { for (const key of keys) { const value = record[key]; if (typeof value === 'number' && Number.isFinite(value)) return value; } return null; }
function formatNumber(value: number): string { return new Intl.NumberFormat(undefined, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value); }
function formatBytes(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return '0 MB';
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)} GB`;
  return `${(value / 1_000_000).toFixed(value >= 100_000_000 ? 0 : 1)} MB`;
}
function findString(value: unknown, keys: string[]): string | null {
  if (!value || typeof value !== 'object') return null;
  const record = value as Record<string, unknown>;
  const direct = stringValue(record, keys);
  if (direct) return direct;
  for (const child of Object.values(record)) {
    if (Array.isArray(child)) { for (const item of child) { const found = findString(item, keys); if (found) return found; } }
    else if (child && typeof child === 'object') { const found = findString(child, keys); if (found) return found; }
  }
  return null;
}
function firstUrl(value: unknown): string | null {
  if (typeof value === 'string' && /^https?:\/\//i.test(value)) return value;
  if (!value || typeof value !== 'object') return null;
  for (const child of Object.values(value as Record<string, unknown>)) {
    if (Array.isArray(child)) { for (const item of child) { const found = firstUrl(item); if (found) return found; } }
    else { const found = firstUrl(child); if (found) return found; }
  }
  return null;
}
function profileLabel(profile: DiagnosticProfile): string { switch (profile) { case 'connection-check': return 'Connection Check'; case 'quick': return 'Quick'; case 'full': return 'Full'; case 'stress': return 'Stress'; } }
function methodLabel(method: TransferMethod): string { switch (method) { case 'single': return 'Single flow'; case 'aggregate': return 'Aggregate flows'; case 'compare': return 'Single + aggregate'; } }

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const bridgeHarness = vi.hoisted(() => {
  const listeners = new Map<string, Set<(payload: unknown) => void>>();
  const requests: Array<{ method: string; payload: Record<string, unknown> }> = [];
  let responder: (method: string, payload: Record<string, unknown>) => unknown = () => ({});
  return {
    listeners,
    requests,
    setResponder(next: typeof responder) { responder = next; },
    emit(event: string, payload: unknown) { for (const listener of listeners.get(event) ?? []) listener(payload); },
    bridge: {
      available: true,
      request: vi.fn(async (method: string, payload: Record<string, unknown> = {}) => {
        requests.push({ method, payload });
        return responder(method, payload);
      }),
      on: vi.fn((event: string, listener: (payload: unknown) => void) => {
        const bucket = listeners.get(event) ?? new Set();
        bucket.add(listener);
        listeners.set(event, bucket);
        return () => bucket.delete(listener);
      }),
    },
  };
});

vi.mock('../src/bridge', () => ({ desktopBridge: bridgeHarness.bridge }));

import App from '../src/App';
import { AdvancedDiagnostics } from '../src/AdvancedDiagnostics';
import { CommandPalette, type PaletteCommand } from '../src/CommandPalette';
import { SettingsMenu } from '../src/SettingsMenu';

const advancedSettings = {
  endpointCandidates: [],
  interfaceId: null,
  includeLocalIdentifiers: false,
  lanTarget: null,
  lanPort: 8765,
  lanDurationSeconds: 8,
  lanConnections: 4,
};

const monitor = {
  enabled: true,
  running: true,
  window: '5m',
  score: 96,
  band: 'excellent',
  status: 'Excellent',
  summary: 'Normal range',
  deviceName: 'Test Mac',
  interfaceName: 'en0',
  lastUpdated: new Date().toISOString(),
  unreadAlertCount: 0,
  responsiveness: { title: 'Responsiveness', score: 95, band: 'excellent', status: 'Excellent', summary: 'Responsive', metrics: [] },
  reliability: { title: 'Reliability', score: 100, band: 'excellent', status: 'Excellent', summary: 'Available', metrics: [] },
  speed: { title: 'Capacity', score: 90, band: 'good', status: 'Good', summary: 'Measured', metrics: [] },
  timeline: [],
  alerts: [],
};

const plan = {
  profile: 'connection-check', profileName: 'Connection Check', method: 'compare', downloadPath: 'automatic',
  estimatedSeconds: 15, internetEstimatedSeconds: 15, transferCapBytes: 28_000_000, internetTransferCapBytes: 28_000_000,
  includeServices: false, serviceCheckCount: 0, deepDiagnostics: false, diagnosticDepth: 'Core native set',
  idlePingCount: 8, pingIntervalMs: 150, downloadStages: [], uploadStages: [], downloadRuns: 2,
  maxDownloadConnections: 2, maxUploadConnections: 2, totalTransferStages: 3, lanEnabled: false, lanEstimatedSeconds: 0,
};

function reportDetail(id: string, verdict: string, savedLocally = true) {
  return {
    report: { id, generatedAt: new Date().toISOString(), storedAt: new Date().toISOString(), profile: 'connection-check', profileName: 'Connection Check', tags: [], savedLocally, outcome: 'success', outcomeLabel: 'Healthy' },
    context: 'Connection Check · Compare',
    method: 'compare',
    measurement: { endpoint: { origin: 'https://network.johnnyli.dev/' } },
    technicalReport: { schemaVersion: '2.0', run: { id } },
    presentation: { outcome: 'success', label: 'HEALTHY', verdict, summary: `${verdict} summary`, nextAction: 'No action required.', metrics: [], findings: [], technicalData: ['schema 2.0'] },
  };
}

beforeEach(() => {
  bridgeHarness.listeners.clear();
  bridgeHarness.requests.length = 0;
  bridgeHarness.bridge.request.mockClear();
  bridgeHarness.bridge.on.mockClear();
});

describe('desktop result state', () => {
  it('renders the active-run instrument immediately after the native run is accepted', async () => {
    const user = userEvent.setup();
    bridgeHarness.setResponder((method) => {
      if (method === 'app.ready') return { product: 'Network Diagnostics', host: 'photino', platform: 'macOS', architecture: 'Arm64', appearance: 'dark', monitor };
      if (method === 'diagnostic.describePlan') return plan;
      if (method === 'settings.getAdvanced') return advancedSettings;
      if (method === 'diagnostic.interfaces') return [];
      if (method === 'diagnostic.preflight') return { measurement: {}, interfaces: [], downloadPath: { requestedPath: 'automatic', selectedPath: 'worker', r2ProbeStatus: 'available' } };
      if (method === 'reports.list') return [];
      if (method === 'diagnostic.run') return { runId: 'active-run', profile: 'connection-check', method: 'compare', downloadPath: 'automatic', transferCapBytes: 28_000_000, estimatedSeconds: 15, totalStages: 6 };
      return {};
    });

    render(<App />);
    await user.click(screen.getByRole('button', { name: 'Diagnostics' }));
    await user.click(await screen.findByRole('button', { name: 'Run Connection' }));

    expect(await screen.findByRole('button', { name: 'Cancel run' })).toBeTruthy();
    expect(screen.getByText('LIVE MEASUREMENTS')).toBeTruthy();
    expect(screen.getByText('ACTIVE PATH')).toBeTruthy();
  });

  it('shows the newly completed native detail without re-reading or mixing the prior saved report', async () => {
    const oldDetail = reportDetail('old-report', 'Old saved verdict');
    bridgeHarness.setResponder((method) => {
      if (method === 'app.ready') return { product: 'Network Diagnostics', host: 'photino', platform: 'Unix', architecture: 'Arm64', appearance: 'light', monitor };
      if (method === 'diagnostic.describePlan') return plan;
      if (method === 'settings.getAdvanced') return advancedSettings;
      if (method === 'diagnostic.interfaces') return [];
      if (method === 'diagnostic.preflight') return { measurement: {}, interfaces: [], downloadPath: { requestedPath: 'automatic', selectedPath: 'worker', r2ProbeStatus: 'available' } };
      if (method === 'reports.list') return [oldDetail.report];
      if (method === 'reports.get') return oldDetail;
      return {};
    });
    render(<App />);
    await screen.findByText('Old saved verdict');

    const nextDetail = reportDetail('new-report', 'New current verdict', false);
    bridgeHarness.emit('diagnostic.completed', {
      runId: 'bridge-run', reportId: 'new-report', generatedAt: new Date().toISOString(), profile: 'connection-check', method: 'compare',
      savedLocally: false, storageError: 'Disk unavailable', detail: nextDetail, latencyMs: 18, downloadMbps: 210, uploadMbps: 65, requestLossPercent: 0,
    });
    await userEvent.click(screen.getByRole('button', { name: 'Diagnostics' }));

    expect(await screen.findByText('New current verdict')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Retry save' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Open saved report' })).toBeNull();
    const newReportReads = bridgeHarness.requests.filter((request) => request.method === 'reports.get' && request.payload.id === 'new-report');
    expect(newReportReads).toHaveLength(0);
  });
});

describe('settings and dialog accessibility', () => {
  it('saves monitoring and report preferences and restores trigger focus', async () => {
    const user = userEvent.setup();
    bridgeHarness.setResponder((method, payload) => {
      if (method === 'settings.get') return { appearance: 'system', monitoringEnabled: true, monitoringWindow: '5m', monitoringIntervalSeconds: 5, monitoringAlertScoreThreshold: 70, expectedDownloadMbps: 100, expectedUploadMbps: 20, reportsDirectory: null, effectiveReportsDirectory: '/tmp/reports', reportRetentionDays: 0 };
      if (method === 'settings.setPreferences') return { settings: { appearance: 'system', monitoringEnabled: true, monitoringWindow: '5m', expectedDownloadMbps: 100, expectedUploadMbps: 20, ...payload }, prunedReports: 0, effectiveReportsDirectory: '/tmp/reports' };
      return {};
    });
    render(<SettingsMenu appearance="system" onAppearanceChange={() => undefined} />);
    const trigger = screen.getByRole('button', { name: 'Settings' });
    await user.click(trigger);
    const interval = await screen.findByLabelText('Sample interval');
    await user.clear(interval);
    await user.type(interval, '12');
    await user.click(screen.getByRole('button', { name: 'Save monitoring & reports' }));
    await waitFor(() => expect(bridgeHarness.requests.some((request) => request.method === 'settings.setPreferences' && request.payload.monitoringIntervalSeconds === 12)).toBe(true));
    await user.keyboard('{Escape}');
    expect(document.activeElement).toBe(trigger);
  });

  it('traps focus in the command palette and restores prior focus on Escape', async () => {
    const user = userEvent.setup();
    const prior = document.createElement('button');
    prior.textContent = 'Prior';
    document.body.appendChild(prior);
    prior.focus();
    const commands: PaletteCommand[] = [{ id: 'one', title: 'Run Connection', detail: 'Start', run: () => undefined }];
    const onClose = vi.fn();
    const { rerender } = render(<CommandPalette open commands={commands} onClose={onClose} />);
    const search = await screen.findByRole('textbox', { name: 'Search commands' });
    await waitFor(() => expect(document.activeElement).toBe(search));
    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledOnce();
    rerender(<CommandPalette open={false} commands={commands} onClose={onClose} />);
    expect(document.activeElement).toBe(prior);
  });
});

describe('custom interface picker', () => {
  it('exposes active option semantics and supports keyboard selection', async () => {
    await import('../src/interface-picker');
    const select = document.createElement('select');
    select.dataset.interfacePicker = '';
    select.setAttribute('aria-label', 'Network interface');
    select.innerHTML = '<option value="">Automatic routing</option><option value="en0">Wi-Fi · 1 Gbps link</option>';
    document.body.appendChild(select);
    fireEvent.pointerDown(select);
    expect(select.getAttribute('aria-expanded')).toBe('true');
    expect(select.getAttribute('aria-activedescendant')).toContain('network-interface-picker-menu-option-0');
    fireEvent.keyDown(select, { key: 'ArrowDown' });
    fireEvent.keyDown(select, { key: 'Enter' });
    expect(select.value).toBe('en0');
    expect(select.getAttribute('aria-expanded')).toBe('false');
  });
});

describe('advanced diagnostics initialization', () => {
  it('loads native interfaces when the parent has not populated its initial list yet', async () => {
    bridgeHarness.setResponder((method) => {
      if (method === 'settings.getAdvanced') return advancedSettings;
      if (method === 'diagnostic.interfaces') return [{ id: 'en0', displayName: 'Wi-Fi', supportsIpv4: true }];
      if (method === 'lan.server.status') return { running: false, port: null, addresses: [] };
      return {};
    });

    render(<AdvancedDiagnostics
      profile="connection-check"
      method="compare"
      downloadPath="automatic"
      initialInterfaces={[]}
    />);

    expect(await screen.findByText('1 detected')).toBeTruthy();
    expect(bridgeHarness.requests.some((request) => request.method === 'diagnostic.interfaces')).toBe(true);
  });
});

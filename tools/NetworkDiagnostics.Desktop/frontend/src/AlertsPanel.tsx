import { useEffect, useState } from 'react';
import { desktopBridge } from './bridge';
import type { MonitorSnapshot } from './ContinuousDiagnostics';
import './alerts.css';

export function AlertsPanel({
  open,
  snapshot,
  onUpdate,
  onClose,
}: {
  open: boolean;
  snapshot: MonitorSnapshot | null;
  onUpdate: (snapshot: MonitorSnapshot) => void;
  onClose: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [clearConfirm, setClearConfirm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setClearConfirm(false);
      setError(null);
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target;
      if (target instanceof Element && target.closest('.settings-trigger')) onClose();
    };
    window.addEventListener('keydown', onKeyDown);
    document.addEventListener('pointerdown', onPointerDown, true);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('pointerdown', onPointerDown, true);
    };
  }, [open, onClose]);

  async function request(method: string) {
    if (!desktopBridge.available) return;
    setBusy(true);
    setError(null);
    try {
      const next = await desktopBridge.request<MonitorSnapshot>(method);
      onUpdate(next);
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Alerts could not be updated.');
    } finally {
      setBusy(false);
    }
  }

  async function clearAlerts() {
    if (!clearConfirm) {
      setClearConfirm(true);
      return;
    }
    await request('monitor.clearAlerts');
    setClearConfirm(false);
  }

  if (!open) return null;

  return (
    <div className="alerts-layer">
      <button type="button" className="alerts-backdrop" aria-label="Close alerts" onClick={onClose} />
      <aside className="alerts-panel" role="dialog" aria-modal="true" aria-labelledby="alerts-title">
        <header className="alerts-header">
          <div>
            <span>NETWORK EVENTS</span>
            <h2 id="alerts-title">Issues & alerts</h2>
            <p>{snapshot ? alertSummary(snapshot) : 'Reading local monitoring history…'}</p>
          </div>
          <button type="button" className="alerts-close" aria-label="Close alerts" onClick={onClose}>
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 7 10 10M17 7 7 17" /></svg>
          </button>
        </header>

        <div className="alerts-actions">
          <button type="button" disabled={busy || !snapshot?.unreadAlertCount} onClick={() => void request('monitor.markAlertsRead')}>Mark all read</button>
          <button type="button" className={clearConfirm ? 'confirm' : ''} disabled={busy || !snapshot?.alerts.length} onClick={() => void clearAlerts()}>{clearConfirm ? 'Confirm clear' : 'Clear history'}</button>
        </div>

        {error && <div className="alerts-error" role="alert">{error}</div>}

        <div className="alerts-list">
          {!snapshot ? (
            <div className="alerts-empty"><strong>Loading alerts</strong><span>Monitoring events stay on this computer.</span></div>
          ) : snapshot.alerts.length === 0 ? (
            <div className="alerts-empty"><strong>No recorded issues</strong><span>Outages, recoveries, network changes, and meaningful degradation will appear here.</span></div>
          ) : snapshot.alerts.map((alert) => (
            <article key={alert.id} className={`alerts-item ${alert.severity} ${alert.isRead ? 'read' : 'unread'}`}>
              <div className="alerts-item-meta">
                <span>{formatTime(alert.timestamp)}</span>
                <span>{labelize(alert.kind)}</span>
                {!alert.isRead && <i>Unread</i>}
              </div>
              <strong>{alert.title}</strong>
              <p>{alert.detail}</p>
            </article>
          ))}
        </div>
      </aside>
    </div>
  );
}

function alertSummary(snapshot: MonitorSnapshot): string {
  if (snapshot.alerts.length === 0) return 'No recorded monitoring events.';
  if (snapshot.unreadAlertCount > 0) return `${snapshot.unreadAlertCount} unread · ${snapshot.alerts.length} recorded`;
  return `${snapshot.alerts.length} recorded · all read`;
}

function formatTime(value: string): string {
  return new Date(value).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function labelize(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/-/g, ' ');
}

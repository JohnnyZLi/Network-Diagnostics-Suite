import { useEffect, useRef, useState } from 'react';
import './settings.css';

export type AppearanceMode = 'system' | 'light' | 'dark';

export function SettingsMenu({
  appearance,
  onAppearanceChange,
  onOpenAdvanced,
  disabled = false,
}: {
  appearance: AppearanceMode;
  onAppearanceChange: (appearance: AppearanceMode) => void;
  onOpenAdvanced?: () => void;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
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

  function openAdvanced() {
    setOpen(false);
    onOpenAdvanced?.();
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
          <path d="M12 8.4a3.6 3.6 0 1 0 0 7.2 3.6 3.6 0 0 0 0-7.2Z" />
          <path d="M19.2 13.2c.05-.4.05-.8 0-1.2l2-1.55-1.9-3.3-2.35.95a8.4 8.4 0 0 0-1.05-.6L15.55 5h-3.8l-.35 2.5c-.37.17-.72.37-1.05.6L8 7.15l-1.9 3.3L8.1 12a8.6 8.6 0 0 0 0 1.2l-2 1.55 1.9 3.3 2.35-.95c.33.23.68.43 1.05.6l.35 2.5h3.8l.35-2.5c.37-.17.72-.37 1.05-.6l2.35.95 1.9-3.3-2-1.55Z" />
        </svg>
      </button>

      {open && (
        <div className="settings-popover" role="dialog" aria-label="Application settings">
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
          {onOpenAdvanced && (
            <button type="button" className="settings-advanced-link" onClick={openAdvanced}>
              <span>
                <strong>Advanced diagnostics</strong>
                <small>Endpoints, interfaces, privacy, and LAN</small>
              </span>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m9 6 6 6-6 6" /></svg>
            </button>
          )}
        </div>
      )}
    </div>
  );
}

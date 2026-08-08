import { useEffect, useRef, useState } from 'react';
import './settings.css';

export type AppearanceMode = 'system' | 'light' | 'dark';

export function SettingsMenu({
  appearance,
  onAppearanceChange,
  disabled = false,
}: {
  appearance: AppearanceMode;
  onAppearanceChange: (appearance: AppearanceMode) => void;
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
          <path d="M12 3.25v2.2M12 18.55v2.2M3.25 12h2.2M18.55 12h2.2M5.81 5.81l1.56 1.56M16.63 16.63l1.56 1.56M18.19 5.81l-1.56 1.56M7.37 16.63l-1.56 1.56" />
          <circle cx="12" cy="12" r="4.15" />
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
        </div>
      )}
    </div>
  );
}

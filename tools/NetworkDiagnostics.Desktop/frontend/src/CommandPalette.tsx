import { useEffect, useMemo, useRef, useState } from 'react';
import './command-palette.css';

export type PaletteCommand = {
  id: string;
  title: string;
  detail: string;
  keywords?: string;
  shortcut?: string;
  enabled?: boolean;
  priority?: number;
  run: () => void;
};

export function CommandPalette({
  open,
  commands,
  onClose,
}: {
  open: boolean;
  commands: PaletteCommand[];
  onClose: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const surfaceRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef(onClose);
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState(0);
  closeRef.current = onClose;

  const filtered = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    const terms = normalized.split(/\s+/).filter(Boolean);
    return commands
      .map((command) => ({ command, score: scoreCommand(command, normalized, terms) }))
      .filter((item) => item.score < Number.MAX_SAFE_INTEGER)
      .sort((a, b) => a.score - b.score || a.command.title.localeCompare(b.command.title))
      .slice(0, 14)
      .map((item) => item.command);
  }, [commands, query]);

  useEffect(() => {
    if (!open) return;
    setQuery('');
    setSelected(0);
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const frame = window.requestAnimationFrame(() => inputRef.current?.focus());

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeRef.current();
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
    setSelected((current) => Math.min(current, Math.max(0, filtered.length - 1)));
  }, [filtered.length]);

  if (!open) return null;

  function invoke(command: PaletteCommand) {
    if (command.enabled === false) return;
    onClose();
    command.run();
  }

  function onInputKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (filtered.length > 0) setSelected((current) => (current + 1) % filtered.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (filtered.length > 0) setSelected((current) => (current - 1 + filtered.length) % filtered.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const command = filtered[selected];
      if (command) invoke(command);
    }
  }

  return (
    <div className="command-palette-layer" role="presentation">
      <button type="button" className="command-palette-backdrop" aria-label="Close command palette" onClick={onClose} tabIndex={-1} />
      <div ref={surfaceRef} className="command-palette-surface" role="dialog" aria-modal="true" aria-label="Command palette">
        <div className="command-palette-search">
          <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="11" cy="11" r="6.5" /><path d="m16 16 4 4" /></svg>
          <input
            ref={inputRef}
            value={query}
            onChange={(event) => { setQuery(event.target.value); setSelected(0); }}
            onKeyDown={onInputKeyDown}
            placeholder="Search commands…"
            aria-label="Search commands"
            autoComplete="off"
            spellCheck={false}
          />
          <kbd>Esc</kbd>
        </div>

        <div className="command-palette-meta">
          <span>{filtered.length === 1 ? '1 command' : `${filtered.length} commands`}</span>
          <span>↑↓ navigate · Enter run</span>
        </div>

        <div className="command-palette-results" role="listbox" aria-label="Available commands">
          {filtered.length === 0 ? (
            <div className="command-palette-empty">
              <strong>No matching command</strong>
              <p>Try a workspace, profile, transfer mode, monitor, or report term.</p>
            </div>
          ) : filtered.map((command, index) => (
            <button
              type="button"
              key={command.id}
              role="option"
              aria-selected={index === selected}
              className={`${index === selected ? 'selected' : ''} ${command.enabled === false ? 'disabled' : ''}`}
              disabled={command.enabled === false}
              onMouseEnter={() => setSelected(index)}
              onClick={() => invoke(command)}
            >
              <span className="command-palette-copy">
                <strong>{command.title}</strong>
                <small>{command.detail}</small>
              </span>
              {command.shortcut && <kbd>{command.shortcut}</kbd>}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

function scoreCommand(command: PaletteCommand, query: string, terms: string[]): number {
  const priority = command.priority ?? 50;
  if (!query) return priority;
  const title = command.title.toLowerCase();
  const detail = command.detail.toLowerCase();
  const keywords = (command.keywords ?? '').toLowerCase();
  const haystack = `${title} ${detail} ${keywords}`;
  if (terms.some((term) => !haystack.includes(term))) return Number.MAX_SAFE_INTEGER;
  if (title.startsWith(query)) return 0;
  if (title.includes(query)) return 10;
  if (keywords.includes(query)) return 20;
  return 30 + priority;
}

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './styles.css';
import './theme.css';
import './workbench-polish.css';
import './shell-status.css';
import './macos-window.css';
import './shell-interactions.css';
import './ux-overhaul.css';
import './ux-workflow.css';
import './ux-macos-wide.css';
import './ux-native-outcomes.css';
import './ux-readability.css';
import './ux-final-layout.css';

if (navigator.platform.toLowerCase().startsWith('mac')) {
  document.documentElement.dataset.platform = 'macos';

  document.addEventListener('dblclick', (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    if (!target.closest('.product-bar')) return;
    if (target.closest('button, a, input, select, textarea, [role="button"], [contenteditable="true"]')) return;

    event.preventDefault();
    const external = window.external as unknown as { sendMessage?: (message: string) => void };
    external?.sendMessage?.('macos.window.toggleZoom');
  });
}

const rootElement = document.getElementById('root')!;
createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

// Transfer topology is a normal run-time choice on the desktop canvas, not hidden
// progressive disclosure. Keep the existing details semantics/tests while presenting
// the control as expanded from the first rendered frame onward.
requestAnimationFrame(() => {
  rootElement.querySelector<HTMLDetailsElement>('.diagnostic-options')?.setAttribute('open', '');
});

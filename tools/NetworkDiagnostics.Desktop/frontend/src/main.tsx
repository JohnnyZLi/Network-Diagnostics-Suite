import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './app.css';
import './interface-picker.css';
import './interface-picker';

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

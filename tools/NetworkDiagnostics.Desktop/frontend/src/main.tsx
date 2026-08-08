import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './styles.css';
import './theme.css';
import './workbench-polish.css';
import './macos-window.css';

if (navigator.platform.toLowerCase().startsWith('mac')) {
  document.documentElement.dataset.platform = 'macos';
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

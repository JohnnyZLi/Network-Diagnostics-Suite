import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

Object.defineProperty(window, 'matchMedia', {
  configurable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  }),
});

Object.defineProperty(navigator, 'clipboard', {
  configurable: true,
  value: { writeText: async () => undefined },
});

HTMLElement.prototype.scrollIntoView = () => undefined;
HTMLElement.prototype.scrollTo = () => undefined;
window.scrollTo = () => undefined;

afterEach(() => {
  cleanup();
  document.body.innerHTML = '';
});

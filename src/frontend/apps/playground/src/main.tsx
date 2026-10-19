import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { createRuntimeConfigLoader } from '@ncode/config';
import { BootstrapGate } from '@ncode/shell';
import { createAppStore } from '@ncode/store';
import { App } from './App.js';

const store = createAppStore();
// The host returns this surface's own coordinates: its seeded public client id and scopes.
const loadRuntimeConfig = createRuntimeConfigLoader({ surface: 'playground' });

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root not found.');
}

createRoot(container).render(
  <StrictMode>
    <Provider store={store}>
      <BootstrapGate load={loadRuntimeConfig}>
        <App />
      </BootstrapGate>
    </Provider>
  </StrictMode>,
);

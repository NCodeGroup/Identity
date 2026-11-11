import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { createRuntimeConfigLoader } from '@ncode/config';
import { BootstrapGate } from '@ncode/shell';
import { createAppStore } from '@ncode/store';
import { App } from './App.js';

const store = createAppStore();
// The dashboard is read-only and performs no interactive login, so the host returns no client id for this surface.
const loadRuntimeConfig = createRuntimeConfigLoader({ surface: 'conformance' });

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

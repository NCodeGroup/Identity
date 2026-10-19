import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { createRuntimeConfigLoader } from '@ncode/config';
import { BootstrapGate } from '@ncode/shell';
import { createAppStore } from '@ncode/store';
import { AuthGate } from './AuthGate.js';
import { App } from './App.js';

const store = createAppStore();
const loadRuntimeConfig = createRuntimeConfigLoader({ surface: 'mgmt' });

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root not found.');
}

createRoot(container).render(
  <StrictMode>
    <Provider store={store}>
      <BootstrapGate load={loadRuntimeConfig}>
        <AuthGate>
          <App />
        </AuthGate>
      </BootstrapGate>
    </Provider>
  </StrictMode>,
);

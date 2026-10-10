import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { BootstrapGate } from './BootstrapGate.js';
import { AuthGate } from './AuthGate.js';
import { App } from './App.js';

const store = createAppStore();

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root not found.');
}

createRoot(container).render(
  <StrictMode>
    <Provider store={store}>
      <BootstrapGate>
        <AuthGate>
          <App />
        </AuthGate>
      </BootstrapGate>
    </Provider>
  </StrictMode>,
);

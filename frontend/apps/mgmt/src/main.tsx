import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import createCache from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { createBffRuntimeConfigLoader } from '@ncode/config';
import { BootstrapGate } from '@ncode/shell';
import { createAppStore } from '@ncode/store';
import { readCspNonce } from '@ncode/ui';
import { SessionGate } from './SessionGate.js';
import { App } from './App.js';

const store = createAppStore();
const loadRuntimeConfig = createBffRuntimeConfigLoader();

// Emotion writes the theme's styles into `<style>` elements, which the page's Content-Security-Policy admits only
// with the host's nonce.
const nonce = readCspNonce(document);
const styleCache = createCache({
  key: 'mgmt',
  container: document.head,
  ...(nonce ? { nonce } : {}),
});

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root not found.');
}

createRoot(container).render(
  <StrictMode>
    <CacheProvider value={styleCache}>
      <Provider store={store}>
        <BootstrapGate load={loadRuntimeConfig}>
          <SessionGate>
            <App />
          </SessionGate>
        </BootstrapGate>
      </Provider>
    </CacheProvider>
  </StrictMode>,
);

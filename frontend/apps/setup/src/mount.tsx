import { StrictMode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import createCache from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { AppThemeProvider, readCspNonce } from '@ncode/ui';

function App() {
  return <h1>NCode.Identity setup</h1>;
}

/**
 * Mounts the setup wizard over the page the host rendered, as soon as the bundle's module runs.
 */
export function mountSetup(container: HTMLElement, doc: Document = document): Root {
  // Emotion writes the theme's styles into `<style>` elements, which the page's Content-Security-Policy admits only
  // with the host's nonce.
  const nonce = readCspNonce(doc);
  const styleCache = createCache({
    key: 'setup',
    container: doc.head,
    ...(nonce ? { nonce } : {}),
  });

  // A root's first render is not time-sliced, so it commits in the task after this one and `flushSync` is not needed.
  const root = createRoot(container);
  root.render(
    <StrictMode>
      <CacheProvider value={styleCache}>
        <AppThemeProvider>
          <App />
        </AppThemeProvider>
      </CacheProvider>
    </StrictMode>,
  );
  return root;
}

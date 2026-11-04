import { StrictMode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import createCache from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { AppThemeProvider } from '@ncode/ui';
import { App } from './App.js';
import { readBranding } from './branding.js';
import { readCspNonce } from './cspNonce.js';
import { LocaleProvider } from './i18n/LocaleContext.js';
import { readPageLanguage } from './pageLanguage.js';
import { readServerPage } from './serverPage.js';

/**
 * Mounts the surface over the page the host rendered, as soon as the bundle's module runs. Nothing is fetched first:
 * the page already holds the state to carry, the tenant's branding and the language the host selected.
 */
export function mountSurface(container: HTMLElement, doc: Document = document): Root {
  // Read what the end-user typed or the browser autofilled before React replaces the page (ADR-1253).
  const serverPage = readServerPage(container, doc.activeElement);
  const branding = readBranding(doc);
  // The host selected the page's language from the request's ui_locales and the tenant's settings (ADR-1402).
  const pageLanguage = readPageLanguage(doc);
  // Emotion writes the theme's styles into `<style>` elements, which the page's Content-Security-Policy admits only
  // with the host's nonce (ADR-1256).
  const nonce = readCspNonce(doc);
  const styleCache = createCache({ key: 'auth', container: doc.head, ...(nonce ? { nonce } : {}) });

  // A root's first render is not time-sliced, so it commits in the task after this one and `flushSync` is not needed.
  const root = createRoot(container);
  root.render(
    <StrictMode>
      <CacheProvider value={styleCache}>
        <LocaleProvider pageLanguage={pageLanguage}>
          <AppThemeProvider {...(branding ? { branding } : {})}>
            <App serverPage={serverPage} {...(branding ? { branding } : {})} />
          </AppThemeProvider>
        </LocaleProvider>
      </CacheProvider>
    </StrictMode>,
  );
  return root;
}

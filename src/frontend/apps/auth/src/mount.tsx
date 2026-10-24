import { StrictMode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { AppThemeProvider } from '@ncode/ui';
import { App } from './App.js';
import { readBranding } from './branding.js';
import { readServerPage } from './serverPage.js';

/**
 * Mounts the surface over the page the host rendered, as soon as the bundle's module runs. Nothing is fetched first:
 * the page already holds the state to carry and the tenant's branding.
 */
export function mountSurface(container: HTMLElement, doc: Document = document): Root {
  // Read what the end-user typed or the browser autofilled before React replaces the page (ADR-0253).
  const serverPage = readServerPage(container, doc.activeElement);
  const branding = readBranding(doc);

  // A root's first render is not time-sliced, so it commits in the task after this one and `flushSync` is not needed.
  const root = createRoot(container);
  root.render(
    <StrictMode>
      <AppThemeProvider {...(branding ? { branding } : {})}>
        <App serverPage={serverPage} {...(branding ? { branding } : {})} />
      </AppThemeProvider>
    </StrictMode>,
  );
  return root;
}

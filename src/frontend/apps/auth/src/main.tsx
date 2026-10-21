import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AppThemeProvider } from '@ncode/ui';
import { App } from './App.js';
import { loadBranding } from './branding.js';

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root not found.');
}

// The server-rendered page stays in place while the tenant's branding loads, so the surface first draws already themed.
void loadBranding().then((branding) => {
  createRoot(container).render(
    <StrictMode>
      <AppThemeProvider {...(branding ? { branding } : {})}>
        <App {...(branding ? { branding } : {})} />
      </AppThemeProvider>
    </StrictMode>,
  );
});

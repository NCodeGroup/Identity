import { render, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import { createAppStore, runtimeConfigLoaded, type AppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { App } from './App.js';
// The dashboard is a lazily loaded chunk. Loading its module first lets the lazy component resolve from the module
// cache, so the test waits for the dashboard's render rather than a cold transform of its chunk.
import './containers/ConformanceDashboard.js';
import { loadDiscovery } from './discovery.js';

// The dashboard loads discovery on mount; keep the shell test off the network.
vi.mock(import('./discovery.js'), async (importOriginal) => ({
  ...(await importOriginal()),
  loadDiscovery: vi.fn(() => new Promise<never>(() => {})),
}));

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

afterEach(() => {
  vi.clearAllMocks();
});

function renderApp(store: AppStore) {
  return render(
    <Provider store={store}>
      <AppThemeProvider>
        <App />
      </AppThemeProvider>
    </Provider>,
  );
}

describe('Conformance App', () => {
  it('renders the dashboard header once runtime config is loaded', async () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded(runtime));

    const { getByRole, getByText, findByTestId } = renderApp(store);

    expect(getByRole('heading', { name: 'NCode.Identity · Conformance' })).toBeInTheDocument();
    expect(getByText('https://op.test')).toBeInTheDocument();
    // The dashboard is a lazily loaded chunk; once loaded, it audits the discovered issuer. The audit starts in an
    // effect that can run after the loading state is first found, so wait for the call rather than asserting it once.
    expect(await findByTestId('data-loading')).toBeInTheDocument();
    await waitFor(() => {
      expect(vi.mocked(loadDiscovery)).toHaveBeenCalledWith(
        'https://op.test',
        expect.any(AbortSignal),
      );
    });
  });

  it('brands the header with the discovered display name', () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded({ ...runtime, branding: { displayName: 'Acme ID' } }));

    const { getByRole } = renderApp(store);

    expect(getByRole('heading', { name: 'Acme ID · Conformance' })).toBeInTheDocument();
  });

  it('renders nothing until runtime config is loaded', () => {
    const { queryByRole, queryByTestId } = renderApp(createAppStore());

    expect(queryByRole('heading')).not.toBeInTheDocument();
    expect(queryByTestId('data-loading')).not.toBeInTheDocument();
    expect(vi.mocked(loadDiscovery)).not.toHaveBeenCalled();
  });
});

import { render } from '@testing-library/react';
import { Provider } from 'react-redux';
import { describe, expect, it } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import { createAppStore, runtimeConfigLoaded, type AppStore } from '@ncode/store';
import type { BffRuntimeConfig, OpenIdRuntimeConfig } from '@ncode/config';
import { App } from './App.js';
// The explorer is a lazily loaded chunk. Loading its module first lets the lazy component resolve from the module
// cache, so the test waits for the explorer's render rather than a cold transform of its chunk.
import './containers/FlowExplorer.js';

const runtime: OpenIdRuntimeConfig = {
  kind: 'openid',
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  clientId: 'playground-spa',
  scopes: ['openid'],
  features: {},
};

const bffRuntime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: '/management',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

function renderApp(store: AppStore) {
  return render(
    <Provider store={store}>
      <AppThemeProvider>
        <App />
      </AppThemeProvider>
    </Provider>,
  );
}

describe('Playground App', () => {
  it('renders the explorer once runtime config is loaded', async () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded(runtime));

    const { getByRole, getByTestId, getByText, findByTestId } = renderApp(store);

    expect(getByRole('heading', { name: 'NCode.Identity · Playground' })).toBeInTheDocument();
    expect(getByText('https://op.test')).toBeInTheDocument();
    // The explorer is a lazily loaded chunk.
    expect(await findByTestId('begin')).toBeInTheDocument();
    expect(getByTestId('client-id')).toHaveValue('playground-spa');
  });

  it('brands the header with the discovered display name', () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded({ ...runtime, branding: { displayName: 'Acme ID' } }));

    const { getByRole } = renderApp(store);

    expect(getByRole('heading', { name: 'Acme ID · Playground' })).toBeInTheDocument();
  });

  it('renders nothing until runtime config is loaded', () => {
    const { queryByRole, queryByTestId } = renderApp(createAppStore());

    expect(queryByRole('heading')).not.toBeInTheDocument();
    expect(queryByTestId('begin')).not.toBeInTheDocument();
  });

  it('renders nothing for a BFF runtime config', () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded(bffRuntime));

    const { queryByRole, queryByTestId } = renderApp(store);

    expect(queryByRole('heading')).not.toBeInTheDocument();
    expect(queryByTestId('begin')).not.toBeInTheDocument();
  });
});

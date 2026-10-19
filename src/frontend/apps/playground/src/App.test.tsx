import { render } from '@testing-library/react';
import { Provider } from 'react-redux';
import { describe, expect, it } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import { createAppStore, runtimeConfigLoaded, type AppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { App } from './App.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  clientId: 'playground-spa',
  scopes: ['openid'],
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
  it('renders the explorer once runtime config is loaded', () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded(runtime));

    const { getByRole, getByTestId, getByText } = renderApp(store);

    expect(getByRole('heading', { name: 'NCode.Identity · Playground' })).toBeInTheDocument();
    expect(getByText('https://op.test')).toBeInTheDocument();
    expect(getByTestId('begin')).toBeInTheDocument();
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
});

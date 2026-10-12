import { render } from '@testing-library/react';
import { Provider } from 'react-redux';
import { describe, expect, it } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import { createAppStore, runtimeConfigLoaded } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { App } from './App.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

describe('Conformance App', () => {
  it('renders the dashboard header once runtime config is loaded', () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded(runtime));

    const { getByRole } = render(
      <Provider store={store}>
        <AppThemeProvider>
          <App />
        </AppThemeProvider>
      </Provider>,
    );

    expect(getByRole('heading', { name: /Conformance/i })).toBeInTheDocument();
  });
});

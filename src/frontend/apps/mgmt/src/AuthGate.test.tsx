import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { AuthGate } from './AuthGate.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid', 'read:clients'],
  features: {},
};

function renderGate(preloadedRuntime: RuntimeConfig, accessToken: string | null = null) {
  const store = createAppStore({
    config: { runtime: preloadedRuntime },
    auth: { accessToken, subject: null, expiresAt: null, refreshToken: null },
  });
  return render(
    <Provider store={store}>
      <AuthGate>
        <div data-testid="protected">protected</div>
      </AuthGate>
    </Provider>,
  );
}

describe('AuthGate', () => {
  it('runs open (renders children) when no client id is configured', () => {
    renderGate(runtime);
    expect(screen.getByTestId('protected')).toBeInTheDocument();
  });

  it('renders children when already authenticated', () => {
    renderGate({ ...runtime, clientId: 'mgmt-spa' }, 'existing-token');
    expect(screen.getByTestId('protected')).toBeInTheDocument();
  });

  it('shows the sign-in screen when a client id is configured and there is no token', () => {
    renderGate({ ...runtime, clientId: 'mgmt-spa' });
    expect(screen.getByTestId('signin-button')).toBeInTheDocument();
    expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
  });
});

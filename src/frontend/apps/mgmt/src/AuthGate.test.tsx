import { describe, expect, it, afterEach, vi } from 'vitest';
import { StrictMode } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
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
    auth: { accessToken, subject: null, expiresAt: null, refreshToken: null, idToken: null },
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

  describe('authorization callback completion', () => {
    // The storage key @ncode/auth persists the in-flight PKCE transaction under; mirrored here to
    // seed a completed begin-authorization without reaching into the package internals.
    const TRANSACTION_KEY = 'ncode.auth.tx';
    const originalUrl = window.location.href;

    function base64Url(json: unknown): string {
      return btoa(JSON.stringify(json)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function jwt(claims: Record<string, unknown>): string {
      return `${base64Url({ alg: 'none' })}.${base64Url(claims)}.sig`;
    }

    afterEach(() => {
      vi.unstubAllGlobals();
      window.sessionStorage.clear();
      window.history.replaceState({}, '', originalUrl);
    });

    // Regression: React StrictMode double-invokes effects in development. The callback consumes a
    // single-use code and the one-shot PKCE transaction, so a cancellation-flag guard let the second
    // invocation re-enter completion after the first consumed the transaction, dead-ending on
    // "No in-flight authorization transaction was found." A run-once ref must make it idempotent.
    it('completes the callback exactly once under StrictMode double-invocation', async () => {
      const state = 'state-xyz';
      window.history.replaceState({}, '', `/callback?code=auth-code&state=${state}`);
      window.sessionStorage.setItem(
        TRANSACTION_KEY,
        JSON.stringify({
          state,
          codeVerifier: 'verifier',
          redirectUri: 'http://localhost/callback',
        }),
      );

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          access_token: jwt({ sub: 'user-123' }),
          token_type: 'Bearer',
          expires_in: 3600,
        }),
        text: async () => '',
      });
      vi.stubGlobal('fetch', fetchMock);

      const store = createAppStore({
        config: { runtime: { ...runtime, clientId: 'mgmt-spa' } },
        auth: {
          accessToken: null,
          subject: null,
          expiresAt: null,
          refreshToken: null,
          idToken: null,
        },
      });
      render(
        <StrictMode>
          <Provider store={store}>
            <AuthGate>
              <div data-testid="protected">protected</div>
            </AuthGate>
          </Provider>
        </StrictMode>,
      );

      await waitFor(() => expect(screen.getByTestId('protected')).toBeInTheDocument());
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(screen.queryByText(/No in-flight authorization transaction/i)).not.toBeInTheDocument();
      expect(store.getState().auth.accessToken).toBe(jwt({ sub: 'user-123' }));
    });
  });
});

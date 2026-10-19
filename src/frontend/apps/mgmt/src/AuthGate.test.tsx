import { describe, expect, it, afterEach, vi } from 'vitest';
import { StrictMode } from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore, type AuthState } from '@ncode/store';
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

const withClient: RuntimeConfig = { ...runtime, clientId: 'mgmt-spa' };

const signedOutAuth: AuthState = {
  accessToken: null,
  subject: null,
  expiresAt: null,
  refreshToken: null,
  idToken: null,
};

// The storage key @ncode/auth persists the in-flight PKCE transaction under; mirrored here to seed a
// completed begin-authorization without reaching into the package internals.
const TRANSACTION_KEY = 'ncode.auth.tx';

function base64Url(json: unknown): string {
  return btoa(JSON.stringify(json)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function jwt(claims: Record<string, unknown>): string {
  return `${base64Url({ alg: 'none' })}.${base64Url(claims)}.sig`;
}

// An access token is opaque to the client (OAuth 2.0; RFC 9068 §1): these are deliberately not JWTs,
// so any attempt by the gate to parse one would throw "Malformed JWT" and fail the flow under test.
const OPAQUE_ACCESS_TOKEN = 'opaque-access-token';
const RENEWED_ACCESS_TOKEN = 'opaque-renewed-access-token';

function tokenResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function renderGate(preloadedRuntime: RuntimeConfig, auth: Partial<AuthState> = {}) {
  const store = createAppStore({
    config: { runtime: preloadedRuntime },
    auth: { ...signedOutAuth, ...auth },
  });
  render(
    <Provider store={store}>
      <AuthGate>
        <div data-testid="protected">protected</div>
      </AuthGate>
    </Provider>,
  );
  return store;
}

describe('AuthGate', () => {
  it('runs open (renders children) when no client id is configured', () => {
    renderGate(runtime);
    expect(screen.getByTestId('protected')).toBeInTheDocument();
  });

  it('runs open (renders children) before any runtime config is discovered', () => {
    render(
      <Provider store={createAppStore({ auth: signedOutAuth })}>
        <AuthGate>
          <div data-testid="protected">protected</div>
        </AuthGate>
      </Provider>,
    );
    expect(screen.getByTestId('protected')).toBeInTheDocument();
    expect(screen.queryByTestId('signin-button')).not.toBeInTheDocument();
  });

  it('renders children when already authenticated', () => {
    renderGate(withClient, { accessToken: 'existing-token' });
    expect(screen.getByTestId('protected')).toBeInTheDocument();
  });

  it('shows the sign-in screen when a client id is configured and there is no token', () => {
    renderGate(withClient);
    expect(screen.getByTestId('signin-button')).toBeInTheDocument();
    expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
  });

  it('names the product on the sign-in screen from the discovered branding', () => {
    renderGate({ ...withClient, branding: { displayName: 'Acme Console' } });
    expect(screen.getByRole('heading', { name: 'Acme Console' })).toBeInTheDocument();
  });

  describe('starting sign-in', () => {
    const originalLocation = window.location;

    afterEach(() => {
      vi.restoreAllMocks();
      Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
      window.sessionStorage.clear();
    });

    function stubLocation() {
      const assign = vi.fn();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: { origin: 'https://ui.test', href: 'https://ui.test/', search: '', assign },
      });
      return assign;
    }

    it('redirects to the authorization endpoint with a PKCE S256 request', async () => {
      const assign = stubLocation();
      renderGate(withClient);

      fireEvent.click(screen.getByTestId('signin-button'));

      await waitFor(() => expect(assign).toHaveBeenCalledTimes(1));
      const target = new URL(assign.mock.calls[0]![0] as string);
      expect(target.origin + target.pathname).toBe('https://op.test/authorize');
      expect(target.searchParams.get('response_type')).toBe('code');
      expect(target.searchParams.get('client_id')).toBe('mgmt-spa');
      expect(target.searchParams.get('redirect_uri')).toBe('https://ui.test/callback');
      expect(target.searchParams.get('scope')).toBe('openid read:clients');
      expect(target.searchParams.get('code_challenge_method')).toBe('S256');
      expect(target.searchParams.get('code_challenge')).toBeTruthy();
      const transaction = JSON.parse(window.sessionStorage.getItem(TRANSACTION_KEY) ?? '{}') as {
        state?: string;
      };
      expect(target.searchParams.get('state')).toBe(transaction.state);
      expect(screen.getByTestId('signin-button')).toBeDisabled();
    });

    it('surfaces a failure to start sign-in and re-enables the button', async () => {
      const assign = stubLocation();
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('Session storage is full.');
      });
      renderGate(withClient);

      fireEvent.click(screen.getByTestId('signin-button'));

      expect(await screen.findByTestId('signin-error')).toHaveTextContent(
        'Session storage is full.',
      );
      expect(screen.getByTestId('signin-button')).toBeEnabled();
      expect(assign).not.toHaveBeenCalled();
    });

    it('falls back to a generic message when sign-in fails with a non-Error value', async () => {
      stubLocation();
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw 'quota';
      });
      renderGate(withClient);

      fireEvent.click(screen.getByTestId('signin-button'));

      expect(await screen.findByTestId('signin-error')).toHaveTextContent(
        'Unable to start sign-in.',
      );
    });
  });

  describe('authorization callback completion', () => {
    const originalUrl = window.location.href;
    const state = 'state-xyz';

    function seedCallback() {
      window.history.replaceState({}, '', `/callback?code=auth-code&state=${state}`);
      window.sessionStorage.setItem(
        TRANSACTION_KEY,
        JSON.stringify({
          state,
          codeVerifier: 'verifier',
          redirectUri: 'http://localhost/callback',
        }),
      );
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
      seedCallback();

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          access_token: OPAQUE_ACCESS_TOKEN,
          token_type: 'Bearer',
          expires_in: 3600,
          id_token: jwt({ sub: 'user-123' }),
        }),
        text: async () => '',
      });
      vi.stubGlobal('fetch', fetchMock);

      const store = createAppStore({
        config: { runtime: withClient },
        auth: signedOutAuth,
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
      expect(store.getState().auth.accessToken).toBe(OPAQUE_ACCESS_TOKEN);
      expect(store.getState().auth.subject).toBe('user-123');
      expect(store.getState().auth.expiresAt).toBeGreaterThan(Date.now());
    });

    it('signs in with the ID token subject and keeps the opaque access, refresh, and ID tokens', async () => {
      seedCallback();
      const idToken = jwt({ sub: 'id-subject' });
      const fetchMock = vi.fn(async () =>
        tokenResponse({
          access_token: OPAQUE_ACCESS_TOKEN,
          token_type: 'Bearer',
          refresh_token: 'rt-1',
          id_token: idToken,
        }),
      );
      vi.stubGlobal('fetch', fetchMock);

      const store = renderGate(withClient);

      expect(await screen.findByTestId('protected')).toBeInTheDocument();
      expect(store.getState().auth).toMatchObject({
        accessToken: OPAQUE_ACCESS_TOKEN,
        subject: 'id-subject',
        refreshToken: 'rt-1',
        idToken,
        expiresAt: null,
      });
      // The single-use code is dropped from the address bar once it has been exchanged.
      expect(window.location.pathname).toBe('/');
      expect(window.location.search).toBe('');
    });

    it('records an unknown subject without reading the access token when no ID token is issued', async () => {
      seedCallback();
      vi.stubGlobal(
        'fetch',
        vi.fn(async () =>
          tokenResponse({ access_token: OPAQUE_ACCESS_TOKEN, token_type: 'Bearer' }),
        ),
      );

      const store = renderGate(withClient);

      expect(await screen.findByTestId('protected')).toBeInTheDocument();
      expect(screen.queryByTestId('signin-error')).not.toBeInTheDocument();
      expect(store.getState().auth).toMatchObject({
        accessToken: OPAQUE_ACCESS_TOKEN,
        subject: 'unknown',
        idToken: null,
      });
    });

    it('records an unknown subject when the ID token carries none', async () => {
      seedCallback();
      vi.stubGlobal(
        'fetch',
        vi.fn(async () =>
          tokenResponse({
            access_token: OPAQUE_ACCESS_TOKEN,
            token_type: 'Bearer',
            id_token: jwt({}),
          }),
        ),
      );

      const store = renderGate(withClient);

      expect(await screen.findByTestId('protected')).toBeInTheDocument();
      expect(store.getState().auth.subject).toBe('unknown');
    });

    it('surfaces an authorization error returned on the callback', async () => {
      window.history.replaceState(
        {},
        '',
        '/callback?error=access_denied&error_description=The+user+denied+access.',
      );
      const fetchMock = vi.fn();
      vi.stubGlobal('fetch', fetchMock);

      renderGate(withClient);
      // The callback is being processed, so the sign-in action starts out busy.
      expect(screen.getByTestId('signin-button')).toBeDisabled();

      expect(await screen.findByTestId('signin-error')).toHaveTextContent(
        'The user denied access.',
      );
      expect(screen.getByTestId('signin-button')).toBeEnabled();
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('falls back to a generic message when the token exchange fails with a non-Error value', async () => {
      seedCallback();
      vi.stubGlobal('fetch', vi.fn().mockRejectedValue('network down'));

      const store = renderGate(withClient);

      expect(await screen.findByTestId('signin-error')).toHaveTextContent('Sign-in failed.');
      expect(store.getState().auth.accessToken).toBeNull();
    });

    it('does not redeem the callback again when a session already exists', () => {
      seedCallback();
      const fetchMock = vi.fn();
      vi.stubGlobal('fetch', fetchMock);

      renderGate(withClient, { accessToken: 'existing-token' });

      expect(screen.getByTestId('protected')).toBeInTheDocument();
      expect(fetchMock).not.toHaveBeenCalled();
      expect(window.sessionStorage.getItem(TRANSACTION_KEY)).not.toBeNull();
    });
  });

  describe('silent renewal', () => {
    afterEach(() => {
      vi.unstubAllGlobals();
    });

    type TokenCall = { readonly url: string; readonly body: URLSearchParams };

    function installTokenEndpoint(response: () => Response) {
      const calls: TokenCall[] = [];
      vi.stubGlobal(
        'fetch',
        vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
          calls.push({ url: String(input), body: new URLSearchParams(String(init?.body)) });
          return response();
        }),
      );
      return calls;
    }

    // An access token that is already inside the renewal skew, so the renewer fires immediately.
    const dueSession: Partial<AuthState> = {
      accessToken: OPAQUE_ACCESS_TOKEN,
      subject: 'user-1',
      refreshToken: 'rt-1',
      idToken: 'id-old',
    };

    it('stays signed in with the prior subject and ID token when renewal returns an opaque access token and no ID token', async () => {
      const calls = installTokenEndpoint(() =>
        tokenResponse({
          access_token: RENEWED_ACCESS_TOKEN,
          token_type: 'Bearer',
          expires_in: 3600,
          refresh_token: 'rt-2',
        }),
      );

      const store = renderGate(withClient, { ...dueSession, expiresAt: Date.now() });

      await waitFor(() => expect(store.getState().auth.accessToken).toBe(RENEWED_ACCESS_TOKEN));
      expect(store.getState().auth).toMatchObject({
        subject: 'user-1',
        refreshToken: 'rt-2',
        idToken: 'id-old',
      });
      expect(store.getState().auth.expiresAt).toBeGreaterThan(Date.now());
      expect(calls).toHaveLength(1);
      expect(calls[0]!.url).toBe('https://op.test/token');
      expect(calls[0]!.body.get('grant_type')).toBe('refresh_token');
      expect(calls[0]!.body.get('refresh_token')).toBe('rt-1');
      expect(calls[0]!.body.get('client_id')).toBe('mgmt-spa');
      expect(screen.getByTestId('protected')).toBeInTheDocument();
      expect(screen.queryByTestId('signin-button')).not.toBeInTheDocument();
    });

    it('adopts a re-issued ID token and its subject', async () => {
      const idToken = jwt({ sub: 'id-subject' });
      installTokenEndpoint(() =>
        tokenResponse({
          access_token: RENEWED_ACCESS_TOKEN,
          token_type: 'Bearer',
          id_token: idToken,
        }),
      );

      const store = renderGate(withClient, { ...dueSession, expiresAt: Date.now() });

      await waitFor(() => expect(store.getState().auth.idToken).toBe(idToken));
      expect(store.getState().auth).toMatchObject({
        accessToken: RENEWED_ACCESS_TOKEN,
        subject: 'id-subject',
        refreshToken: null,
        expiresAt: null,
      });
    });

    it('keeps the prior subject when a re-issued ID token carries none', async () => {
      const idToken = jwt({});
      installTokenEndpoint(() =>
        tokenResponse({
          access_token: RENEWED_ACCESS_TOKEN,
          token_type: 'Bearer',
          id_token: idToken,
        }),
      );

      const store = renderGate(withClient, { ...dueSession, expiresAt: Date.now() });

      await waitFor(() => expect(store.getState().auth.accessToken).toBe(RENEWED_ACCESS_TOKEN));
      expect(store.getState().auth.subject).toBe('user-1');
      expect(store.getState().auth.idToken).toBe(idToken);
    });

    it('records an unknown subject when no subject or ID token is known at all', async () => {
      installTokenEndpoint(() =>
        tokenResponse({ access_token: RENEWED_ACCESS_TOKEN, token_type: 'Bearer' }),
      );

      const store = renderGate(withClient, {
        ...dueSession,
        subject: null,
        idToken: null,
        expiresAt: Date.now(),
      });

      await waitFor(() => expect(store.getState().auth.accessToken).toBe(RENEWED_ACCESS_TOKEN));
      expect(store.getState().auth.subject).toBe('unknown');
      expect(store.getState().auth.idToken).toBeNull();
    });

    it('signs out to the sign-in screen when the refresh is rejected', async () => {
      installTokenEndpoint(() =>
        tokenResponse(
          { error: 'invalid_grant', error_description: 'The refresh token has expired.' },
          400,
        ),
      );

      const store = renderGate(withClient, { ...dueSession, expiresAt: Date.now() });

      expect(await screen.findByTestId('signin-button')).toBeInTheDocument();
      expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
      expect(store.getState().auth.accessToken).toBeNull();
    });

    it('signs out when no refresh token is available to renew with', async () => {
      const calls = installTokenEndpoint(() => tokenResponse({}));

      const store = renderGate(withClient, {
        ...dueSession,
        refreshToken: null,
        expiresAt: Date.now(),
      });

      expect(await screen.findByTestId('signin-button')).toBeInTheDocument();
      expect(store.getState().auth.accessToken).toBeNull();
      expect(calls).toHaveLength(0);
    });
  });
});

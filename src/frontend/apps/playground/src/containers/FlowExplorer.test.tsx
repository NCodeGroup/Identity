import { webcrypto } from 'node:crypto';
import { render, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import type { FetchLike } from '@ncode/auth';
import type { RuntimeConfig } from '@ncode/config';
import { FlowExplorer } from './FlowExplorer.js';
import { TRANSACTION_KEY, type FlowDeps } from '../flow.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  endSessionEndpoint: 'https://op.test/end-session',
  userInfoEndpoint: 'https://op.test/userinfo',
  clientId: 'playground-spa',
  scopes: ['openid', 'profile'],
  features: {},
};

function createStorage() {
  const map = new Map<string, string>();
  return {
    getItem: (key: string) => map.get(key) ?? null,
    setItem: (key: string, value: string) => void map.set(key, value),
    removeItem: (key: string) => void map.delete(key),
  };
}

function createDeps(fetchFn: FetchLike): FlowDeps {
  return {
    fetchFn,
    storage: createStorage(),
    randomBytes: (length: number) => new Uint8Array(length).fill(7),
    subtle: webcrypto.subtle as unknown as SubtleCrypto,
  };
}

function renderExplorer(props: Partial<Parameters<typeof FlowExplorer>[0]> = {}) {
  const deps = props.deps ?? createDeps(vi.fn<FetchLike>());
  return render(
    <AppThemeProvider>
      <FlowExplorer runtime={runtime} deps={deps} origin="https://ui.test" {...props} />
    </AppThemeProvider>,
  );
}

describe('FlowExplorer', () => {
  it('prefills the client id and scopes from the discovered config', () => {
    const { getByTestId } = renderExplorer();
    expect((getByTestId('client-id') as HTMLInputElement).value).toBe('playground-spa');
    expect((getByTestId('scopes') as HTMLInputElement).value).toBe('openid profile');
  });

  it('navigates to the authorize url when the flow begins', async () => {
    const navigate = vi.fn();
    const { getByTestId } = renderExplorer({ navigate });

    getByTestId('begin').click();

    await waitFor(() => expect(navigate).toHaveBeenCalledTimes(1));
    const url = new URL(navigate.mock.calls[0]![0] as string);
    expect(url.origin + url.pathname).toBe('https://op.test/authorize');
    expect(url.searchParams.get('client_id')).toBe('playground-spa');
    expect(url.searchParams.get('code_challenge_method')).toBe('S256');
    expect(url.searchParams.get('redirect_uri')).toBe('https://ui.test/callback');
  });

  it('completes a callback by exchanging the code for tokens', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({
        ok: true,
        status: 200,
        json: () =>
          Promise.resolve({ access_token: 'opaque-at', token_type: 'Bearer', scope: 'openid' }),
        text: () => Promise.resolve(''),
      }),
    );
    const deps = createDeps(fetchFn);
    deps.storage.setItem(
      TRANSACTION_KEY,
      JSON.stringify({
        clientId: 'playground-spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 'state-7',
        nonce: 'nonce-7',
        codeVerifier: 'verifier-7',
        codeChallenge: 'challenge-7',
      }),
    );
    const onCleanUrl = vi.fn();

    const { findByText } = renderExplorer({
      deps,
      initialSearch: '?code=the-code&state=state-7',
      onCleanUrl,
    });

    expect(await findByText(/token_type/)).toBeInTheDocument();
    expect(fetchFn).toHaveBeenCalledTimes(1);
    expect(onCleanUrl).toHaveBeenCalled();
  });

  it('shows the callback error on a state mismatch', async () => {
    const deps = createDeps(vi.fn<FetchLike>());
    deps.storage.setItem(
      TRANSACTION_KEY,
      JSON.stringify({
        clientId: 'playground-spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 'state-7',
        nonce: 'nonce-7',
        codeVerifier: 'verifier-7',
        codeChallenge: 'challenge-7',
      }),
    );

    const { findByText } = renderExplorer({
      deps,
      initialSearch: '?code=the-code&state=wrong',
      onCleanUrl: () => {},
    });

    expect(await findByText(/possible CSRF/)).toBeInTheDocument();
  });
});

import { webcrypto } from 'node:crypto';
import { describe, expect, it, vi } from 'vitest';
import type { FetchLike } from '@ncode/auth';
import type { RuntimeConfig } from '@ncode/config';
import {
  AuthError,
  beginFlow,
  clearTransaction,
  endSessionUrl,
  exchangeCode,
  fetchUserInfo,
  inspectJwt,
  isCallback,
  loadTransaction,
  playgroundRedirectUri,
  readCallback,
  refreshTokens,
  TRANSACTION_KEY,
  type FlowDeps,
  type FlowTransaction,
} from './flow.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  endSessionEndpoint: 'https://op.test/end-session',
  userInfoEndpoint: 'https://op.test/userinfo',
  scopes: ['openid'],
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

// Deterministic byte source so state/nonce/verifier are stable under test.
const fixedBytes = (length: number) => new Uint8Array(length).fill(7);

function createDeps(fetchFn: FetchLike): FlowDeps {
  return {
    fetchFn,
    storage: createStorage(),
    randomBytes: fixedBytes,
    subtle: webcrypto.subtle as unknown as SubtleCrypto,
  };
}

const okJson = (body: unknown) =>
  Promise.resolve({
    ok: true,
    status: 200,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  });

describe('playgroundRedirectUri', () => {
  it('appends the callback path to the origin', () => {
    expect(playgroundRedirectUri('https://ui.test')).toBe('https://ui.test/callback');
  });
});

describe('beginFlow', () => {
  it('persists the transaction and builds a PKCE + nonce authorize url', async () => {
    const deps = createDeps(vi.fn<FetchLike>());

    const { authorizeUrl, transaction } = await beginFlow(
      runtime,
      { clientId: 'playground-spa', scopes: ['openid', 'profile'] },
      'https://ui.test',
      deps,
    );

    const url = new URL(authorizeUrl);
    expect(url.origin + url.pathname).toBe('https://op.test/authorize');
    expect(url.searchParams.get('response_type')).toBe('code');
    expect(url.searchParams.get('client_id')).toBe('playground-spa');
    expect(url.searchParams.get('redirect_uri')).toBe('https://ui.test/callback');
    expect(url.searchParams.get('scope')).toBe('openid profile');
    expect(url.searchParams.get('code_challenge_method')).toBe('S256');
    expect(url.searchParams.get('code_challenge')).toBe(transaction.codeChallenge);
    expect(url.searchParams.get('state')).toBe(transaction.state);
    expect(url.searchParams.get('nonce')).toBe(transaction.nonce);

    expect(loadTransaction(deps)).toEqual(transaction);
  });
});

describe('isCallback', () => {
  it('detects code and error responses but not an empty query', () => {
    expect(isCallback('?code=abc&state=xyz')).toBe(true);
    expect(isCallback('?error=access_denied')).toBe(true);
    expect(isCallback('')).toBe(false);
  });
});

describe('readCallback', () => {
  const transaction: FlowTransaction = {
    clientId: 'playground-spa',
    redirectUri: 'https://ui.test/callback',
    scopes: ['openid'],
    state: 'state-7',
    nonce: 'nonce-7',
    codeVerifier: 'verifier-7',
    codeChallenge: 'challenge-7',
  };

  it('returns the code and transaction on a matching state', () => {
    const deps = createDeps(vi.fn<FetchLike>());
    deps.storage.setItem(TRANSACTION_KEY, JSON.stringify(transaction));

    const result = readCallback('?code=the-code&state=state-7', deps);

    expect(result.code).toBe('the-code');
    expect(result.transaction.codeVerifier).toBe('verifier-7');
  });

  it('throws on a server error response', () => {
    const deps = createDeps(vi.fn<FetchLike>());
    expect(() => readCallback('?error=access_denied', deps)).toThrow(AuthError);
  });

  it('throws when no transaction is in flight', () => {
    const deps = createDeps(vi.fn<FetchLike>());
    expect(() => readCallback('?code=c&state=state-7', deps)).toThrow(/in-flight/);
  });

  it('throws on a state mismatch', () => {
    const deps = createDeps(vi.fn<FetchLike>());
    deps.storage.setItem(TRANSACTION_KEY, JSON.stringify(transaction));
    expect(() => readCallback('?code=c&state=wrong', deps)).toThrow(/CSRF/);
  });
});

describe('exchangeCode', () => {
  it('posts the code and verifier then clears the transaction', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      okJson({ access_token: 'at', token_type: 'Bearer', refresh_token: 'rt' }),
    );
    const deps = createDeps(fetchFn);
    deps.storage.setItem(
      TRANSACTION_KEY,
      JSON.stringify({
        clientId: 'playground-spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 's',
        nonce: 'n',
        codeVerifier: 'verifier-7',
        codeChallenge: 'challenge-7',
      }),
    );

    const callback = readCallback('?code=the-code&state=s', deps);
    const tokens = await exchangeCode(runtime, callback, deps);

    expect(tokens.accessToken).toBe('at');
    const [, init] = fetchFn.mock.calls[0]!;
    const body = new URLSearchParams(init.body);
    expect(body.get('grant_type')).toBe('authorization_code');
    expect(body.get('code')).toBe('the-code');
    expect(body.get('code_verifier')).toBe('verifier-7');
    expect(loadTransaction(deps)).toBeNull();
  });
});

describe('refreshTokens', () => {
  it('posts the refresh_token grant', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({ access_token: 'at2', token_type: 'Bearer' }));
    const deps = createDeps(fetchFn);

    const tokens = await refreshTokens(runtime, 'playground-spa', 'rt', deps);

    expect(tokens.accessToken).toBe('at2');
    const [, init] = fetchFn.mock.calls[0]!;
    const body = new URLSearchParams(init.body);
    expect(body.get('grant_type')).toBe('refresh_token');
    expect(body.get('refresh_token')).toBe('rt');
  });
});

describe('fetchUserInfo', () => {
  it('calls the userinfo endpoint with a bearer token', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({ sub: 'user-1' }));
    const deps = createDeps(fetchFn);

    const claims = await fetchUserInfo(runtime, 'at', deps);

    expect(claims.sub).toBe('user-1');
    const [url, init] = fetchFn.mock.calls[0]!;
    expect(url).toBe('https://op.test/userinfo');
    expect(init.headers.authorization).toBe('Bearer at');
  });

  it('throws when the provider advertises no userinfo endpoint', async () => {
    const withoutUserInfo: RuntimeConfig = {
      apiBaseUrl: 'https://op.test',
      issuer: 'https://op.test',
      authorizationEndpoint: 'https://op.test/authorize',
      tokenEndpoint: 'https://op.test/token',
      scopes: ['openid'],
      features: {},
    };
    const deps = createDeps(vi.fn<FetchLike>());
    await expect(fetchUserInfo(withoutUserInfo, 'at', deps)).rejects.toThrow(/userinfo/);
  });
});

describe('inspectJwt', () => {
  it('decodes a JWT header and payload', () => {
    const header = Buffer.from(JSON.stringify({ alg: 'PS256', typ: 'JWT' })).toString('base64url');
    const payload = Buffer.from(JSON.stringify({ sub: 'user-1' })).toString('base64url');
    const decoded = inspectJwt(`${header}.${payload}.sig`);
    expect(decoded?.header.alg).toBe('PS256');
    expect(decoded?.payload.sub).toBe('user-1');
  });

  it('returns null for an opaque (non-JWT) token', () => {
    expect(inspectJwt('opaque-access-token')).toBeNull();
  });
});

describe('endSessionUrl', () => {
  it('builds an RP-initiated logout url with the id token hint', () => {
    const url = new URL(endSessionUrl(runtime, 'id-token', 'playground-spa', 'https://ui.test')!);
    expect(url.origin + url.pathname).toBe('https://op.test/end-session');
    expect(url.searchParams.get('id_token_hint')).toBe('id-token');
    expect(url.searchParams.get('client_id')).toBe('playground-spa');
    expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://ui.test/');
  });

  it('returns null when no end_session_endpoint is advertised', () => {
    const withoutLogout: RuntimeConfig = {
      apiBaseUrl: 'https://op.test',
      issuer: 'https://op.test',
      authorizationEndpoint: 'https://op.test/authorize',
      tokenEndpoint: 'https://op.test/token',
      scopes: ['openid'],
      features: {},
    };
    expect(
      endSessionUrl(withoutLogout, 'id-token', 'playground-spa', 'https://ui.test'),
    ).toBeNull();
  });
});

describe('clearTransaction', () => {
  it('removes a persisted transaction', () => {
    const deps = createDeps(vi.fn<FetchLike>());
    deps.storage.setItem(TRANSACTION_KEY, '{}');
    clearTransaction(deps);
    expect(deps.storage.getItem(TRANSACTION_KEY)).toBeNull();
  });
});

import { describe, expect, it, vi } from 'vitest';
import {
  AuthError,
  buildAuthorizeUrl,
  buildEndSessionUrl,
  exchangeCodeForToken,
  parseCallbackParams,
  refreshAccessToken,
  type FetchLike,
} from './flow.js';

describe('buildAuthorizeUrl', () => {
  it('builds an authorization-code + PKCE (S256) request', () => {
    const url = new URL(
      buildAuthorizeUrl('https://op.test/authorize', {
        clientId: 'mgmt-spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid', 'read:clients'],
        state: 'xyz',
        codeChallenge: 'chal',
      }),
    );
    expect(url.origin + url.pathname).toBe('https://op.test/authorize');
    expect(url.searchParams.get('response_type')).toBe('code');
    expect(url.searchParams.get('client_id')).toBe('mgmt-spa');
    expect(url.searchParams.get('redirect_uri')).toBe('https://ui.test/callback');
    expect(url.searchParams.get('scope')).toBe('openid read:clients');
    expect(url.searchParams.get('state')).toBe('xyz');
    expect(url.searchParams.get('code_challenge')).toBe('chal');
    expect(url.searchParams.get('code_challenge_method')).toBe('S256');
  });

  it('omits the nonce when absent and emits it when present', () => {
    const without = new URL(
      buildAuthorizeUrl('https://op.test/authorize', {
        clientId: 'spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 'xyz',
        codeChallenge: 'chal',
      }),
    );
    expect(without.searchParams.has('nonce')).toBe(false);

    const withNonce = new URL(
      buildAuthorizeUrl('https://op.test/authorize', {
        clientId: 'spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 'xyz',
        codeChallenge: 'chal',
        nonce: 'n-123',
      }),
    );
    expect(withNonce.searchParams.get('nonce')).toBe('n-123');
  });

  it('emits the prompt only when present', () => {
    const without = new URL(
      buildAuthorizeUrl('https://op.test/authorize', {
        clientId: 'spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 'xyz',
        codeChallenge: 'chal',
      }),
    );
    expect(without.searchParams.has('prompt')).toBe(false);

    const withPrompt = new URL(
      buildAuthorizeUrl('https://op.test/authorize', {
        clientId: 'spa',
        redirectUri: 'https://ui.test/callback',
        scopes: ['openid'],
        state: 'xyz',
        codeChallenge: 'chal',
        prompt: 'consent',
      }),
    );
    expect(withPrompt.searchParams.get('prompt')).toBe('consent');
  });
});

describe('buildEndSessionUrl', () => {
  it('emits every supplied RP-Initiated Logout parameter', () => {
    const url = new URL(
      buildEndSessionUrl('https://op.test/end-session', {
        idTokenHint: 'id-token',
        clientId: 'mgmt-spa',
        postLogoutRedirectUri: 'https://ui.test/',
        state: 'xyz',
      }),
    );
    expect(url.origin + url.pathname).toBe('https://op.test/end-session');
    expect(url.searchParams.get('id_token_hint')).toBe('id-token');
    expect(url.searchParams.get('client_id')).toBe('mgmt-spa');
    expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://ui.test/');
    expect(url.searchParams.get('state')).toBe('xyz');
  });

  it('omits parameters that are not supplied so the OP cookie can identify the session', () => {
    const url = new URL(buildEndSessionUrl('https://op.test/end-session', {}));
    expect(url.search).toBe('');
  });
});

describe('parseCallbackParams', () => {
  it('returns code and state on success', () => {
    expect(parseCallbackParams('?code=abc&state=xyz')).toEqual({ code: 'abc', state: 'xyz' });
  });

  it('returns the error object when the server reports an error', () => {
    expect(parseCallbackParams('error=access_denied&error_description=nope&state=xyz')).toEqual({
      error: 'access_denied',
      errorDescription: 'nope',
      state: 'xyz',
    });
  });

  it('omits the description and state when the error response carries neither', () => {
    expect(parseCallbackParams('?error=server_error')).toEqual({ error: 'server_error' });
  });

  it('returns null when the code arrives without a state', () => {
    expect(parseCallbackParams('?code=abc')).toBeNull();
  });

  it('returns null when the query is not a callback', () => {
    expect(parseCallbackParams('?foo=bar')).toBeNull();
  });
});

function okJson(body: unknown): ReturnType<FetchLike> {
  return Promise.resolve({
    ok: true,
    status: 200,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  });
}

describe('exchangeCodeForToken', () => {
  it('posts the authorization_code grant and normalizes the token set', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      okJson({
        access_token: 'at',
        token_type: 'Bearer',
        expires_in: 3600,
        refresh_token: 'rt',
        id_token: 'it',
        scope: 'openid read:clients',
      }),
    );

    const tokens = await exchangeCodeForToken(
      'https://op.test/token',
      {
        clientId: 'mgmt-spa',
        code: 'abc',
        redirectUri: 'https://ui.test/callback',
        codeVerifier: 'verifier',
      },
      fetchFn,
    );

    expect(tokens).toEqual({
      accessToken: 'at',
      tokenType: 'Bearer',
      expiresInSeconds: 3600,
      refreshToken: 'rt',
      idToken: 'it',
      scope: 'openid read:clients',
    });

    const [tokenUrl, init] = fetchFn.mock.calls[0]!;
    expect(tokenUrl).toBe('https://op.test/token');
    expect(init.method).toBe('POST');
    expect(init.headers).toEqual({
      'content-type': 'application/x-www-form-urlencoded',
      accept: 'application/json',
    });
    const body = new URLSearchParams(init.body);
    expect(body.get('grant_type')).toBe('authorization_code');
    expect(body.get('code_verifier')).toBe('verifier');
    expect(body.get('client_id')).toBe('mgmt-spa');
  });

  it('defaults the token type to Bearer and omits absent optional fields', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({ access_token: 'at' }));

    const tokens = await exchangeCodeForToken(
      'https://op.test/token',
      { clientId: 'c', code: 'x', redirectUri: 'r', codeVerifier: 'v' },
      fetchFn,
    );

    expect(tokens).toEqual({ accessToken: 'at', tokenType: 'Bearer' });
  });

  it('throws an AuthError carrying the OAuth error code on failure', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ error: 'invalid_grant', error_description: 'bad code' }),
        text: () => Promise.resolve('{}'),
      }),
    );

    await expect(
      exchangeCodeForToken(
        'https://op.test/token',
        { clientId: 'c', code: 'x', redirectUri: 'r', codeVerifier: 'v' },
        fetchFn,
      ),
    ).rejects.toMatchObject({ name: 'AuthError', code: 'invalid_grant', message: 'bad code' });
  });

  it('falls back to the HTTP status when the error body is not JSON', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({
        ok: false,
        status: 502,
        json: () => Promise.reject(new SyntaxError('Unexpected token <')),
        text: () => Promise.resolve('<html>Bad Gateway</html>'),
      }),
    );

    const error = await exchangeCodeForToken(
      'https://op.test/token',
      { clientId: 'c', code: 'x', redirectUri: 'r', codeVerifier: 'v' },
      fetchFn,
    ).catch((cause: unknown) => cause);

    expect(error).toBeInstanceOf(AuthError);
    expect(error).toMatchObject({ message: 'Token request failed (HTTP 502).', code: undefined });
  });

  it('ignores non-string error fields in a JSON error body', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({
        ok: false,
        status: 401,
        json: () => Promise.resolve({ error: 42, error_description: { text: 'nope' } }),
        text: () => Promise.resolve('{}'),
      }),
    );

    await expect(
      refreshAccessToken('https://op.test/token', { clientId: 'c', refreshToken: 'rt' }, fetchFn),
    ).rejects.toMatchObject({
      name: 'AuthError',
      message: 'Token request failed (HTTP 401).',
      code: undefined,
    });
  });

  it('throws when the response lacks an access_token', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({ token_type: 'Bearer' }));
    await expect(
      exchangeCodeForToken(
        'https://op.test/token',
        { clientId: 'c', code: 'x', redirectUri: 'r', codeVerifier: 'v' },
        fetchFn,
      ),
    ).rejects.toBeInstanceOf(AuthError);
  });
});

describe('refreshAccessToken', () => {
  it('posts the refresh_token grant', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({ access_token: 'at2', token_type: 'Bearer' }));
    const tokens = await refreshAccessToken(
      'https://op.test/token',
      { clientId: 'mgmt-spa', refreshToken: 'rt' },
      fetchFn,
    );
    expect(tokens.accessToken).toBe('at2');
    const [, init] = fetchFn.mock.calls[0]!;
    const body = new URLSearchParams(init.body);
    expect(body.get('grant_type')).toBe('refresh_token');
    expect(body.get('refresh_token')).toBe('rt');
  });
});

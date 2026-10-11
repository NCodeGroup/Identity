import { describe, expect, it, vi } from 'vitest';
import {
  beginAuthorization,
  completeAuthorization,
  isAuthorizationCallback,
  type AuthClientConfig,
  type AuthDeps,
  type KeyValueStorage,
} from './client.js';
import type { FetchLike, RandomBytes } from './index.js';

function memoryStorage(): KeyValueStorage {
  const map = new Map<string, string>();
  return {
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => void map.set(k, v),
    removeItem: (k) => void map.delete(k),
  };
}

const config: AuthClientConfig = {
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  clientId: 'mgmt-spa',
  redirectUri: 'https://ui.test/callback',
  scopes: ['openid', 'read:clients'],
};

const fixedRandom: RandomBytes = (n) => new Uint8Array(n).fill(0);

describe('isAuthorizationCallback', () => {
  it('detects a code+state response', () => {
    expect(isAuthorizationCallback('?code=a&state=b')).toBe(true);
  });
  it('is false for an unrelated query', () => {
    expect(isAuthorizationCallback('?foo=bar')).toBe(false);
  });
});

describe('beginAuthorization', () => {
  it('persists a transaction and returns the authorize URL', async () => {
    const storage = memoryStorage();
    const deps: AuthDeps = { fetchFn: vi.fn(), storage, randomBytes: fixedRandom };

    const url = new URL(await beginAuthorization(config, deps));

    expect(url.searchParams.get('client_id')).toBe('mgmt-spa');
    expect(url.searchParams.get('code_challenge_method')).toBe('S256');
    const tx = JSON.parse(storage.getItem('ncode.auth.tx')!);
    expect(tx.state).toBe(url.searchParams.get('state'));
    expect(tx.redirectUri).toBe('https://ui.test/callback');
    expect(typeof tx.codeVerifier).toBe('string');
  });
});

describe('completeAuthorization', () => {
  function okJson(body: unknown): ReturnType<FetchLike> {
    return Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve(body),
      text: () => Promise.resolve(JSON.stringify(body)),
    });
  }

  it('validates state and exchanges the code for tokens', async () => {
    const storage = memoryStorage();
    const fetchFn = vi.fn<FetchLike>(() => okJson({ access_token: 'at', token_type: 'Bearer' }));
    const deps: AuthDeps = { fetchFn, storage, randomBytes: fixedRandom };

    const url = new URL(await beginAuthorization(config, deps));
    const state = url.searchParams.get('state')!;

    const tokens = await completeAuthorization(config, `?code=abc&state=${state}`, deps);

    expect(tokens.accessToken).toBe('at');
    expect(storage.getItem('ncode.auth.tx')).toBeNull(); // consumed
  });

  it('rejects a state mismatch (CSRF guard)', async () => {
    const storage = memoryStorage();
    const deps: AuthDeps = { fetchFn: vi.fn(), storage, randomBytes: fixedRandom };
    await beginAuthorization(config, deps);

    await expect(
      completeAuthorization(config, '?code=abc&state=tampered', deps),
    ).rejects.toMatchObject({ name: 'AuthError' });
  });

  it('throws when the server returned an error', async () => {
    const storage = memoryStorage();
    const deps: AuthDeps = { fetchFn: vi.fn(), storage, randomBytes: fixedRandom };
    await expect(
      completeAuthorization(config, '?error=access_denied&state=x', deps),
    ).rejects.toMatchObject({ name: 'AuthError', code: 'access_denied' });
  });

  it('throws when no transaction is in flight', async () => {
    const storage = memoryStorage();
    const deps: AuthDeps = { fetchFn: vi.fn(), storage, randomBytes: fixedRandom };
    await expect(completeAuthorization(config, '?code=abc&state=x', deps)).rejects.toMatchObject({
      name: 'AuthError',
    });
  });
});

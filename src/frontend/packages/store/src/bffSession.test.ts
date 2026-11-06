import { describe, expect, it, vi } from 'vitest';
import type { BffRuntimeConfig } from '@ncode/config';
import { fetchBffUser, parseBffUser } from './bffSession.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: '/management',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const rawUser = {
  sub: 'admin',
  name: 'Ada',
  claims: { email: 'ada@example.test' },
  session: { expiresAt: 2_000, idleExpiresAt: 1_500 },
  logoutUrl: '/bff/logout',
  logoutSid: 'sid-1',
};

const malformedMessage = 'The session probe answered with a malformed user.';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('parseBffUser', () => {
  it('reads a complete user', () => {
    expect(parseBffUser(rawUser)).toEqual({
      sub: 'admin',
      name: 'Ada',
      claims: { email: 'ada@example.test' },
      session: { expiresAt: 2_000, idleExpiresAt: 1_500 },
      logoutUrl: '/bff/logout',
      logoutSid: 'sid-1',
    });
  });

  it('keeps no name when it is absent', () => {
    const raw: Record<string, unknown> = { ...rawUser };
    delete raw.name;

    expect(parseBffUser(raw)).not.toHaveProperty('name');
  });

  it.each([['' as unknown], [42 as unknown], [null as unknown]])(
    'keeps no name when it is %j',
    (name) => {
      expect(parseBffUser({ ...rawUser, name })).not.toHaveProperty('name');
    },
  );

  it.each([
    ['null', null],
    ['a string', 'user'],
    ['an array', []],
  ])('rejects %s', (_label, raw) => {
    expect(() => parseBffUser(raw)).toThrow(malformedMessage);
  });

  it.each(['sub', 'logoutUrl', 'logoutSid'])('rejects a missing %s', (field) => {
    const raw: Record<string, unknown> = { ...rawUser };
    delete raw[field];

    expect(() => parseBffUser(raw)).toThrow(malformedMessage);
  });

  it.each(['sub', 'logoutUrl', 'logoutSid'])('rejects an empty %s', (field) => {
    expect(() => parseBffUser({ ...rawUser, [field]: '' })).toThrow(malformedMessage);
  });

  it.each(['sub', 'logoutUrl', 'logoutSid'])('rejects a non-string %s', (field) => {
    expect(() => parseBffUser({ ...rawUser, [field]: 7 })).toThrow(malformedMessage);
  });

  it.each([
    ['missing', undefined],
    ['null', null],
    ['a string', 'session'],
    ['an array', []],
  ])('rejects a session that is %s', (_label, session) => {
    expect(() => parseBffUser({ ...rawUser, session })).toThrow(malformedMessage);
  });

  it.each(['expiresAt', 'idleExpiresAt'])('rejects a session without a numeric %s', (field) => {
    expect(() =>
      parseBffUser({ ...rawUser, session: { ...rawUser.session, [field]: '1' } }),
    ).toThrow(malformedMessage);
    expect(() =>
      parseBffUser({ ...rawUser, session: { ...rawUser.session, [field]: undefined } }),
    ).toThrow(malformedMessage);
  });

  it.each([
    ['missing', undefined],
    ['null', null],
    ['a string', 'claims'],
    ['an array', []],
  ])('rejects claims that are %s', (_label, claims) => {
    expect(() => parseBffUser({ ...rawUser, claims })).toThrow(malformedMessage);
  });
});

describe('fetchBffUser', () => {
  it('probes the user endpoint with the session cookie and the anti-forgery header', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => jsonResponse(rawUser));

    await fetchBffUser(runtime, fetchFn);

    expect(fetchFn).toHaveBeenCalledTimes(1);
    expect(fetchFn).toHaveBeenCalledWith('/bff/user', {
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { accept: 'application/json', 'X-CSRF': '1' },
    });
  });

  it('answers the signed-in user on 200', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => jsonResponse(rawUser));

    await expect(fetchBffUser(runtime, fetchFn)).resolves.toEqual(parseBffUser(rawUser));
  });

  it('rejects a malformed user on 200', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => jsonResponse({ sub: 'admin' }));

    await expect(fetchBffUser(runtime, fetchFn)).rejects.toThrow(malformedMessage);
  });

  it('answers null on 401', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => new Response(null, { status: 401 }));

    await expect(fetchBffUser(runtime, fetchFn)).resolves.toBeNull();
  });

  it('throws on any other failure', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => new Response(null, { status: 500 }));

    await expect(fetchBffUser(runtime, fetchFn)).rejects.toThrow(
      'The session probe failed (HTTP 500).',
    );
  });

  it('probes through the global fetch by default', async () => {
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 401 }));
    vi.stubGlobal('fetch', fetchMock);
    try {
      await expect(fetchBffUser(runtime)).resolves.toBeNull();
      expect(fetchMock).toHaveBeenCalledTimes(1);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

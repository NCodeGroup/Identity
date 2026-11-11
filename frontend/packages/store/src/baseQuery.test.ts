import { afterEach, describe, expect, it, vi } from 'vitest';
import type { BffRuntimeConfig, OpenIdRuntimeConfig } from '@ncode/config';
import { runtimeConfigLoaded } from './configSlice.js';
import { managementApi } from './managementApi.js';
import { sessionStarted, type BffUser } from './sessionSlice.js';
import { createAppStore } from './store.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const openIdRuntime: OpenIdRuntimeConfig = {
  kind: 'openid',
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const user: BffUser = {
  sub: 'admin',
  claims: {},
  session: { expiresAt: 2_000_000_000, idleExpiresAt: 1_900_000_000 },
  logoutUrl: '/bff/logout',
  logoutSid: 'sid-1',
};

type CapturedRequest = {
  readonly url: string;
  readonly antiforgery: string | null;
  readonly authorization: string | null;
  readonly credentials: RequestCredentials;
};

function mockFetch(status = 200) {
  const captured: CapturedRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    captured.push({
      url: request.url,
      antiforgery: request.headers.get('x-csrf'),
      authorization: request.headers.get('authorization'),
      credentials: request.credentials,
    });
    return new Response(JSON.stringify({ ok: true }), {
      status,
      headers: { 'content-type': 'application/json' },
    });
  });
  vi.stubGlobal('fetch', fetchMock);
  return { captured, fetchMock };
}

describe('dynamicBaseQuery', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('fails without fetching while the runtime configuration has not been loaded', async () => {
    const { fetchMock } = mockFetch();
    const store = createAppStore();

    const result = await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(result.error).toEqual({
      status: 'CUSTOM_ERROR',
      error: 'Runtime configuration has not been loaded.',
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('fails without fetching for an OpenID runtime configuration', async () => {
    const { fetchMock } = mockFetch();
    const store = createAppStore({ config: { runtime: openIdRuntime } });

    const result = await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(result.error).toEqual({
      status: 'CUSTOM_ERROR',
      error: 'The management API is called only from a surface its BFF serves.',
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('resolves the base URL from the runtime configuration at request time', async () => {
    const { captured } = mockFetch();
    const store = createAppStore();

    store.dispatch(runtimeConfigLoaded({ ...runtime, apiBaseUrl: 'https://tier-a.test' }));
    await store.dispatch(managementApi.endpoints.getCapabilities.initiate());
    store.dispatch(runtimeConfigLoaded({ ...runtime, apiBaseUrl: 'https://tier-b.test/mgmt' }));
    await store.dispatch(managementApi.endpoints.listTeams.initiate());

    expect(captured.map((r) => r.url)).toEqual([
      'https://tier-a.test/api/capabilities',
      'https://tier-b.test/mgmt/api/teams',
    ]);
  });

  it('sends the anti-forgery header with same-origin credentials and no authorization header', async () => {
    const { captured } = mockFetch();
    const store = createAppStore({ config: { runtime } });

    const result = await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(result.data).toEqual({ ok: true });
    expect(captured).toEqual([
      {
        url: 'https://api.test/api/capabilities',
        antiforgery: '1',
        authorization: null,
        credentials: 'same-origin',
      },
    ]);
  });

  it('ends the session when the API answers 401', async () => {
    mockFetch(401);
    const store = createAppStore({ config: { runtime } });
    store.dispatch(sessionStarted(user));

    await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(store.getState().session).toEqual({ status: 'signed-out', user: null });
  });

  it('keeps the session when the API answers 500', async () => {
    mockFetch(500);
    const store = createAppStore({ config: { runtime } });
    store.dispatch(sessionStarted(user));

    await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(store.getState().session).toEqual({ status: 'signed-in', user });
  });
});

import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { signedOut } from './authSlice.js';
import { runtimeConfigLoaded } from './configSlice.js';
import { managementApi } from './managementApi.js';
import { createAppStore } from './store.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const signedInAuth = {
  accessToken: 'token-123',
  subject: 'admin',
  expiresAt: null,
  refreshToken: null,
  idToken: null,
};

type CapturedRequest = { readonly url: string; readonly authorization: string | null };

function mockFetch() {
  const captured: CapturedRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    captured.push({ url: request.url, authorization: request.headers.get('authorization') });
    return new Response(JSON.stringify({ ok: true }), {
      status: 200,
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
    const store = createAppStore({ auth: signedInAuth });

    const result = await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(result.error).toEqual({
      status: 'CUSTOM_ERROR',
      error: 'Runtime configuration has not been loaded.',
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('resolves the base URL from the runtime configuration at request time', async () => {
    const { captured } = mockFetch();
    const store = createAppStore({ auth: signedInAuth });

    store.dispatch(runtimeConfigLoaded({ ...runtime, apiBaseUrl: 'https://tier-a.test' }));
    await store.dispatch(managementApi.endpoints.getCapabilities.initiate());
    store.dispatch(runtimeConfigLoaded({ ...runtime, apiBaseUrl: 'https://tier-b.test/mgmt' }));
    await store.dispatch(managementApi.endpoints.listTenants.initiate());

    expect(captured.map((r) => r.url)).toEqual([
      'https://tier-a.test/api/capabilities',
      'https://tier-b.test/mgmt/api/tenants',
    ]);
  });

  it('attaches the bearer token from the auth slice', async () => {
    const { captured } = mockFetch();
    const store = createAppStore({ config: { runtime }, auth: signedInAuth });

    const result = await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(result.data).toEqual({ ok: true });
    expect(captured).toEqual([
      { url: 'https://api.test/api/capabilities', authorization: 'Bearer token-123' },
    ]);
  });

  it('sends no authorization header when signed out', async () => {
    const { captured } = mockFetch();
    const store = createAppStore({ config: { runtime }, auth: signedInAuth });

    store.dispatch(signedOut());
    await store.dispatch(managementApi.endpoints.getCapabilities.initiate());

    expect(captured).toEqual([{ url: 'https://api.test/api/capabilities', authorization: null }]);
  });
});

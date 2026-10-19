import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CreateSecretRequest } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { createAppStore, type AppStore } from './store.js';
import { managementApi } from './managementApi.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function createStore() {
  return createAppStore({
    config: { runtime },
    auth: {
      accessToken: 'token-123',
      subject: 'admin',
      expiresAt: null,
      refreshToken: null,
      idToken: null,
    },
  });
}

type CapturedRequest = {
  readonly url: string;
  readonly method: string;
  readonly authorization: string | null;
  readonly ifMatch: string | null;
  readonly body: string;
};

function mockFetch(response: Response) {
  const captured: CapturedRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    captured.push({
      url: request.url,
      method: request.method,
      authorization: request.headers.get('authorization'),
      ifMatch: request.headers.get('if-match'),
      body: await request.clone().text(),
    });
    return response.clone();
  });
  vi.stubGlobal('fetch', fetchMock);
  return captured;
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

/** Stubs `fetch` with a per-request responder and records every call as `METHOD /path?query`. */
function routeFetch(respond: (method: string, path: string) => Response) {
  const calls: string[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    const { pathname, search } = new URL(request.url);
    const path = `${pathname}${search}`;
    calls.push(`${request.method} ${path}`);
    return respond(request.method, path);
  });
  vi.stubGlobal('fetch', fetchMock);
  return {
    calls,
    count: (method: string, path: string) =>
      calls.filter((call) => call === `${method} ${path}`).length,
  };
}

/** Waits for every in-flight query, including refetches triggered by tag invalidation. */
async function settle(store: AppStore): Promise<void> {
  await Promise.all(store.dispatch(managementApi.util.getRunningQueriesThunk()));
}

type ManagementTag = {
  readonly type:
    'Tenant' | 'Server' | 'Client' | 'ResourceServer' | 'Grant' | 'Secret' | 'Settings';
  readonly id: string;
};

/** The endpoint names of the cached queries that the given tags would invalidate. */
function invalidatedBy(store: AppStore, tags: readonly ManagementTag[]): string[] {
  return managementApi.util
    .selectInvalidatedBy(store.getState(), tags)
    .map((entry) => entry.endpointName);
}

const acme = { kind: 'tenant', tenantId: 'acme' } as const;
const globalScope = { kind: 'global' } as const;

const secretRequest: CreateSecretRequest = {
  secretType: 'symmetric',
  keySizeBits: 256,
  use: null,
  algorithm: null,
  expiresWhen: '2027-01-01T00:00:00Z',
};

describe('managementApi tenant mutations', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createTenant POSTs the request body with the bearer token', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          tenantId: 'new',
          concurrencyToken: 'c1',
          domainName: null,
          isDisabled: false,
          displayName: 'New',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createTenant.initiate({
        domainName: null,
        displayName: 'New',
        isDisabled: false,
        settings: {},
      }),
    );

    expect('data' in result && result.data?.tenantId).toBe('new');
    expect(captured).toHaveLength(1);
    expect(captured[0]?.url).toBe('https://api.test/api/tenants');
    expect(captured[0]?.method).toBe('POST');
    expect(captured[0]?.authorization).toBe('Bearer token-123');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({
      domainName: null,
      displayName: 'New',
      isDisabled: false,
      settings: {},
    });
  });

  it('updateTenant PATCHes a replace JSON Patch with the If-Match concurrency token', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateTenant.initiate({
        tenantId: 'acme/1',
        concurrencyToken: 'etag-7',
        fields: { domainName: 'acme.example', displayName: 'Acme', isDisabled: true },
      }),
    );

    expect(captured).toHaveLength(1);
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme%2F1');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-7');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/domainName', value: 'acme.example' },
      { op: 'replace', path: '/displayName', value: 'Acme' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('deleteTenant DELETEs the tenant by id', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(managementApi.endpoints.deleteTenant.initiate({ tenantId: 'acme' }));

    expect(captured).toHaveLength(1);
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme');
    expect(captured[0]?.method).toBe('DELETE');
    expect(captured[0]?.authorization).toBe('Bearer token-123');
  });
});

describe('managementApi client mutations (tenant scope)', () => {
  const scope = { kind: 'tenant', tenantId: 'acme' } as const;

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createClient POSTs under the tenant path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          clientId: 'new',
          tenantId: 'acme',
          concurrencyToken: 'c1',
          isDisabled: false,
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.createClient.initiate({
        scope,
        body: { isDisabled: false, settings: {} },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients');
    expect(captured[0]?.method).toBe('POST');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({ isDisabled: false, settings: {} });
  });

  it('updateClient PATCHes a replace patch with If-Match under the tenant path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateClient.initiate({
        scope,
        clientId: 'web',
        concurrencyToken: 'etag-2',
        fields: { isDisabled: true },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients/web');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-2');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('deleteClient DELETEs under the tenant path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(managementApi.endpoints.deleteClient.initiate({ scope, clientId: 'web' }));

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients/web');
    expect(captured[0]?.method).toBe('DELETE');
  });
});

describe('managementApi resource-server + scope mutations (tenant scope)', () => {
  const scope = { kind: 'tenant', tenantId: 'acme' } as const;

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createResourceServer POSTs the request body under the tenant path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          tenantId: 'acme',
          resourceServerId: 'rs-new',
          identifier: 'urn:api',
          concurrencyToken: 'c1',
          name: 'API',
          isSystem: false,
          isDisabled: false,
          scopes: [],
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createResourceServer.initiate({
        scope,
        body: { identifier: 'urn:api', name: 'API', isDisabled: false, settings: {} },
      }),
    );

    expect('data' in result && result.data?.resourceServerId).toBe('rs-new');
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/resource-servers');
    expect(captured[0]?.method).toBe('POST');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({
      identifier: 'urn:api',
      name: 'API',
      isDisabled: false,
      settings: {},
    });
  });

  it('updateResourceServer PATCHes a replace patch with If-Match under the tenant path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateResourceServer.initiate({
        scope,
        resourceServerId: 'rs-1',
        concurrencyToken: 'etag-3',
        fields: { name: 'Renamed', isDisabled: true },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/resource-servers/rs-1');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-3');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/name', value: 'Renamed' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('deleteResourceServer DELETEs under the tenant path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.deleteResourceServer.initiate({ scope, resourceServerId: 'rs-1' }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/resource-servers/rs-1');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('createScope POSTs the scope body under the resource server', async () => {
    const captured = mockFetch(
      new Response(JSON.stringify({ value: 'read:x', description: null, isSystem: false }), {
        status: 201,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.createScope.initiate({
        scope,
        resourceServerId: 'rs-1',
        body: { value: 'read:x', description: 'Read X' },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/resource-servers/rs-1/scopes');
    expect(captured[0]?.method).toBe('POST');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({
      value: 'read:x',
      description: 'Read X',
    });
  });

  it('updateScope PUTs the description for the (immutable) scope value', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateScope.initiate({
        scope,
        resourceServerId: 'rs-1',
        scopeValue: 'read:x',
        body: { description: 'Read all X' },
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/resource-servers/rs-1/scopes/read%3Ax',
    );
    expect(captured[0]?.method).toBe('PUT');
    expect(captured[0]?.ifMatch).toBeNull();
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({ description: 'Read all X' });
  });

  it('deleteScope DELETEs the scope by value', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.deleteScope.initiate({
        scope,
        resourceServerId: 'rs-1',
        scopeValue: 'read:x',
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/resource-servers/rs-1/scopes/read%3Ax',
    );
    expect(captured[0]?.method).toBe('DELETE');
  });
});

describe('managementApi grant queries + mutations (tenant scope)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('listGrants GETs the tenant grants with subject/client query params', async () => {
    const captured = mockFetch(
      new Response(JSON.stringify({ items: [] }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.listGrants.initiate({
        tenantId: 'acme',
        subjectId: 'user-1',
        clientId: 'web',
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/grants?subjectId=user-1&clientId=web',
    );
    expect(captured[0]?.method).toBe('GET');
  });

  it('revokeGrant DELETEs a single grant by id', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.revokeGrant.initiate({ tenantId: 'acme', grantId: 'g-1' }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/grants/g-1');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('revokeGrants DELETEs with the filter query params for a bulk revoke', async () => {
    const captured = mockFetch(
      new Response(JSON.stringify({ revoked: 3 }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.revokeGrants.initiate({ tenantId: 'acme', clientId: 'web' }),
    );

    expect('data' in result && result.data?.revoked).toBe(3);
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/grants?clientId=web');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('revokeGrants sends only the subject filter when no client is given', async () => {
    const captured = mockFetch(jsonResponse({ revoked: 1 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.revokeGrants.initiate({ tenantId: 'acme', subjectId: 'user-1' }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/grants?subjectId=user-1');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('listGrants sends no filter parameters when none are given', async () => {
    const captured = mockFetch(jsonResponse({ items: [] }));
    const store = createStore();

    await store.dispatch(managementApi.endpoints.listGrants.initiate({ tenantId: 'acme' }));

    const url = new URL(captured[0]?.url ?? '');
    expect(url.origin + url.pathname).toBe('https://api.test/api/tenants/acme/grants');
    expect([...url.searchParams.keys()]).toEqual([]);
    expect(captured[0]?.method).toBe('GET');
  });
});

describe('managementApi client secret + settings endpoints (tenant scope)', () => {
  const scope = { kind: 'tenant', tenantId: 'acme' } as const;

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createClientSecret POSTs the request body under the client path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          secretId: 's-1',
          concurrencyToken: 'c1',
          use: null,
          algorithm: null,
          createdWhen: '2026-01-01T00:00:00Z',
          expiresWhen: '2027-01-01T00:00:00Z',
          secretType: 'symmetric',
          keySizeBits: 256,
          secretMaterial: 'abc',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createClientSecret.initiate({
        scope,
        clientId: 'web',
        body: {
          secretType: 'symmetric',
          keySizeBits: 256,
          use: null,
          algorithm: null,
          expiresWhen: '2027-01-01T00:00:00Z',
        },
      }),
    );

    expect('data' in result && result.data?.secretMaterial).toBe('abc');
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients/web/secrets');
    expect(captured[0]?.method).toBe('POST');
  });

  it('deleteClientSecret DELETEs the secret by id under the client path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.deleteClientSecret.initiate({
        scope,
        clientId: 'web',
        secretId: 's-1',
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients/web/secrets/s-1');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('updateClientSettings PATCHes a JSON Patch diff with If-Match', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateClientSettings.initiate({
        scope,
        clientId: 'web',
        concurrencyToken: 'etag-9',
        original: { keep: 1, drop: 2 },
        desired: { keep: 1, added: 3 },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients/web/settings');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-9');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'remove', path: '/drop' },
      { op: 'add', path: '/added', value: 3 },
    ]);
  });
});

describe('managementApi tenant secret + settings endpoints', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createTenantSecret POSTs the request body under the tenant path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          secretId: 's-1',
          concurrencyToken: 'c1',
          use: null,
          algorithm: null,
          createdWhen: '2026-01-01T00:00:00Z',
          expiresWhen: '2027-01-01T00:00:00Z',
          secretType: 'symmetric',
          keySizeBits: 256,
          secretMaterial: 'abc',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createTenantSecret.initiate({
        tenantId: 'acme',
        body: {
          secretType: 'symmetric',
          keySizeBits: 256,
          use: null,
          algorithm: null,
          expiresWhen: '2027-01-01T00:00:00Z',
        },
      }),
    );

    expect('data' in result && result.data?.secretMaterial).toBe('abc');
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/secrets');
    expect(captured[0]?.method).toBe('POST');
  });

  it('deleteTenantSecret DELETEs the secret by id under the tenant path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.deleteTenantSecret.initiate({ tenantId: 'acme', secretId: 's-1' }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/secrets/s-1');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('updateTenantSettings PATCHes a JSON Patch diff with If-Match', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateTenantSettings.initiate({
        tenantId: 'acme',
        concurrencyToken: 'etag-3',
        original: { keep: 1, drop: 2 },
        desired: { keep: 1, added: 3 },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/settings');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-3');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'remove', path: '/drop' },
      { op: 'add', path: '/added', value: 3 },
    ]);
  });

  it('getTenantEffectiveSettings GETs the tenant effective-settings path', async () => {
    const captured = mockFetch(
      new Response(JSON.stringify({ settings: {} }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.getTenantEffectiveSettings.initiate({ tenantId: 'acme' }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/effective-settings');
    expect(captured[0]?.method).toBe('GET');
  });
});

describe('managementApi server CRUD endpoints (global scope)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createServer POSTs the request body under the server path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({ serverId: 's-new', concurrencyToken: 'c1', displayName: 'New server' }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createServer.initiate({ displayName: 'New server', settings: {} }),
    );

    expect('data' in result && result.data?.serverId).toBe('s-new');
    expect(captured[0]?.url).toBe('https://api.test/api/servers');
    expect(captured[0]?.method).toBe('POST');
  });

  it('updateServer PATCHes a replace patch with If-Match', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateServer.initiate({
        serverId: 'root',
        concurrencyToken: 'etag-7',
        fields: { displayName: 'Renamed' },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/servers/root');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-7');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/displayName', value: 'Renamed' },
    ]);
  });

  it('deleteServer DELETEs under the server path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(managementApi.endpoints.deleteServer.initiate({ serverId: 'root' }));

    expect(captured[0]?.url).toBe('https://api.test/api/servers/root');
    expect(captured[0]?.method).toBe('DELETE');
  });
});

describe('managementApi server secret + settings endpoints (global scope)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createServerSecret POSTs the request body under the server path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          secretId: 's-1',
          concurrencyToken: 'c1',
          use: null,
          algorithm: null,
          createdWhen: '2026-01-01T00:00:00Z',
          expiresWhen: '2027-01-01T00:00:00Z',
          secretType: 'symmetric',
          keySizeBits: 256,
          secretMaterial: 'abc',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.createServerSecret.initiate({
        serverId: 'root',
        body: {
          secretType: 'symmetric',
          keySizeBits: 256,
          use: null,
          algorithm: null,
          expiresWhen: '2027-01-01T00:00:00Z',
        },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/servers/root/secrets');
    expect(captured[0]?.method).toBe('POST');
  });

  it('deleteServerSecret DELETEs the secret by id under the server path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.deleteServerSecret.initiate({ serverId: 'root', secretId: 's-1' }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/servers/root/secrets/s-1');
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('updateServerSettings PATCHes a JSON Patch diff with If-Match', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateServerSettings.initiate({
        serverId: 'root',
        concurrencyToken: 'etag-5',
        original: { a: 1 },
        desired: { a: 2 },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/servers/root/settings');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-5');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/a', value: 2 },
    ]);
  });
});

describe('managementApi initial access token mint (tenant scope)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createInitialAccessToken POSTs the request body under the tenant grants path', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          initialAccessToken: 'iat-xyz',
          grantId: 'g-1',
          tenantId: 'acme',
          description: 'Partner X',
          createdWhen: '2026-01-01T00:00:00Z',
          expiresWhen: '2026-01-01T01:00:00Z',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createInitialAccessToken.initiate({
        tenantId: 'acme',
        body: { description: 'Partner X', lifetimeSeconds: 3600 },
      }),
    );

    expect('data' in result && result.data?.initialAccessToken).toBe('iat-xyz');
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/grants/initial-access-tokens');
    expect(captured[0]?.method).toBe('POST');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({
      description: 'Partner X',
      lifetimeSeconds: 3600,
    });
  });
});

describe('managementApi resource-server secret + settings endpoints (tenant scope)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('createResourceServerSecret POSTs the request body under the resource-server path', async () => {
    const captured = mockFetch(
      jsonResponse(
        {
          secretId: 's-1',
          concurrencyToken: 'c1',
          use: null,
          algorithm: null,
          createdWhen: '2026-01-01T00:00:00Z',
          expiresWhen: '2027-01-01T00:00:00Z',
          secretType: 'symmetric',
          keySizeBits: 256,
          secretMaterial: 'abc',
        },
        201,
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createResourceServerSecret.initiate({
        scope: acme,
        resourceServerId: 'rs/1',
        body: secretRequest,
      }),
    );

    expect('data' in result && result.data?.secretMaterial).toBe('abc');
    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/resource-servers/rs%2F1/secrets',
    );
    expect(captured[0]?.method).toBe('POST');
    expect(captured[0]?.authorization).toBe('Bearer token-123');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual(secretRequest);
  });

  it('deleteResourceServerSecret DELETEs the secret by id under the resource-server path', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.deleteResourceServerSecret.initiate({
        scope: acme,
        resourceServerId: 'rs-1',
        secretId: 's-1',
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/resource-servers/rs-1/secrets/s-1',
    );
    expect(captured[0]?.method).toBe('DELETE');
  });

  it('updateResourceServerSettings PATCHes a JSON Patch diff with If-Match', async () => {
    const captured = mockFetch(new Response(null, { status: 204 }));
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.updateResourceServerSettings.initiate({
        scope: acme,
        resourceServerId: 'rs-1',
        concurrencyToken: 'etag-4',
        original: { keep: 1, drop: 2, change: 'a' },
        desired: { keep: 1, change: 'b' },
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/resource-servers/rs-1/settings',
    );
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-4');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'remove', path: '/drop' },
      { op: 'replace', path: '/change', value: 'b' },
    ]);
  });
});

type QueryRequestCase = {
  readonly name: string;
  readonly url: string;
  readonly run: (store: AppStore) => PromiseLike<unknown>;
};

const queryRequestCases: readonly QueryRequestCase[] = [
  {
    name: 'getCapabilities',
    url: 'https://api.test/api/capabilities',
    run: (s) => s.dispatch(managementApi.endpoints.getCapabilities.initiate()),
  },
  {
    name: 'listTenants',
    url: 'https://api.test/api/tenants',
    run: (s) => s.dispatch(managementApi.endpoints.listTenants.initiate()),
  },
  {
    name: 'getTenant',
    url: 'https://api.test/api/tenants/acme%2F1',
    run: (s) => s.dispatch(managementApi.endpoints.getTenant.initiate('acme/1')),
  },
  {
    name: 'listServers',
    url: 'https://api.test/api/servers',
    run: (s) => s.dispatch(managementApi.endpoints.listServers.initiate()),
  },
  {
    name: 'listClients (global scope)',
    url: 'https://api.test/api/clients',
    run: (s) => s.dispatch(managementApi.endpoints.listClients.initiate({ scope: globalScope })),
  },
  {
    name: 'listClients (tenant scope)',
    url: 'https://api.test/api/tenants/acme/clients',
    run: (s) => s.dispatch(managementApi.endpoints.listClients.initiate({ scope: acme })),
  },
  {
    name: 'listResourceServers (global scope)',
    url: 'https://api.test/api/resource-servers',
    run: (s) =>
      s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: globalScope })),
  },
  {
    name: 'listResourceServers (tenant scope)',
    url: 'https://api.test/api/tenants/acme/resource-servers',
    run: (s) => s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: acme })),
  },
  {
    name: 'listGrants (subject filter)',
    url: 'https://api.test/api/tenants/acme/grants?subjectId=user-1',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.listGrants.initiate({ tenantId: 'acme', subjectId: 'user-1' }),
      ),
  },
  {
    name: 'listClientSecrets',
    url: 'https://api.test/api/tenants/acme/clients/web/secrets',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.listClientSecrets.initiate({ scope: acme, clientId: 'web' }),
      ),
  },
  {
    name: 'getClientSecretPublicKey',
    url: 'https://api.test/api/tenants/acme/clients/web/secrets/s-1/public-key',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientSecretPublicKey.initiate({
          scope: acme,
          clientId: 'web',
          secretId: 's-1',
        }),
      ),
  },
  {
    name: 'getClientSettings',
    url: 'https://api.test/api/tenants/acme/clients/web/settings',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientSettings.initiate({ scope: acme, clientId: 'web' }),
      ),
  },
  {
    name: 'getClientEffectiveSettings (global scope)',
    url: 'https://api.test/api/clients/web/effective-settings',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientEffectiveSettings.initiate({
          scope: globalScope,
          clientId: 'web',
        }),
      ),
  },
  {
    name: 'listTenantSecrets',
    url: 'https://api.test/api/tenants/acme/secrets',
    run: (s) =>
      s.dispatch(managementApi.endpoints.listTenantSecrets.initiate({ tenantId: 'acme' })),
  },
  {
    name: 'getTenantSecretPublicKey',
    url: 'https://api.test/api/tenants/acme/secrets/s-1/public-key',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getTenantSecretPublicKey.initiate({
          tenantId: 'acme',
          secretId: 's-1',
        }),
      ),
  },
  {
    name: 'getTenantSettings',
    url: 'https://api.test/api/tenants/acme/settings',
    run: (s) =>
      s.dispatch(managementApi.endpoints.getTenantSettings.initiate({ tenantId: 'acme' })),
  },
  {
    name: 'listServerSecrets',
    url: 'https://api.test/api/servers/root/secrets',
    run: (s) =>
      s.dispatch(managementApi.endpoints.listServerSecrets.initiate({ serverId: 'root' })),
  },
  {
    name: 'getServerSecretPublicKey',
    url: 'https://api.test/api/servers/root/secrets/s-1/public-key',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getServerSecretPublicKey.initiate({
          serverId: 'root',
          secretId: 's-1',
        }),
      ),
  },
  {
    name: 'getServerSettings',
    url: 'https://api.test/api/servers/root/settings',
    run: (s) =>
      s.dispatch(managementApi.endpoints.getServerSettings.initiate({ serverId: 'root' })),
  },
  {
    name: 'getServerEffectiveSettings',
    url: 'https://api.test/api/servers/root/effective-settings',
    run: (s) =>
      s.dispatch(managementApi.endpoints.getServerEffectiveSettings.initiate({ serverId: 'root' })),
  },
  {
    name: 'listResourceServerSecrets',
    url: 'https://api.test/api/tenants/acme/resource-servers/rs-1/secrets',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.listResourceServerSecrets.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
  },
  {
    name: 'getResourceServerSecretPublicKey',
    url: 'https://api.test/api/tenants/acme/resource-servers/rs-1/secrets/s-1/public-key',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getResourceServerSecretPublicKey.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          secretId: 's-1',
        }),
      ),
  },
  {
    name: 'getResourceServerSettings',
    url: 'https://api.test/api/tenants/acme/resource-servers/rs-1/settings',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getResourceServerSettings.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
  },
  {
    name: 'getResourceServerEffectiveSettings',
    url: 'https://api.test/api/tenants/acme/resource-servers/rs-1/effective-settings',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getResourceServerEffectiveSettings.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
  },
];

describe('managementApi query request shapes', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each(queryRequestCases)('$name GETs $url with the bearer token', async ({ url, run }) => {
    const captured = mockFetch(jsonResponse({}));

    await run(createStore());

    expect(captured).toEqual([
      { url, method: 'GET', authorization: 'Bearer token-123', ifMatch: null, body: '' },
    ]);
  });
});

type MutationRequestCase = {
  readonly name: string;
  readonly method: string;
  readonly url: string;
  readonly run: (store: AppStore) => PromiseLike<unknown>;
};

const globalScopeMutationCases: readonly MutationRequestCase[] = [
  {
    name: 'createClient',
    method: 'POST',
    url: 'https://api.test/api/clients',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.createClient.initiate({
          scope: globalScope,
          body: { isDisabled: false, settings: {} },
        }),
      ),
  },
  {
    name: 'updateClient',
    method: 'PATCH',
    url: 'https://api.test/api/clients/web',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.updateClient.initiate({
          scope: globalScope,
          clientId: 'web',
          concurrencyToken: 'etag',
          fields: { isDisabled: true },
        }),
      ),
  },
  {
    name: 'deleteClient',
    method: 'DELETE',
    url: 'https://api.test/api/clients/web',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteClient.initiate({ scope: globalScope, clientId: 'web' }),
      ),
  },
  {
    name: 'createResourceServer',
    method: 'POST',
    url: 'https://api.test/api/resource-servers',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.createResourceServer.initiate({
          scope: globalScope,
          body: { identifier: 'urn:api', name: 'API', isDisabled: false, settings: {} },
        }),
      ),
  },
  {
    name: 'updateResourceServer',
    method: 'PATCH',
    url: 'https://api.test/api/resource-servers/rs-1',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.updateResourceServer.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          concurrencyToken: 'etag',
          fields: { name: 'API', isDisabled: false },
        }),
      ),
  },
  {
    name: 'deleteResourceServer',
    method: 'DELETE',
    url: 'https://api.test/api/resource-servers/rs-1',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteResourceServer.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
        }),
      ),
  },
  {
    name: 'createScope',
    method: 'POST',
    url: 'https://api.test/api/resource-servers/rs-1/scopes',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.createScope.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          body: { value: 'read:x', description: null },
        }),
      ),
  },
  {
    name: 'updateScope',
    method: 'PUT',
    url: 'https://api.test/api/resource-servers/rs-1/scopes/read%3Ax',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.updateScope.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          scopeValue: 'read:x',
          body: { description: null },
        }),
      ),
  },
  {
    name: 'deleteScope',
    method: 'DELETE',
    url: 'https://api.test/api/resource-servers/rs-1/scopes/read%3Ax',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteScope.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          scopeValue: 'read:x',
        }),
      ),
  },
  {
    name: 'createClientSecret',
    method: 'POST',
    url: 'https://api.test/api/clients/web/secrets',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.createClientSecret.initiate({
          scope: globalScope,
          clientId: 'web',
          body: secretRequest,
        }),
      ),
  },
  {
    name: 'deleteClientSecret',
    method: 'DELETE',
    url: 'https://api.test/api/clients/web/secrets/s-1',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteClientSecret.initiate({
          scope: globalScope,
          clientId: 'web',
          secretId: 's-1',
        }),
      ),
  },
  {
    name: 'updateClientSettings',
    method: 'PATCH',
    url: 'https://api.test/api/clients/web/settings',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.updateClientSettings.initiate({
          scope: globalScope,
          clientId: 'web',
          concurrencyToken: 'etag',
          original: {},
          desired: { a: 1 },
        }),
      ),
  },
  {
    name: 'createResourceServerSecret',
    method: 'POST',
    url: 'https://api.test/api/resource-servers/rs-1/secrets',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.createResourceServerSecret.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          body: secretRequest,
        }),
      ),
  },
  {
    name: 'deleteResourceServerSecret',
    method: 'DELETE',
    url: 'https://api.test/api/resource-servers/rs-1/secrets/s-1',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteResourceServerSecret.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          secretId: 's-1',
        }),
      ),
  },
  {
    name: 'updateResourceServerSettings',
    method: 'PATCH',
    url: 'https://api.test/api/resource-servers/rs-1/settings',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.updateResourceServerSettings.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          concurrencyToken: 'etag',
          original: {},
          desired: { a: 1 },
        }),
      ),
  },
];

describe('managementApi scope-prefixed mutations (global scope)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each(globalScopeMutationCases)(
    '$name sends $method $url outside any tenant',
    async ({ method, url, run }) => {
      const captured = mockFetch(new Response(null, { status: 204 }));

      await run(createStore());

      expect(captured).toHaveLength(1);
      expect(captured[0]?.method).toBe(method);
      expect(captured[0]?.url).toBe(url);
      expect(captured[0]?.authorization).toBe('Bearer token-123');
    },
  );
});

type ListTagCase = {
  readonly name: string;
  readonly path: string;
  readonly body: unknown;
  readonly run: (store: AppStore) => PromiseLike<{ readonly isError: boolean }>;
  readonly collectionTag: ManagementTag;
  readonly itemTag: ManagementTag;
};

const listTagCases: readonly ListTagCase[] = [
  {
    name: 'listTenants',
    path: '/api/tenants',
    body: { items: [{ tenantId: 'acme' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listTenants.initiate()),
    collectionTag: { type: 'Tenant', id: 'LIST' },
    itemTag: { type: 'Tenant', id: 'acme' },
  },
  {
    name: 'listServers',
    path: '/api/servers',
    body: { items: [{ serverId: 'root' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listServers.initiate()),
    collectionTag: { type: 'Server', id: 'LIST' },
    itemTag: { type: 'Server', id: 'root' },
  },
  {
    name: 'listClients',
    path: '/api/tenants/acme/clients',
    body: { items: [{ clientId: 'web' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listClients.initiate({ scope: acme })),
    collectionTag: { type: 'Client', id: 'LIST' },
    itemTag: { type: 'Client', id: 'web' },
  },
  {
    name: 'listResourceServers',
    path: '/api/tenants/acme/resource-servers',
    body: { items: [{ resourceServerId: 'rs-1' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: acme })),
    collectionTag: { type: 'ResourceServer', id: 'LIST' },
    itemTag: { type: 'ResourceServer', id: 'rs-1' },
  },
  {
    name: 'listGrants',
    path: '/api/tenants/acme/grants',
    body: { items: [{ grantId: 'g-1' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listGrants.initiate({ tenantId: 'acme' })),
    collectionTag: { type: 'Grant', id: 'LIST' },
    itemTag: { type: 'Grant', id: 'g-1' },
  },
  {
    name: 'listClientSecrets',
    path: '/api/tenants/acme/clients/web/secrets',
    body: { secrets: [{ secretId: 'cs-1' }] },
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.listClientSecrets.initiate({ scope: acme, clientId: 'web' }),
      ),
    collectionTag: { type: 'Secret', id: 'client:web' },
    itemTag: { type: 'Secret', id: 'cs-1' },
  },
  {
    name: 'listTenantSecrets',
    path: '/api/tenants/acme/secrets',
    body: { secrets: [{ secretId: 'ts-1' }] },
    run: (s) =>
      s.dispatch(managementApi.endpoints.listTenantSecrets.initiate({ tenantId: 'acme' })),
    collectionTag: { type: 'Secret', id: 'tenant:acme' },
    itemTag: { type: 'Secret', id: 'ts-1' },
  },
  {
    name: 'listServerSecrets',
    path: '/api/servers/root/secrets',
    body: { secrets: [{ secretId: 'ss-1' }] },
    run: (s) =>
      s.dispatch(managementApi.endpoints.listServerSecrets.initiate({ serverId: 'root' })),
    collectionTag: { type: 'Secret', id: 'server:root' },
    itemTag: { type: 'Secret', id: 'ss-1' },
  },
  {
    name: 'listResourceServerSecrets',
    path: '/api/tenants/acme/resource-servers/rs-1/secrets',
    body: { secrets: [{ secretId: 'rss-1' }] },
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.listResourceServerSecrets.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
    collectionTag: { type: 'Secret', id: 'resource-server:rs-1' },
    itemTag: { type: 'Secret', id: 'rss-1' },
  },
];

describe('managementApi provided cache tags', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each(listTagCases)(
    '$name provides its collection tag and one tag per returned item',
    async ({ name, path, body, run, collectionTag, itemTag }) => {
      routeFetch((_method, requested) =>
        requested === path ? jsonResponse(body) : jsonResponse({ title: 'Not Found' }, 404),
      );
      const store = createStore();

      expect((await run(store)).isError).toBe(false);

      expect(invalidatedBy(store, [collectionTag])).toEqual([name]);
      expect(invalidatedBy(store, [itemTag])).toEqual([name]);
      expect(invalidatedBy(store, [{ type: itemTag.type, id: 'unrelated' }])).toEqual([]);
    },
  );

  it.each(listTagCases)(
    '$name still provides its collection tag when the request fails',
    async ({ name, run, collectionTag, itemTag }) => {
      routeFetch(() => jsonResponse({ title: 'Service Unavailable' }, 503));
      const store = createStore();

      expect((await run(store)).isError).toBe(true);

      expect(invalidatedBy(store, [collectionTag])).toEqual([name]);
      expect(invalidatedBy(store, [itemTag])).toEqual([]);
    },
  );
});

type CachedQuery = {
  readonly label: string;
  readonly path: string;
  readonly body: unknown;
  readonly subscribe: (store: AppStore) => PromiseLike<unknown>;
};

/** One subscribed cache entry per tagged (and a few untagged) query, spanning both scopes. */
const cachedQueries: readonly CachedQuery[] = [
  {
    label: 'capabilities',
    path: '/api/capabilities',
    body: {},
    subscribe: (s) => s.dispatch(managementApi.endpoints.getCapabilities.initiate()),
  },
  {
    label: 'tenants',
    path: '/api/tenants',
    body: { items: [{ tenantId: 'acme' }, { tenantId: 'other' }] },
    subscribe: (s) => s.dispatch(managementApi.endpoints.listTenants.initiate()),
  },
  {
    label: 'tenant:acme',
    path: '/api/tenants/acme',
    body: { tenantId: 'acme' },
    subscribe: (s) => s.dispatch(managementApi.endpoints.getTenant.initiate('acme')),
  },
  {
    label: 'tenant:other',
    path: '/api/tenants/other',
    body: { tenantId: 'other' },
    subscribe: (s) => s.dispatch(managementApi.endpoints.getTenant.initiate('other')),
  },
  {
    label: 'servers',
    path: '/api/servers',
    body: { items: [{ serverId: 'root' }] },
    subscribe: (s) => s.dispatch(managementApi.endpoints.listServers.initiate()),
  },
  {
    label: 'clients',
    path: '/api/tenants/acme/clients',
    body: { items: [{ clientId: 'web' }] },
    subscribe: (s) => s.dispatch(managementApi.endpoints.listClients.initiate({ scope: acme })),
  },
  {
    label: 'resourceServers',
    path: '/api/tenants/acme/resource-servers',
    body: { items: [{ resourceServerId: 'rs-1' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: acme })),
  },
  {
    label: 'grants',
    path: '/api/tenants/acme/grants',
    body: { items: [{ grantId: 'g-1' }] },
    subscribe: (s) => s.dispatch(managementApi.endpoints.listGrants.initiate({ tenantId: 'acme' })),
  },
  {
    label: 'clientSecrets:web',
    path: '/api/tenants/acme/clients/web/secrets',
    body: { secrets: [{ secretId: 'cs-1' }] },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.listClientSecrets.initiate({ scope: acme, clientId: 'web' }),
      ),
  },
  {
    label: 'clientSecrets:mobile',
    path: '/api/tenants/acme/clients/mobile/secrets',
    body: { secrets: [{ secretId: 'cs-2' }] },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.listClientSecrets.initiate({ scope: acme, clientId: 'mobile' }),
      ),
  },
  {
    label: 'clientSecretPublicKey:cs-1',
    path: '/api/tenants/acme/clients/web/secrets/cs-1/public-key',
    body: {},
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientSecretPublicKey.initiate({
          scope: acme,
          clientId: 'web',
          secretId: 'cs-1',
        }),
      ),
  },
  {
    label: 'clientSettings:web',
    path: '/api/tenants/acme/clients/web/settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientSettings.initiate({ scope: acme, clientId: 'web' }),
      ),
  },
  {
    label: 'clientEffectiveSettings:web',
    path: '/api/tenants/acme/clients/web/effective-settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientEffectiveSettings.initiate({
          scope: acme,
          clientId: 'web',
        }),
      ),
  },
  {
    label: 'clientSettings:mobile',
    path: '/api/tenants/acme/clients/mobile/settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getClientSettings.initiate({ scope: acme, clientId: 'mobile' }),
      ),
  },
  {
    label: 'tenantSecrets:acme',
    path: '/api/tenants/acme/secrets',
    body: { secrets: [{ secretId: 'ts-1' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listTenantSecrets.initiate({ tenantId: 'acme' })),
  },
  {
    label: 'tenantSecretPublicKey:ts-1',
    path: '/api/tenants/acme/secrets/ts-1/public-key',
    body: {},
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getTenantSecretPublicKey.initiate({
          tenantId: 'acme',
          secretId: 'ts-1',
        }),
      ),
  },
  {
    label: 'tenantSettings:acme',
    path: '/api/tenants/acme/settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.getTenantSettings.initiate({ tenantId: 'acme' })),
  },
  {
    label: 'tenantEffectiveSettings:acme',
    path: '/api/tenants/acme/effective-settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.getTenantEffectiveSettings.initiate({ tenantId: 'acme' })),
  },
  {
    label: 'tenantSettings:other',
    path: '/api/tenants/other/settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.getTenantSettings.initiate({ tenantId: 'other' })),
  },
  {
    label: 'serverSecrets:root',
    path: '/api/servers/root/secrets',
    body: { secrets: [{ secretId: 'ss-1' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listServerSecrets.initiate({ serverId: 'root' })),
  },
  {
    label: 'serverSecretPublicKey:ss-1',
    path: '/api/servers/root/secrets/ss-1/public-key',
    body: {},
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getServerSecretPublicKey.initiate({
          serverId: 'root',
          secretId: 'ss-1',
        }),
      ),
  },
  {
    label: 'serverSettings:root',
    path: '/api/servers/root/settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.getServerSettings.initiate({ serverId: 'root' })),
  },
  {
    label: 'serverEffectiveSettings:root',
    path: '/api/servers/root/effective-settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.getServerEffectiveSettings.initiate({ serverId: 'root' })),
  },
  {
    label: 'resourceServerSecrets:rs-1',
    path: '/api/tenants/acme/resource-servers/rs-1/secrets',
    body: { secrets: [{ secretId: 'rss-1' }] },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.listResourceServerSecrets.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
  },
  {
    label: 'resourceServerSecretPublicKey:rss-1',
    path: '/api/tenants/acme/resource-servers/rs-1/secrets/rss-1/public-key',
    body: {},
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getResourceServerSecretPublicKey.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          secretId: 'rss-1',
        }),
      ),
  },
  {
    label: 'resourceServerSettings:rs-1',
    path: '/api/tenants/acme/resource-servers/rs-1/settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getResourceServerSettings.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
  },
  {
    label: 'resourceServerEffectiveSettings:rs-1',
    path: '/api/tenants/acme/resource-servers/rs-1/effective-settings',
    body: { settings: {} },
    subscribe: (s) =>
      s.dispatch(
        managementApi.endpoints.getResourceServerEffectiveSettings.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
  },
];

type InvalidationCase = {
  readonly name: string;
  readonly mutate: (store: AppStore) => PromiseLike<unknown>;
  /** The {@link cachedQueries} labels expected to refetch, in fixture order. */
  readonly refetches: readonly string[];
};

const invalidationCases: readonly InvalidationCase[] = [
  {
    name: 'createTenant',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createTenant.initiate({
          domainName: null,
          displayName: 'New',
          isDisabled: false,
          settings: {},
        }),
      ),
    refetches: ['tenants'],
  },
  {
    name: 'updateTenant',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateTenant.initiate({
          tenantId: 'acme',
          concurrencyToken: 'etag',
          fields: { domainName: null, displayName: 'Acme', isDisabled: false },
        }),
      ),
    refetches: ['tenants', 'tenant:acme'],
  },
  {
    name: 'deleteTenant',
    mutate: (s) => s.dispatch(managementApi.endpoints.deleteTenant.initiate({ tenantId: 'acme' })),
    refetches: ['tenants', 'tenant:acme'],
  },
  {
    name: 'createServer',
    mutate: (s) =>
      s.dispatch(managementApi.endpoints.createServer.initiate({ displayName: 'S', settings: {} })),
    refetches: ['servers'],
  },
  {
    name: 'updateServer',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateServer.initiate({
          serverId: 'root',
          concurrencyToken: 'etag',
          fields: { displayName: 'Root' },
        }),
      ),
    refetches: ['servers'],
  },
  {
    name: 'deleteServer',
    mutate: (s) => s.dispatch(managementApi.endpoints.deleteServer.initiate({ serverId: 'root' })),
    refetches: ['servers'],
  },
  {
    name: 'createClient',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createClient.initiate({
          scope: acme,
          body: { isDisabled: false, settings: {} },
        }),
      ),
    refetches: ['clients'],
  },
  {
    name: 'updateClient',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateClient.initiate({
          scope: acme,
          clientId: 'web',
          concurrencyToken: 'etag',
          fields: { isDisabled: true },
        }),
      ),
    refetches: ['clients'],
  },
  {
    name: 'deleteClient',
    mutate: (s) =>
      s.dispatch(managementApi.endpoints.deleteClient.initiate({ scope: acme, clientId: 'web' })),
    refetches: ['clients'],
  },
  {
    name: 'createResourceServer',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createResourceServer.initiate({
          scope: acme,
          body: { identifier: 'urn:api', name: 'API', isDisabled: false, settings: {} },
        }),
      ),
    refetches: ['resourceServers'],
  },
  {
    name: 'updateResourceServer',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateResourceServer.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          concurrencyToken: 'etag',
          fields: { name: 'API', isDisabled: true },
        }),
      ),
    refetches: ['resourceServers'],
  },
  {
    name: 'deleteResourceServer',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteResourceServer.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
        }),
      ),
    refetches: ['resourceServers'],
  },
  {
    name: 'createScope',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createScope.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          body: { value: 'read:x', description: null },
        }),
      ),
    refetches: ['resourceServers'],
  },
  {
    name: 'updateScope',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateScope.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          scopeValue: 'read:x',
          body: { description: 'Read X' },
        }),
      ),
    refetches: ['resourceServers'],
  },
  {
    name: 'deleteScope',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteScope.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          scopeValue: 'read:x',
        }),
      ),
    refetches: ['resourceServers'],
  },
  {
    name: 'revokeGrant',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.revokeGrant.initiate({ tenantId: 'acme', grantId: 'g-1' }),
      ),
    refetches: ['grants'],
  },
  {
    name: 'revokeGrants',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.revokeGrants.initiate({ tenantId: 'acme', subjectId: 'user-1' }),
      ),
    refetches: ['grants'],
  },
  {
    name: 'createInitialAccessToken',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createInitialAccessToken.initiate({
          tenantId: 'acme',
          body: { description: null, lifetimeSeconds: null },
        }),
      ),
    refetches: ['grants'],
  },
  {
    name: 'createClientSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createClientSecret.initiate({
          scope: acme,
          clientId: 'web',
          body: secretRequest,
        }),
      ),
    refetches: ['clientSecrets:web'],
  },
  {
    name: 'deleteClientSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteClientSecret.initiate({
          scope: acme,
          clientId: 'web',
          secretId: 'cs-1',
        }),
      ),
    refetches: ['clientSecrets:web'],
  },
  {
    name: 'updateClientSettings',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateClientSettings.initiate({
          scope: acme,
          clientId: 'web',
          concurrencyToken: 'etag',
          original: {},
          desired: { a: 1 },
        }),
      ),
    refetches: ['clientSettings:web', 'clientEffectiveSettings:web'],
  },
  {
    name: 'createTenantSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createTenantSecret.initiate({
          tenantId: 'acme',
          body: secretRequest,
        }),
      ),
    refetches: ['tenantSecrets:acme'],
  },
  {
    name: 'deleteTenantSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteTenantSecret.initiate({ tenantId: 'acme', secretId: 'ts-1' }),
      ),
    refetches: ['tenantSecrets:acme'],
  },
  {
    name: 'updateTenantSettings',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateTenantSettings.initiate({
          tenantId: 'acme',
          concurrencyToken: 'etag',
          original: {},
          desired: { a: 1 },
        }),
      ),
    refetches: ['tenantSettings:acme', 'tenantEffectiveSettings:acme'],
  },
  {
    name: 'createServerSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createServerSecret.initiate({
          serverId: 'root',
          body: secretRequest,
        }),
      ),
    refetches: ['serverSecrets:root'],
  },
  {
    name: 'deleteServerSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteServerSecret.initiate({ serverId: 'root', secretId: 'ss-1' }),
      ),
    refetches: ['serverSecrets:root'],
  },
  {
    name: 'updateServerSettings',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateServerSettings.initiate({
          serverId: 'root',
          concurrencyToken: 'etag',
          original: {},
          desired: { a: 1 },
        }),
      ),
    refetches: ['serverSettings:root', 'serverEffectiveSettings:root'],
  },
  {
    name: 'createResourceServerSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createResourceServerSecret.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          body: secretRequest,
        }),
      ),
    refetches: ['resourceServerSecrets:rs-1'],
  },
  {
    name: 'deleteResourceServerSecret',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.deleteResourceServerSecret.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          secretId: 'rss-1',
        }),
      ),
    refetches: ['resourceServerSecrets:rs-1'],
  },
  {
    name: 'updateResourceServerSettings',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateResourceServerSettings.initiate({
          scope: acme,
          resourceServerId: 'rs-1',
          concurrencyToken: 'etag',
          original: {},
          desired: { a: 1 },
        }),
      ),
    refetches: ['resourceServerSettings:rs-1', 'resourceServerEffectiveSettings:rs-1'],
  },
];

describe('managementApi cache invalidation', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each(invalidationCases)(
    '$name refetches only the cache entries it affects',
    async ({ mutate, refetches }) => {
      const fetches = routeFetch((method, path) =>
        method === 'GET'
          ? jsonResponse(cachedQueries.find((q) => q.path === path)?.body ?? {})
          : new Response(null, { status: 204 }),
      );
      const store = createStore();
      await Promise.all(cachedQueries.map((q) => q.subscribe(store)));
      expect(cachedQueries.filter((q) => fetches.count('GET', q.path) !== 1)).toEqual([]);

      await mutate(store);
      await settle(store);

      expect(fetches.calls.filter((call) => !call.startsWith('GET '))).toHaveLength(1);
      const refetched = cachedQueries
        .filter((q) => fetches.count('GET', q.path) === 2)
        .map((q) => q.label);
      expect(refetched).toEqual(refetches);
    },
  );

  it('also refetches after a rejected mutation so a stale concurrency token is replaced', async () => {
    const fetches = routeFetch((method, path) =>
      method === 'GET'
        ? jsonResponse(cachedQueries.find((q) => q.path === path)?.body ?? {})
        : jsonResponse({ title: 'Precondition Failed' }, 412),
    );
    const store = createStore();
    await Promise.all(cachedQueries.map((q) => q.subscribe(store)));

    const result = await store.dispatch(
      managementApi.endpoints.updateTenant.initiate({
        tenantId: 'acme',
        concurrencyToken: 'stale',
        fields: { domainName: null, displayName: 'Acme', isDisabled: false },
      }),
    );
    await settle(store);

    expect('error' in result && result.error).toMatchObject({ status: 412 });
    expect(fetches.count('GET', '/api/tenants')).toBe(2);
    expect(fetches.count('GET', '/api/tenants/acme')).toBe(2);
    expect(fetches.count('GET', '/api/tenants/other')).toBe(1);
  });
});

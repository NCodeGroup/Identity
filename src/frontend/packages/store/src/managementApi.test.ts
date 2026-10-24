import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CreateSecretRequest, ManagementOwner } from '@ncode/api';
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
const other = { kind: 'tenant', tenantId: 'other' } as const;
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
          issuer: 'https://new.auth.test',
          mtlsEndpointAliasAuthority: 'https://new.mtls.test',
          isDisabled: false,
          displayName: 'New',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createTenant.initiate({
        tenantId: 'new',
        issuer: 'https://new.auth.test',
        mtlsEndpointAliasAuthority: 'https://new.mtls.test',
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
      tenantId: 'new',
      issuer: 'https://new.auth.test',
      mtlsEndpointAliasAuthority: 'https://new.mtls.test',
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
        fields: {
          issuer: 'https://acme.auth.test',
          mtlsEndpointAliasAuthority: 'https://acme.mtls.test',
          displayName: 'Acme',
          isDisabled: true,
        },
      }),
    );

    expect(captured).toHaveLength(1);
    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme%2F1');
    expect(captured[0]?.method).toBe('PATCH');
    expect(captured[0]?.ifMatch).toBe('etag-7');
    expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/issuer', value: 'https://acme.auth.test' },
      { op: 'replace', path: '/mtlsEndpointAliasAuthority', value: 'https://acme.mtls.test' },
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

  it('createClient POSTs the resource-server binding of a bound client', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          clientId: 'new',
          tenantId: 'acme',
          concurrencyToken: 'c1',
          isDisabled: false,
          resourceServerId: 'rs-orders',
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.createClient.initiate({
        scope,
        body: { isDisabled: false, resourceServerId: 'rs-orders', settings: {} },
      }),
    );

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/clients');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({
      isDisabled: false,
      resourceServerId: 'rs-orders',
      settings: {},
    });
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

describe('managementApi tenant usage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('getTenantUsage GETs the tenant usage path without a range by default', async () => {
    const captured = mockFetch(
      new Response(JSON.stringify({ tenantId: 'acme', from: '', to: '', entries: [] }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const store = createStore();

    await store.dispatch(managementApi.endpoints.getTenantUsage.initiate({ tenantId: 'acme' }));

    expect(captured[0]?.url).toBe('https://api.test/api/tenants/acme/usage');
    expect(captured[0]?.method).toBe('GET');
  });

  it('getTenantUsage sends the range as from/to query parameters', async () => {
    const captured = mockFetch(
      new Response(JSON.stringify({ tenantId: 'acme', from: '', to: '', entries: [] }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const store = createStore();

    await store.dispatch(
      managementApi.endpoints.getTenantUsage.initiate({
        tenantId: 'acme',
        from: '2026-10-01',
        to: '2026-10-21',
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/usage?from=2026-10-01&to=2026-10-21',
    );
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

  it('createInitialAccessToken sends the resource server a scoped token names', async () => {
    const captured = mockFetch(
      new Response(
        JSON.stringify({
          initialAccessToken: 'iat-xyz',
          grantId: 'g-1',
          tenantId: 'acme',
          description: null,
          resourceServerId: 'rs-orders',
          createdWhen: '2026-01-01T00:00:00Z',
          expiresWhen: null,
        }),
        { status: 201, headers: { 'content-type': 'application/json' } },
      ),
    );
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.createInitialAccessToken.initiate({
        tenantId: 'acme',
        body: { description: null, lifetimeSeconds: null, resourceServerId: 'rs-orders' },
      }),
    );

    expect('data' in result && result.data?.resourceServerId).toBe('rs-orders');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual({
      description: null,
      lifetimeSeconds: null,
      resourceServerId: 'rs-orders',
    });
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
    name: 'getTenantAddress (derived issuer)',
    url: 'https://api.test/api/tenants/acme/address',
    run: (s) => s.dispatch(managementApi.endpoints.getTenantAddress.initiate({ tenantId: 'acme' })),
  },
  {
    name: 'getTenantAddress (proposed issuer)',
    url: 'https://api.test/api/tenants/acme/address?issuer=https%3A%2F%2Facme.example',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getTenantAddress.initiate({
          tenantId: 'acme',
          issuer: 'https://acme.example',
        }),
      ),
  },
  {
    name: 'getTenantAddress (proposed issuer and mTLS endpoint alias)',
    url: 'https://api.test/api/tenants/acme/address?issuer=https%3A%2F%2Facme.example&mtlsEndpointAliasAuthority=https%3A%2F%2Facme.mtls.example',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getTenantAddress.initiate({
          tenantId: 'acme',
          issuer: 'https://acme.example',
          mtlsEndpointAliasAuthority: 'https://acme.mtls.example',
        }),
      ),
  },
  {
    name: 'getTenantAddress (proposed mTLS endpoint alias only)',
    url: 'https://api.test/api/tenants/acme/address?mtlsEndpointAliasAuthority=https%3A%2F%2Facme.mtls.example',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.getTenantAddress.initiate({
          tenantId: 'acme',
          mtlsEndpointAliasAuthority: 'https://acme.mtls.example',
        }),
      ),
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
    name: 'listClients (resource server filter)',
    url: 'https://api.test/api/tenants/acme/clients?resourceServerId=rs-orders',
    run: (s) =>
      s.dispatch(
        managementApi.endpoints.listClients.initiate({
          scope: acme,
          resourceServerId: 'rs-orders',
        }),
      ),
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
    collectionTag: { type: 'Tenant', id: 'global:LIST' },
    itemTag: { type: 'Tenant', id: 'global:acme' },
  },
  {
    name: 'listServers',
    path: '/api/servers',
    body: { items: [{ serverId: 'root' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listServers.initiate()),
    collectionTag: { type: 'Server', id: 'global:LIST' },
    itemTag: { type: 'Server', id: 'global:root' },
  },
  {
    name: 'listClients',
    path: '/api/tenants/acme/clients',
    body: { items: [{ clientId: 'web' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listClients.initiate({ scope: acme })),
    collectionTag: { type: 'Client', id: 'tenant:acme:LIST' },
    itemTag: { type: 'Client', id: 'tenant:acme:web' },
  },
  {
    name: 'listResourceServers',
    path: '/api/tenants/acme/resource-servers',
    body: { items: [{ resourceServerId: 'rs-1' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: acme })),
    collectionTag: { type: 'ResourceServer', id: 'tenant:acme:LIST' },
    itemTag: { type: 'ResourceServer', id: 'tenant:acme:rs-1' },
  },
  {
    name: 'listGrants',
    path: '/api/tenants/acme/grants',
    body: { items: [{ grantId: 'g-1' }] },
    run: (s) => s.dispatch(managementApi.endpoints.listGrants.initiate({ tenantId: 'acme' })),
    collectionTag: { type: 'Grant', id: 'tenant:acme:LIST' },
    itemTag: { type: 'Grant', id: 'tenant:acme:g-1' },
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

/**
 * One subscribed cache entry per tagged (and a few untagged) query, spanning both scopes. The same
 * resource ids recur in the global scope and in a second tenant, so every mutation also proves that it
 * leaves the other scopes' caches alone.
 */
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
    label: 'clients:global',
    path: '/api/clients',
    body: { items: [{ clientId: 'web' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listClients.initiate({ scope: globalScope })),
  },
  {
    label: 'clients:other',
    path: '/api/tenants/other/clients',
    body: { items: [{ clientId: 'web' }] },
    subscribe: (s) => s.dispatch(managementApi.endpoints.listClients.initiate({ scope: other })),
  },
  {
    label: 'resourceServers',
    path: '/api/tenants/acme/resource-servers',
    body: { items: [{ resourceServerId: 'rs-1' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: acme })),
  },
  {
    label: 'resourceServers:global',
    path: '/api/resource-servers',
    body: { items: [{ resourceServerId: 'rs-1' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listResourceServers.initiate({ scope: globalScope })),
  },
  {
    label: 'grants',
    path: '/api/tenants/acme/grants',
    body: { items: [{ grantId: 'g-1' }] },
    subscribe: (s) => s.dispatch(managementApi.endpoints.listGrants.initiate({ tenantId: 'acme' })),
  },
  {
    label: 'grants:other',
    path: '/api/tenants/other/grants',
    body: { items: [{ grantId: 'g-1' }] },
    subscribe: (s) =>
      s.dispatch(managementApi.endpoints.listGrants.initiate({ tenantId: 'other' })),
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
          tenantId: 'new',
          issuer: null,
          mtlsEndpointAliasAuthority: null,
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
          fields: {
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            displayName: 'Acme',
            isDisabled: false,
          },
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
    name: 'createClient in the global scope',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.createClient.initiate({
          scope: globalScope,
          body: { isDisabled: false, settings: {} },
        }),
      ),
    refetches: ['clients:global'],
  },
  {
    name: 'deleteClient in another tenant',
    mutate: (s) =>
      s.dispatch(managementApi.endpoints.deleteClient.initiate({ scope: other, clientId: 'web' })),
    refetches: ['clients:other'],
  },
  {
    name: 'updateResourceServer in the global scope',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.updateResourceServer.initiate({
          scope: globalScope,
          resourceServerId: 'rs-1',
          concurrencyToken: 'etag',
          fields: { name: 'API', isDisabled: true },
        }),
      ),
    refetches: ['resourceServers:global'],
  },
  {
    name: 'revokeGrant in another tenant',
    mutate: (s) =>
      s.dispatch(
        managementApi.endpoints.revokeGrant.initiate({ tenantId: 'other', grantId: 'g-1' }),
      ),
    refetches: ['grants:other'],
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
        fields: {
          issuer: null,
          mtlsEndpointAliasAuthority: null,
          displayName: 'Acme',
          isDisabled: false,
        },
      }),
    );
    await settle(store);

    expect('error' in result && result.error).toMatchObject({ status: 412 });
    expect(fetches.count('GET', '/api/tenants')).toBe(2);
    expect(fetches.count('GET', '/api/tenants/acme')).toBe(2);
    expect(fetches.count('GET', '/api/tenants/other')).toBe(1);
  });
});

type OwnerCase = {
  readonly key: string;
  readonly owner: ManagementOwner;
  readonly path: string;
};

/** One owner of every kind, in the global scope and in a tenant, with the API path its requests use. */
const ownerCases: readonly OwnerCase[] = [
  { key: 'server', owner: { kind: 'server', serverId: 'root' }, path: '/api/servers/root' },
  { key: 'tenant', owner: { kind: 'tenant', tenantId: 'acme' }, path: '/api/tenants/acme' },
  {
    key: 'tenant-client',
    owner: { kind: 'client', scope: acme, clientId: 'web' },
    path: '/api/tenants/acme/clients/web',
  },
  {
    key: 'global-client',
    owner: { kind: 'client', scope: globalScope, clientId: 'web' },
    path: '/api/clients/web',
  },
  {
    key: 'tenant-resource-server',
    owner: { kind: 'resource-server', scope: acme, resourceServerId: 'rs-1' },
    path: '/api/tenants/acme/resource-servers/rs-1',
  },
  {
    key: 'global-resource-server',
    owner: { kind: 'resource-server', scope: globalScope, resourceServerId: 'rs-1' },
    path: '/api/resource-servers/rs-1',
  },
];

describe('managementApi owner-generic secrets and settings requests', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each(ownerCases)('listSecrets GETs the secrets of the $key owner', async ({ owner, path }) => {
    const captured = mockFetch(jsonResponse({ secrets: [] }));
    const store = createStore();

    await store.dispatch(managementApi.endpoints.listSecrets.initiate(owner));

    expect(captured[0]?.url).toBe(`https://api.test${path}/secrets`);
    expect(captured[0]?.method).toBe('GET');
  });

  it.each(ownerCases)(
    'getSecretPublicKey GETs the public key of a secret of the $key owner',
    async ({ owner, path }) => {
      const captured = mockFetch(jsonResponse({}));
      const store = createStore();

      await store.dispatch(
        managementApi.endpoints.getSecretPublicKey.initiate({ owner, secretId: 's-1' }),
      );

      expect(captured[0]?.url).toBe(`https://api.test${path}/secrets/s-1/public-key`);
      expect(captured[0]?.method).toBe('GET');
    },
  );

  it.each(ownerCases)(
    'getSettings GETs the settings of the $key owner',
    async ({ owner, path }) => {
      const captured = mockFetch(jsonResponse({}));
      const store = createStore();

      await store.dispatch(managementApi.endpoints.getSettings.initiate(owner));

      expect(captured[0]?.url).toBe(`https://api.test${path}/settings`);
      expect(captured[0]?.method).toBe('GET');
    },
  );

  it.each(ownerCases)(
    'getEffectiveSettings GETs the effective settings of the $key owner',
    async ({ owner, path }) => {
      const captured = mockFetch(jsonResponse({}));
      const store = createStore();

      await store.dispatch(managementApi.endpoints.getEffectiveSettings.initiate(owner));

      expect(captured[0]?.url).toBe(`https://api.test${path}/effective-settings`);
      expect(captured[0]?.method).toBe('GET');
    },
  );

  it.each(ownerCases)(
    'createSecret POSTs the request body under the $key owner',
    async ({ owner, path }) => {
      const captured = mockFetch(jsonResponse({ secretId: 's-1' }, 201));
      const store = createStore();

      await store.dispatch(
        managementApi.endpoints.createSecret.initiate({ owner, body: secretRequest }),
      );

      expect(captured[0]?.url).toBe(`https://api.test${path}/secrets`);
      expect(captured[0]?.method).toBe('POST');
      expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual(secretRequest);
    },
  );

  it.each(ownerCases)(
    'deleteSecret DELETEs the secret by id under the $key owner',
    async ({ owner, path }) => {
      const captured = mockFetch(new Response(null, { status: 204 }));
      const store = createStore();

      await store.dispatch(
        managementApi.endpoints.deleteSecret.initiate({ owner, secretId: 's-1' }),
      );

      expect(captured[0]?.url).toBe(`https://api.test${path}/secrets/s-1`);
      expect(captured[0]?.method).toBe('DELETE');
    },
  );

  it.each(ownerCases)(
    'updateSettings PATCHes a JSON Patch diff with If-Match under the $key owner',
    async ({ owner, path }) => {
      const captured = mockFetch(new Response(null, { status: 204 }));
      const store = createStore();

      await store.dispatch(
        managementApi.endpoints.updateSettings.initiate({
          owner,
          concurrencyToken: 'etag',
          original: { keep: 1, drop: 2 },
          desired: { keep: 1 },
        }),
      );

      expect(captured[0]?.url).toBe(`https://api.test${path}/settings`);
      expect(captured[0]?.method).toBe('PATCH');
      expect(captured[0]?.ifMatch).toBe('etag');
      expect(JSON.parse(captured[0]?.body ?? '[]')).toEqual([{ op: 'remove', path: '/drop' }]);
    },
  );

  it('rotateSecret POSTs the replacement expiry to the secret rotate path of a client', async () => {
    const captured = mockFetch(jsonResponse({ secretId: 's-2' }, 201));
    const store = createStore();
    const body = { expiresWhen: '2027-01-01T00:00:00Z' };

    await store.dispatch(
      managementApi.endpoints.rotateSecret.initiate({
        owner: { kind: 'client', scope: acme, clientId: 'web' },
        secretId: 's-1',
        body,
      }),
    );

    expect(captured[0]?.url).toBe(
      'https://api.test/api/tenants/acme/clients/web/secrets/s-1/rotate',
    );
    expect(captured[0]?.method).toBe('POST');
    expect(JSON.parse(captured[0]?.body ?? '{}')).toEqual(body);
  });
});

/**
 * For every owner of {@link ownerCases}: its secrets, its settings and its effective settings. The same
 * ids recur across owners and scopes, so every mutation also proves that it leaves the other owners' caches alone.
 */
const ownerCachedQueries: readonly CachedQuery[] = ownerCases.flatMap(
  ({ key, owner, path }): CachedQuery[] => [
    {
      label: `secrets:${key}`,
      path: `${path}/secrets`,
      body: { secrets: [{ secretId: `${key}-secret` }] },
      subscribe: (s) => s.dispatch(managementApi.endpoints.listSecrets.initiate(owner)),
    },
    {
      label: `settings:${key}`,
      path: `${path}/settings`,
      body: {},
      subscribe: (s) => s.dispatch(managementApi.endpoints.getSettings.initiate(owner)),
    },
    {
      label: `effective:${key}`,
      path: `${path}/effective-settings`,
      body: {},
      subscribe: (s) => s.dispatch(managementApi.endpoints.getEffectiveSettings.initiate(owner)),
    },
  ],
);

/** Subscribes every owner cache entry, runs the mutation, and returns the labels that were fetched again. */
async function refetchedByOwnerMutation(
  mutate: (store: AppStore) => PromiseLike<unknown>,
): Promise<string[]> {
  const fetches = routeFetch((method, path) =>
    method === 'GET'
      ? jsonResponse(ownerCachedQueries.find((q) => q.path === path)?.body ?? {})
      : new Response(null, { status: 204 }),
  );
  const store = createStore();
  await Promise.all(ownerCachedQueries.map((q) => q.subscribe(store)));
  expect(ownerCachedQueries.filter((q) => fetches.count('GET', q.path) !== 1)).toEqual([]);

  await mutate(store);
  await settle(store);

  expect(fetches.calls.filter((call) => !call.startsWith('GET '))).toHaveLength(1);
  return ownerCachedQueries.filter((q) => fetches.count('GET', q.path) === 2).map((q) => q.label);
}

describe('managementApi owner-generic cache invalidation', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it.each(ownerCases)(
    'createSecret for the $key owner refetches only its secrets',
    async ({ key, owner }) => {
      const refetched = await refetchedByOwnerMutation((s) =>
        s.dispatch(managementApi.endpoints.createSecret.initiate({ owner, body: secretRequest })),
      );

      expect(refetched).toEqual([`secrets:${key}`]);
    },
  );

  it.each(ownerCases)(
    'deleteSecret for the $key owner refetches only its secrets',
    async ({ key, owner }) => {
      const refetched = await refetchedByOwnerMutation((s) =>
        s.dispatch(
          managementApi.endpoints.deleteSecret.initiate({ owner, secretId: `${key}-secret` }),
        ),
      );

      expect(refetched).toEqual([`secrets:${key}`]);
    },
  );

  it.each(ownerCases)(
    'updateSettings for the $key owner refetches only its settings',
    async ({ key, owner }) => {
      const refetched = await refetchedByOwnerMutation((s) =>
        s.dispatch(
          managementApi.endpoints.updateSettings.initiate({
            owner,
            concurrencyToken: 'etag',
            original: { keep: 1, drop: 2 },
            desired: { keep: 1 },
          }),
        ),
      );

      expect(refetched).toEqual([`settings:${key}`, `effective:${key}`]);
    },
  );

  it('rotateSecret for the tenant client refetches only its secrets', async () => {
    const refetched = await refetchedByOwnerMutation((s) =>
      s.dispatch(
        managementApi.endpoints.rotateSecret.initiate({
          owner: { kind: 'client', scope: acme, clientId: 'web' },
          secretId: 'tenant-client-secret',
          body: { expiresWhen: '2027-01-01T00:00:00Z' },
        }),
      ),
    );

    expect(refetched).toEqual(['secrets:tenant-client']);
  });

  it('listSecrets still provides its owner tag when the request fails', async () => {
    routeFetch(() => jsonResponse({ title: 'Service Unavailable' }, 503));
    const store = createStore();

    const result = await store.dispatch(
      managementApi.endpoints.listSecrets.initiate({ kind: 'server', serverId: 'root' }),
    );

    expect(result.isError).toBe(true);
    expect(invalidatedBy(store, [{ type: 'Secret', id: 'global:server:root' }])).toEqual([
      'listSecrets',
    ]);
  });
});

import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { createAppStore } from './store.js';
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

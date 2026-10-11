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
    auth: { accessToken: 'token-123', subject: 'admin', expiresAt: null, refreshToken: null },
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

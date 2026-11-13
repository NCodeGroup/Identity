import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  checkToken,
  consoleKeySetUrl,
  createRoot,
  fetchConsoleKeySet,
  getStatus,
} from './setupApi.js';

function json(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': contentType } });
}

function stubFetch(response: Response | Error) {
  const fetchMock = vi.fn((_input?: unknown, _init?: unknown) =>
    response instanceof Error ? Promise.reject(response) : Promise.resolve(response),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('getStatus', () => {
  it('reads the status from the relative status endpoint', async () => {
    const fetchMock = stubFetch(json(200, { tokenSource: 'generated', minimumPasswordLength: 15 }));

    const result = await getStatus();

    expect(result).toEqual({
      kind: 'ok',
      status: { tokenSource: 'generated', minimumPasswordLength: 15 },
    });
    expect(fetchMock).toHaveBeenCalledWith('./api/status', {
      headers: { accept: 'application/json' },
    });
  });

  it('reports an instance that is already set up as not found', async () => {
    stubFetch(new Response(null, { status: 404 }));

    expect(await getStatus()).toEqual({ kind: 'notFound' });
  });

  it('reports any other failure status as an error', async () => {
    stubFetch(new Response('boom', { status: 500 }));

    expect(await getStatus()).toEqual({ kind: 'error' });
  });

  it('reports a network failure as an error', async () => {
    stubFetch(new TypeError('network down'));

    expect(await getStatus()).toEqual({ kind: 'error' });
  });
});

describe('checkToken', () => {
  it('posts the token as JSON to the relative token endpoint and returns the console hint', async () => {
    const hint = { baseUrl: 'https://console.example.test', clientId: 'mgmt-console' };
    const fetchMock = stubFetch(json(200, { managementConsole: hint }));

    const result = await checkToken('t0ken');

    expect(result).toEqual({ kind: 'ok', hint });
    expect(fetchMock).toHaveBeenCalledWith('./api/token', {
      method: 'POST',
      headers: { accept: 'application/json', 'content-type': 'application/json' },
      body: JSON.stringify({ token: 't0ken' }),
    });
  });

  it.each([{ managementConsole: null }, {}, null])(
    'returns ok without a hint when the server names no console (%j)',
    async (body) => {
      stubFetch(json(200, body));

      expect(await checkToken('t0ken')).toEqual({ kind: 'ok' });
    },
  );

  it('returns the problem of a 400 as invalid', async () => {
    stubFetch(json(400, { detail: 'The setup request is not valid.' }, 'application/problem+json'));

    expect(await checkToken('t0ken')).toEqual({
      kind: 'invalid',
      problem: { detail: 'The setup request is not valid.' },
    });
  });

  it('returns the problem of a 403 as forbidden', async () => {
    stubFetch(json(403, { detail: 'The setup token is not valid.' }, 'application/problem+json'));

    expect(await checkToken('t0ken')).toEqual({
      kind: 'forbidden',
      problem: { detail: 'The setup token is not valid.' },
    });
  });

  it('returns an instance that is already set up as not found', async () => {
    stubFetch(new Response(null, { status: 404 }));

    expect(await checkToken('t0ken')).toEqual({ kind: 'notFound' });
  });

  it('returns the problem of a 429 as rate limited', async () => {
    stubFetch(json(429, { detail: 'Too many setup attempts.' }, 'application/problem+json'));

    expect(await checkToken('t0ken')).toEqual({
      kind: 'rateLimited',
      problem: { detail: 'Too many setup attempts.' },
    });
  });

  it('returns failed for any other status', async () => {
    stubFetch(new Response('boom', { status: 500 }));

    expect(await checkToken('t0ken')).toEqual({ kind: 'failed' });
  });

  it('returns failed for a network failure', async () => {
    stubFetch(new TypeError('network down'));

    expect(await checkToken('t0ken')).toEqual({ kind: 'failed' });
  });
});

describe('consoleKeySetUrl', () => {
  it('addresses the key set on the origin of the console URL', () => {
    expect(consoleKeySetUrl('https://console.example.test')).toBe(
      'https://console.example.test/bff/jwks',
    );
  });

  it('resolves the key set against the origin when the console URL has a path', () => {
    expect(consoleKeySetUrl('https://console.example.test/manage/')).toBe(
      'https://console.example.test/bff/jwks',
    );
  });

  it('throws for a value that is not a URL', () => {
    expect(() => consoleKeySetUrl('not a url')).toThrow(TypeError);
  });
});

describe('fetchConsoleKeySet', () => {
  const jwks = { keys: [{ kty: 'EC', crv: 'P-256', x: 'x', y: 'y' }] };

  it('reads the key set from the console’s BFF with a credential-less, uncached, non-redirected request', async () => {
    const fetchMock = stubFetch(json(200, jwks));
    const controller = new AbortController();

    const result = await fetchConsoleKeySet('https://console.example.test', controller.signal);

    expect(result).toEqual({ kind: 'ok', jwks });
    expect(fetchMock).toHaveBeenCalledExactlyOnceWith('https://console.example.test/bff/jwks', {
      mode: 'cors',
      credentials: 'omit',
      cache: 'no-store',
      redirect: 'error',
      signal: controller.signal,
    });
  });

  it('resolves the key set against the origin when the console URL has a trailing slash', async () => {
    const fetchMock = stubFetch(json(200, jwks));

    await fetchConsoleKeySet('https://console.example.test/');

    expect(fetchMock.mock.calls[0]?.[0]).toBe('https://console.example.test/bff/jwks');
  });

  it('returns a body that is not JSON as null for the caller to refuse', async () => {
    stubFetch(new Response('not json', { status: 200 }));

    expect(await fetchConsoleKeySet('https://console.example.test')).toEqual({
      kind: 'ok',
      jwks: null,
    });
  });

  it('returns the status of a non-2xx answer', async () => {
    stubFetch(new Response('nope', { status: 503 }));

    expect(await fetchConsoleKeySet('https://console.example.test')).toEqual({
      kind: 'status',
      status: 503,
    });
  });

  it('returns unreachable for a network or CORS failure', async () => {
    stubFetch(new TypeError('Failed to fetch'));

    expect(await fetchConsoleKeySet('https://console.example.test')).toEqual({
      kind: 'unreachable',
    });
  });

  it('returns unreachable for a URL that cannot be parsed', async () => {
    const fetchMock = stubFetch(json(200, jwks));

    expect(await fetchConsoleKeySet('not a url')).toEqual({ kind: 'unreachable' });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('returns aborted when the signal aborted the request', async () => {
    const controller = new AbortController();
    controller.abort();
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(controller.signal.reason)),
    );

    expect(await fetchConsoleKeySet('https://console.example.test', controller.signal)).toEqual({
      kind: 'aborted',
    });
  });

  it('returns aborted for an abort error without a signal', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new DOMException('The operation was aborted.', 'AbortError'))),
    );

    expect(await fetchConsoleKeySet('https://console.example.test')).toEqual({ kind: 'aborted' });
  });

  it('returns unreachable for any other DOM exception', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new DOMException('The network failed.', 'NetworkError'))),
    );

    expect(await fetchConsoleKeySet('https://console.example.test')).toEqual({
      kind: 'unreachable',
    });
  });
});

describe('createRoot', () => {
  const request = { token: 't0ken', userName: 'root', email: 'root@example.test', password: 'pw' };

  it('posts the request as JSON to the relative root endpoint and returns the created root', async () => {
    const created = { tenantId: 'tenant', principalId: 'principal', userName: 'root' };
    const fetchMock = stubFetch(json(201, created));

    const result = await createRoot(request);

    expect(result).toEqual({ kind: 'created', root: created });
    expect(fetchMock).toHaveBeenCalledWith('./api/root', {
      method: 'POST',
      headers: { accept: 'application/json', 'content-type': 'application/json' },
      body: JSON.stringify(request),
    });
  });

  it('posts the management console registration and returns the registered client id', async () => {
    const withConsole = {
      ...request,
      managementConsole: {
        clientId: 'mgmt-console',
        baseUrl: 'https://console.example.test',
        jwks: { keys: [{ kty: 'EC', crv: 'P-256', x: 'x', y: 'y' }] },
      },
    };
    const created = {
      tenantId: 'tenant',
      principalId: 'principal',
      userName: 'root',
      managementConsoleClientId: 'mgmt-console',
    };
    const fetchMock = stubFetch(json(201, created));

    const result = await createRoot(withConsole);

    expect(result).toEqual({ kind: 'created', root: created });
    expect(fetchMock).toHaveBeenCalledWith('./api/root', {
      method: 'POST',
      headers: { accept: 'application/json', 'content-type': 'application/json' },
      body: JSON.stringify(withConsole),
    });
  });

  it.each([400, 409])('returns the problem detail of a %i as invalid', async (status) => {
    stubFetch(
      json(status, { title: 'Invalid', detail: 'Password too short.' }, 'application/problem+json'),
    );

    expect(await createRoot(request)).toEqual({
      kind: 'invalid',
      problem: { title: 'Invalid', detail: 'Password too short.' },
    });
  });

  it('returns the problem of a 403 as forbidden', async () => {
    stubFetch(json(403, { title: 'Forbidden', detail: 'Bad token.' }, 'application/problem+json'));

    expect(await createRoot(request)).toEqual({
      kind: 'forbidden',
      problem: { title: 'Forbidden', detail: 'Bad token.' },
    });
  });

  it('returns an instance that is already set up as not found', async () => {
    stubFetch(new Response(null, { status: 404 }));

    expect(await createRoot(request)).toEqual({ kind: 'notFound' });
  });

  it('returns the problem of a 429 as rate limited', async () => {
    stubFetch(json(429, { title: 'Too many requests' }, 'application/problem+json'));

    expect(await createRoot(request)).toEqual({
      kind: 'rateLimited',
      problem: { title: 'Too many requests' },
    });
  });

  it('returns an empty problem when the body is not problem details', async () => {
    stubFetch(new Response('not json', { status: 403 }));

    expect(await createRoot(request)).toEqual({ kind: 'forbidden', problem: {} });
  });

  it('returns an empty problem when the body is null or its fields are not strings', async () => {
    stubFetch(json(400, null));
    expect(await createRoot(request)).toEqual({ kind: 'invalid', problem: {} });

    stubFetch(json(400, { title: 1, detail: 2 }));
    expect(await createRoot(request)).toEqual({ kind: 'invalid', problem: {} });
  });

  it('returns failed for any other status', async () => {
    stubFetch(new Response('boom', { status: 500 }));

    expect(await createRoot(request)).toEqual({ kind: 'failed' });
  });

  it('returns failed for a network failure', async () => {
    stubFetch(new TypeError('network down'));

    expect(await createRoot(request)).toEqual({ kind: 'failed' });
  });
});

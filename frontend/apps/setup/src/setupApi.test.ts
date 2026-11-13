import { afterEach, describe, expect, it, vi } from 'vitest';
import { createRoot, getStatus } from './setupApi.js';

function json(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': contentType } });
}

function stubFetch(response: Response | Error) {
  const fetchMock = vi.fn(() =>
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

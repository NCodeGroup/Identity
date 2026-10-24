import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fetchSurfaceJson } from './surfaceFetch.js';

describe('fetchSurfaceJson', () => {
  const originalLocation = window.location;
  let assignMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    assignMock = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { origin: 'https://op.test', href: 'https://op.test/account', assign: assignMock },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: originalLocation,
    });
  });

  it('fetches the surface endpoint as JSON', async () => {
    const fetchMock = vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ subject: 'user-123' }), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        }),
      ),
    );
    vi.stubGlobal('fetch', fetchMock);

    const body = await fetchSurfaceJson<{ subject: string }>('me', './');

    expect(body).toEqual({ subject: 'user-123' });
    expect(fetchMock).toHaveBeenCalledWith('/account/me', {
      headers: { accept: 'application/json' },
    });
    expect(assignMock).not.toHaveBeenCalled();
  });

  it('sends the browser to the login page with the return path on 401', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
    vi.stubGlobal('fetch', fetchMock);

    const body = await fetchSurfaceJson('sessions/list', 'sessions');

    expect(body).toBeNull();
    expect(assignMock).toHaveBeenCalledTimes(1);
    expect(assignMock).toHaveBeenCalledWith('/account/login?ReturnUrl=%2Faccount%2Fsessions');
  });

  it('yields null on any other failure without reading the body', async () => {
    const response = new Response('boom', { status: 500 });
    const json = vi.spyOn(response, 'json');
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(response)),
    );

    const body = await fetchSurfaceJson('me', './');

    expect(body).toBeNull();
    expect(json).not.toHaveBeenCalled();
    expect(assignMock).not.toHaveBeenCalled();
  });

  it('yields null without a request when fetch is unavailable', async () => {
    vi.stubGlobal('fetch', undefined);

    const body = await fetchSurfaceJson('me', './');

    expect(body).toBeNull();
    expect(assignMock).not.toHaveBeenCalled();
  });
});

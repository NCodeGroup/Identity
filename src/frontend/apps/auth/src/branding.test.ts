import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadBranding } from './branding.js';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('loadBranding', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('fetches the branding beneath the tenant surface and keeps its well-formed tokens', async () => {
    const fetchFn = vi.fn(() =>
      Promise.resolve(
        jsonResponse({ displayName: 'Acme', primaryColor: '#1565c0', mode: 'bogus' }),
      ),
    );

    const branding = await loadBranding(fetchFn, 'https://op.example/acme/account/');

    expect(branding).toEqual({ displayName: 'Acme', primaryColor: '#1565c0' });
    expect(fetchFn).toHaveBeenCalledWith('/acme/account/branding', {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
  });

  it('resolves against the document base and the global fetch by default', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(jsonResponse({ mode: 'dark' })));
    vi.stubGlobal('fetch', fetchMock);

    await expect(loadBranding()).resolves.toEqual({ mode: 'dark' });
    expect(fetchMock).toHaveBeenCalledWith('/account/branding', expect.anything());
  });

  it('applies the default presentation when the host answers with an error', async () => {
    const fetchFn = vi.fn(() => Promise.resolve(jsonResponse({}, 404)));

    await expect(loadBranding(fetchFn, 'https://op.example/account/')).resolves.toBeUndefined();
  });

  it('applies the default presentation when the request fails', async () => {
    const fetchFn = vi.fn(() => Promise.reject(new TypeError('offline')));

    await expect(loadBranding(fetchFn, 'https://op.example/account/')).resolves.toBeUndefined();
  });
});

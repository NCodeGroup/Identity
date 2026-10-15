import { describe, expect, it, vi } from 'vitest';
import { fetchRawDiscovery, type FetchLike } from './discovery.js';

describe('fetchRawDiscovery', () => {
  it('requests the well-known document for the issuer and returns the object', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ issuer: 'https://op.test' }),
      }),
    );

    const raw = await fetchRawDiscovery('https://op.test/', fetchFn);

    expect(fetchFn).toHaveBeenCalledWith(
      'https://op.test/.well-known/openid-configuration',
      undefined,
    );
    expect(raw.issuer).toBe('https://op.test');
  });

  it('throws when the fetch fails', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({ ok: false, status: 404, json: () => Promise.resolve({}) }),
    );
    await expect(fetchRawDiscovery('https://op.test', fetchFn)).rejects.toThrow(/HTTP 404/);
  });

  it('throws when the document is not a JSON object', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve('nope') }),
    );
    await expect(fetchRawDiscovery('https://op.test', fetchFn)).rejects.toThrow(/JSON object/);
  });
});

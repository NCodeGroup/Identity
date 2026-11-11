import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchRawDiscovery, type RawDiscovery } from '@ncode/conformance-core';
import { loadDiscovery } from './discovery.js';

vi.mock(import('@ncode/conformance-core'), async (importOriginal) => ({
  ...(await importOriginal()),
  fetchRawDiscovery: vi.fn(),
}));

const fetchRawDiscoveryMock = vi.mocked(fetchRawDiscovery);

afterEach(() => {
  vi.unstubAllGlobals();
  vi.resetAllMocks();
});

describe('loadDiscovery', () => {
  const discovery: RawDiscovery = { issuer: 'https://op.test' };

  it('fetches the raw discovery for the issuer with the global fetch and the signal', async () => {
    const fetchStub = vi.fn<typeof fetch>();
    vi.stubGlobal('fetch', fetchStub);
    fetchRawDiscoveryMock.mockResolvedValue(discovery);
    const controller = new AbortController();

    await expect(loadDiscovery('https://op.test', controller.signal)).resolves.toBe(discovery);

    expect(fetchRawDiscoveryMock).toHaveBeenCalledExactlyOnceWith(
      'https://op.test',
      fetchStub,
      controller.signal,
    );
  });

  it('passes no signal when the caller supplies none', async () => {
    const fetchStub = vi.fn<typeof fetch>();
    vi.stubGlobal('fetch', fetchStub);
    fetchRawDiscoveryMock.mockResolvedValue(discovery);

    await loadDiscovery('https://op.test');

    expect(fetchRawDiscoveryMock).toHaveBeenCalledExactlyOnceWith(
      'https://op.test',
      fetchStub,
      undefined,
    );
  });
});

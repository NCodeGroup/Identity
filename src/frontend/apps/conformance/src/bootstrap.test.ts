import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadRuntimeConfig, type RuntimeConfig } from '@ncode/config';
import { fetchRawDiscovery, type RawDiscovery } from '@ncode/conformance-core';
import { bootstrapRuntimeConfig, currentOrigin, loadDiscovery } from './bootstrap.js';

vi.mock(import('@ncode/config'), async (importOriginal) => ({
  ...(await importOriginal()),
  loadRuntimeConfig: vi.fn(),
}));

vi.mock(import('@ncode/conformance-core'), async (importOriginal) => ({
  ...(await importOriginal()),
  fetchRawDiscovery: vi.fn(),
}));

const loadRuntimeConfigMock = vi.mocked(loadRuntimeConfig);
const fetchRawDiscoveryMock = vi.mocked(fetchRawDiscovery);

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

afterEach(() => {
  vi.unstubAllGlobals();
  vi.resetAllMocks();
});

describe('currentOrigin', () => {
  it('returns the origin of the live location', () => {
    vi.stubGlobal('location', { origin: 'https://ui.test' });

    expect(currentOrigin()).toBe('https://ui.test');
  });
});

describe('bootstrapRuntimeConfig', () => {
  it('loads the conformance surface config from the serving origin', async () => {
    vi.stubGlobal('location', { origin: 'https://ui.test' });
    loadRuntimeConfigMock.mockResolvedValue(runtime);

    await expect(bootstrapRuntimeConfig()).resolves.toBe(runtime);

    expect(loadRuntimeConfigMock).toHaveBeenCalledOnce();
    // Strict equality proves no `signal` key is sent when the caller supplies none.
    expect(loadRuntimeConfigMock.mock.calls[0]![0]).toStrictEqual({
      origin: 'https://ui.test',
      surface: 'conformance',
    });
  });

  it('forwards the abort signal when one is supplied', async () => {
    vi.stubGlobal('location', { origin: 'https://ui.test' });
    loadRuntimeConfigMock.mockResolvedValue(runtime);
    const controller = new AbortController();

    await bootstrapRuntimeConfig(controller.signal);

    const options = loadRuntimeConfigMock.mock.calls[0]![0];
    expect(options.origin).toBe('https://ui.test');
    expect(options.surface).toBe('conformance');
    expect(options.signal).toBe(controller.signal);
  });
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

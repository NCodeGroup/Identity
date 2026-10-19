import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadRuntimeConfig, type RuntimeConfig } from '@ncode/config';
import { bootstrapRuntimeConfig, createFlowDeps, currentOrigin } from './bootstrap.js';

vi.mock(import('@ncode/config'), async (importOriginal) => ({
  ...(await importOriginal()),
  loadRuntimeConfig: vi.fn(),
}));

const loadRuntimeConfigMock = vi.mocked(loadRuntimeConfig);

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  clientId: 'playground-spa',
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
  it('loads the playground surface config from the serving origin', async () => {
    vi.stubGlobal('location', { origin: 'https://ui.test' });
    loadRuntimeConfigMock.mockResolvedValue(runtime);

    await expect(bootstrapRuntimeConfig()).resolves.toBe(runtime);

    expect(loadRuntimeConfigMock).toHaveBeenCalledOnce();
    // Strict equality proves no `signal` key is sent when the caller supplies none.
    expect(loadRuntimeConfigMock.mock.calls[0]![0]).toStrictEqual({
      origin: 'https://ui.test',
      surface: 'playground',
    });
  });

  it('forwards the abort signal when one is supplied', async () => {
    vi.stubGlobal('location', { origin: 'https://ui.test' });
    loadRuntimeConfigMock.mockResolvedValue(runtime);
    const controller = new AbortController();

    await bootstrapRuntimeConfig(controller.signal);

    const options = loadRuntimeConfigMock.mock.calls[0]![0];
    expect(options.origin).toBe('https://ui.test');
    expect(options.surface).toBe('playground');
    expect(options.signal).toBe(controller.signal);
  });
});

describe('createFlowDeps', () => {
  function stubFetch() {
    const response = new Response('{}', { status: 200 });
    const fetchStub = vi.fn<typeof fetch>(() => Promise.resolve(response));
    vi.stubGlobal('fetch', fetchStub);
    return { fetchStub, response };
  }

  it('omits an empty body so a bodyless GET is accepted by fetch', async () => {
    const { fetchStub, response } = stubFetch();
    const headers = { accept: 'application/json', authorization: 'Bearer at' };

    const result = await createFlowDeps().fetchFn('https://op.test/userinfo', {
      method: 'GET',
      headers,
      body: '',
    });

    expect(result).toBe(response);
    expect(fetchStub).toHaveBeenCalledOnce();
    const [url, init] = fetchStub.mock.calls[0]!;
    expect(url).toBe('https://op.test/userinfo');
    expect(init).toStrictEqual({ method: 'GET', headers });
  });

  it('forwards a non-empty form body with the method and headers', async () => {
    const { fetchStub } = stubFetch();
    const headers = { 'content-type': 'application/x-www-form-urlencoded' };

    await createFlowDeps().fetchFn('https://op.test/token', {
      method: 'POST',
      headers,
      body: 'grant_type=refresh_token',
    });

    const [url, init] = fetchStub.mock.calls[0]!;
    expect(url).toBe('https://op.test/token');
    expect(init).toStrictEqual({ method: 'POST', headers, body: 'grant_type=refresh_token' });
  });

  it('persists the in-flight transaction in session storage', () => {
    expect(createFlowDeps().storage).toBe(window.sessionStorage);
  });
});

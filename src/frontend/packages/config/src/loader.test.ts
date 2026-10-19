import { afterEach, describe, expect, it, vi } from 'vitest';
import { defineStandaloneSurfaceConfig } from '@ncode/vite-config';
import {
  BOOTSTRAP_PATH,
  DISCOVERY_PATH,
  createRuntimeConfigLoader,
  loadRuntimeConfig,
  type FetchLike,
} from './loader.js';
import { RuntimeConfigError } from './parse.js';

const okJson = (body: unknown): ReturnType<FetchLike> =>
  Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) });

const bootstrapBody = { apiBaseUrl: 'https://api.example', issuer: 'https://op.example' };

const discoveryBody = {
  issuer: 'https://op.example',
  authorization_endpoint: 'https://op.example/authorize',
  token_endpoint: 'https://op.example/token',
};

/** Serves the bootstrap document for any bootstrap URL and the discovery document otherwise. */
function serving(bootstrap: unknown = bootstrapBody, discovery: unknown = discoveryBody) {
  return vi.fn<FetchLike>((url) => okJson(url.includes(BOOTSTRAP_PATH) ? bootstrap : discovery));
}

describe('loadRuntimeConfig', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('publishes the well-known bootstrap and discovery paths', () => {
    expect(BOOTSTRAP_PATH).toBe('/config/bootstrap.json');
    expect(DISCOVERY_PATH).toBe('/.well-known/openid-configuration');
  });

  it('loads bootstrap then discovery and merges them', async () => {
    const fetchFn = serving();

    const result = await loadRuntimeConfig({ origin: 'https://ui.example/', fetchFn });

    expect(fetchFn).toHaveBeenNthCalledWith(
      1,
      'https://ui.example/config/bootstrap.json',
      undefined,
    );
    expect(fetchFn).toHaveBeenNthCalledWith(
      2,
      'https://op.example/.well-known/openid-configuration',
      undefined,
    );
    expect(result.apiBaseUrl).toBe('https://api.example');
    expect(result.authorizationEndpoint).toBe('https://op.example/authorize');
  });

  it('throws when a fetch fails', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({ ok: false, status: 503, json: () => Promise.resolve({}) }),
    );
    await expect(loadRuntimeConfig({ origin: 'https://ui.example', fetchFn })).rejects.toThrow(
      /HTTP 503/,
    );
  });

  it('names the discovery URL when only the discovery fetch fails', async () => {
    const fetchFn = vi.fn<FetchLike>((url) =>
      url.includes(BOOTSTRAP_PATH)
        ? okJson(bootstrapBody)
        : Promise.resolve({ ok: false, status: 404, json: () => Promise.resolve({}) }),
    );

    const error = await loadRuntimeConfig({ origin: 'https://ui.example', fetchFn }).catch(
      (cause: unknown) => cause,
    );

    expect(error).toBeInstanceOf(RuntimeConfigError);
    expect(error).toMatchObject({
      message: "Failed to fetch 'https://op.example/.well-known/openid-configuration' (HTTP 404).",
    });
  });

  it('requests surface-specific coordinates when a surface is given', async () => {
    const fetchFn = serving({ ...bootstrapBody, clientId: 'playground-spa' });

    const result = await loadRuntimeConfig({
      origin: 'https://ui.example',
      surface: 'playground',
      fetchFn,
    });

    expect(fetchFn).toHaveBeenNthCalledWith(
      1,
      'https://ui.example/config/bootstrap.json?surface=playground',
      undefined,
    );
    expect(result.clientId).toBe('playground-spa');
  });

  it('forwards the abort signal to both fetches', async () => {
    const fetchFn = serving();
    const { signal } = new AbortController();

    await loadRuntimeConfig({ origin: 'https://ui.example', fetchFn, signal });

    expect(fetchFn).toHaveBeenCalledTimes(2);
    expect(fetchFn).toHaveBeenNthCalledWith(1, 'https://ui.example/config/bootstrap.json', {
      signal,
    });
    expect(fetchFn).toHaveBeenNthCalledWith(
      2,
      'https://op.example/.well-known/openid-configuration',
      { signal },
    );
  });

  it('defaults to the global fetch when none is injected', async () => {
    const globalFetch = serving();
    vi.stubGlobal('fetch', globalFetch);

    const result = await loadRuntimeConfig({ origin: 'https://ui.example' });

    expect(globalFetch).toHaveBeenCalledTimes(2);
    expect(result.tokenEndpoint).toBe('https://op.example/token');
  });

  it('throws when no fetch implementation is available', async () => {
    vi.stubGlobal('fetch', undefined);

    await expect(loadRuntimeConfig({ origin: 'https://ui.example' })).rejects.toThrow(
      new RuntimeConfigError('No fetch implementation available to load runtime config.'),
    );
  });

  it('rejects discovery metadata whose issuer differs from the bootstrap issuer', async () => {
    const fetchFn = serving(bootstrapBody, { ...discoveryBody, issuer: 'https://evil.example' });

    await expect(loadRuntimeConfig({ origin: 'https://ui.example', fetchFn })).rejects.toThrow(
      /Issuer mismatch/,
    );
  });

  it('defaults to the origin of the page that serves the bundle', async () => {
    vi.stubGlobal('location', { origin: 'https://ui.example' });
    const fetchFn = serving();

    await loadRuntimeConfig({ fetchFn });

    expect(fetchFn).toHaveBeenNthCalledWith(
      1,
      'https://ui.example/config/bootstrap.json',
      undefined,
    );
  });

  it('throws when no serving origin is available', async () => {
    vi.stubGlobal('location', undefined);
    const fetchFn = serving();

    await expect(loadRuntimeConfig({ fetchFn })).rejects.toThrow(
      new RuntimeConfigError('No serving origin available to load runtime config.'),
    );
    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('sends any surface name, since the host defines the surfaces', async () => {
    const fetchFn = serving();

    await loadRuntimeConfig({ origin: 'https://ui.example', surface: 'partner portal', fetchFn });

    expect(fetchFn).toHaveBeenNthCalledWith(
      1,
      'https://ui.example/config/bootstrap.json?surface=partner%20portal',
      undefined,
    );
  });

  it('is served by the bootstrap path the standalone dev servers proxy to the host', () => {
    const config = defineStandaloneSurfaceConfig({
      port: 5173,
      bootstrapHost: { service: 'api', fallbackOrigin: 'https://localhost:5001' },
      coverage: { statements: 0, branches: 0, functions: 0, lines: 0 },
      env: {},
    });

    expect(Object.keys(config.server?.proxy ?? {})).toStrictEqual([BOOTSTRAP_PATH]);
  });
});

describe('createRuntimeConfigLoader', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('binds the options and forwards the signal of each load', async () => {
    const fetchFn = serving();
    const { signal } = new AbortController();
    const load = createRuntimeConfigLoader({
      origin: 'https://ui.example',
      surface: 'mgmt',
      fetchFn,
    });

    const result = await load(signal);

    expect(fetchFn).toHaveBeenNthCalledWith(
      1,
      'https://ui.example/config/bootstrap.json?surface=mgmt',
      { signal },
    );
    expect(fetchFn).toHaveBeenNthCalledWith(
      2,
      'https://op.example/.well-known/openid-configuration',
      { signal },
    );
    expect(result.tokenEndpoint).toBe('https://op.example/token');
  });

  it('loads the host default document from the serving origin when given no options', async () => {
    vi.stubGlobal('location', { origin: 'https://ui.example' });
    const globalFetch = serving();
    vi.stubGlobal('fetch', globalFetch);
    const { signal } = new AbortController();

    await createRuntimeConfigLoader()(signal);

    expect(globalFetch).toHaveBeenNthCalledWith(1, 'https://ui.example/config/bootstrap.json', {
      signal,
    });
  });
});

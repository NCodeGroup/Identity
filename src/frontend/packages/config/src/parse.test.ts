import { describe, expect, it, vi } from 'vitest';
import {
  mergeRuntimeConfig,
  normalizeBaseUrl,
  parseBootstrapDocument,
  parseOpenIdDiscovery,
  RuntimeConfigError,
} from './parse.js';
import { loadRuntimeConfig, type FetchLike } from './loader.js';
import type { BootstrapDocument, OpenIdDiscovery } from './types.js';

describe('normalizeBaseUrl', () => {
  it('strips a single trailing slash', () => {
    expect(normalizeBaseUrl('https://op.example/')).toBe('https://op.example');
  });

  it('leaves a url without a trailing slash unchanged', () => {
    expect(normalizeBaseUrl('https://op.example')).toBe('https://op.example');
  });
});

describe('parseBootstrapDocument', () => {
  it('normalizes and defaults optional fields', () => {
    const result = parseBootstrapDocument({
      apiBaseUrl: 'https://api.example/',
      issuer: 'https://op.example/',
    });
    expect(result).toEqual({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      features: {},
    });
  });

  it('preserves features and branding', () => {
    const result = parseBootstrapDocument({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      features: { dcr: true },
      branding: { displayName: 'Acme' },
    });
    expect(result.features).toEqual({ dcr: true });
    expect(result.branding).toEqual({ displayName: 'Acme' });
  });

  it('rejects a non-object document', () => {
    expect(() => parseBootstrapDocument('nope')).toThrow(RuntimeConfigError);
  });

  it('rejects a missing required field', () => {
    expect(() => parseBootstrapDocument({ apiBaseUrl: 'https://api.example' })).toThrow(/issuer/);
  });
});

describe('parseOpenIdDiscovery', () => {
  it('extracts the required and optional endpoints', () => {
    const result = parseOpenIdDiscovery({
      issuer: 'https://op.example',
      authorization_endpoint: 'https://op.example/authorize',
      token_endpoint: 'https://op.example/token',
      userinfo_endpoint: 'https://op.example/userinfo',
    });
    expect(result.authorization_endpoint).toBe('https://op.example/authorize');
    expect(result.userinfo_endpoint).toBe('https://op.example/userinfo');
    expect(result.end_session_endpoint).toBeUndefined();
  });

  it('rejects a document missing token_endpoint', () => {
    expect(() =>
      parseOpenIdDiscovery({
        issuer: 'https://op.example',
        authorization_endpoint: 'https://op.example/authorize',
      }),
    ).toThrow(/token_endpoint/);
  });
});

describe('mergeRuntimeConfig', () => {
  const bootstrap: BootstrapDocument = {
    apiBaseUrl: 'https://api.example',
    issuer: 'https://op.example',
    features: { dcr: true },
  };
  const discovery: OpenIdDiscovery = {
    issuer: 'https://op.example',
    authorization_endpoint: 'https://op.example/authorize',
    token_endpoint: 'https://op.example/token',
  };

  it('merges matching documents', () => {
    const result = mergeRuntimeConfig(bootstrap, discovery);
    expect(result).toEqual({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      authorizationEndpoint: 'https://op.example/authorize',
      tokenEndpoint: 'https://op.example/token',
      scopes: ['openid', 'read:clients'],
      features: { dcr: true },
    });
  });

  it('applies bootstrap client id and scopes when present', () => {
    const result = mergeRuntimeConfig(
      { ...bootstrap, clientId: 'mgmt-spa', scopes: ['openid', 'write:clients'] },
      discovery,
    );
    expect(result.clientId).toBe('mgmt-spa');
    expect(result.scopes).toEqual(['openid', 'write:clients']);
  });

  it('rejects an issuer mismatch (open-redirect / misconfiguration guard)', () => {
    expect(() =>
      mergeRuntimeConfig(bootstrap, { ...discovery, issuer: 'https://evil.example' }),
    ).toThrow(/Issuer mismatch/);
  });
});

describe('loadRuntimeConfig', () => {
  const okJson = (body: unknown): ReturnType<FetchLike> =>
    Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) });

  it('loads bootstrap then discovery and merges them', async () => {
    const fetchFn = vi.fn<FetchLike>((url) => {
      if (url.endsWith('/config/bootstrap.json')) {
        return okJson({ apiBaseUrl: 'https://api.example', issuer: 'https://op.example' });
      }
      return okJson({
        issuer: 'https://op.example',
        authorization_endpoint: 'https://op.example/authorize',
        token_endpoint: 'https://op.example/token',
      });
    });

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

  it('requests surface-specific coordinates when a surface is given', async () => {
    const fetchFn = vi.fn<FetchLike>((url) => {
      if (url.includes('/config/bootstrap.json')) {
        return okJson({
          apiBaseUrl: 'https://api.example',
          issuer: 'https://op.example',
          clientId: 'playground-spa',
        });
      }
      return okJson({
        issuer: 'https://op.example',
        authorization_endpoint: 'https://op.example/authorize',
        token_endpoint: 'https://op.example/token',
      });
    });

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
});

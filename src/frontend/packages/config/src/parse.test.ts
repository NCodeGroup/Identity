import { describe, expect, it } from 'vitest';
import {
  mergeRuntimeConfig,
  normalizeBaseUrl,
  parseBffBootstrapDocument,
  parseBootstrapDocument,
  parseBrandingConfig,
  parseOpenIdDiscovery,
  RuntimeConfigError,
  toBffRuntimeConfig,
} from './parse.js';
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

  it('keeps a root-relative api base', () => {
    expect(
      parseBootstrapDocument({ apiBaseUrl: '/identity/', issuer: 'https://op.example' }).apiBaseUrl,
    ).toBe('/identity');
    expect(
      parseBootstrapDocument({ apiBaseUrl: '/', issuer: 'https://op.example' }).apiBaseUrl,
    ).toBe('');
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

  it('rejects a null document', () => {
    expect(() => parseBootstrapDocument(null)).toThrow('Bootstrap document must be a JSON object.');
  });

  it('rejects a missing required field', () => {
    expect(() => parseBootstrapDocument({ apiBaseUrl: 'https://api.example' })).toThrow(/issuer/);
  });

  it('rejects a blank required field', () => {
    expect(() =>
      parseBootstrapDocument({ apiBaseUrl: '   ', issuer: 'https://op.example' }),
    ).toThrow("Bootstrap document is missing required field 'apiBaseUrl'.");
  });

  it('keeps a client id and an all-string scopes list', () => {
    const result = parseBootstrapDocument({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      clientId: 'mgmt-spa',
      scopes: ['openid', 'write:clients'],
    });
    expect(result.clientId).toBe('mgmt-spa');
    expect(result.scopes).toEqual(['openid', 'write:clients']);
  });

  it('drops malformed optional fields instead of trusting them', () => {
    const result = parseBootstrapDocument({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      clientId: '',
      scopes: ['openid', 42],
      features: 'all',
      branding: 'acme',
    });
    expect(result).toEqual({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      features: {},
    });
  });

  it('ignores a non-string client id and a non-array scopes value', () => {
    const result = parseBootstrapDocument({
      apiBaseUrl: 'https://api.example',
      issuer: 'https://op.example',
      clientId: 7,
      scopes: 'openid',
    });
    expect(result).not.toHaveProperty('clientId');
    expect(result).not.toHaveProperty('scopes');
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

  it('extracts the end-session endpoint and JWKS URI when advertised', () => {
    const result = parseOpenIdDiscovery({
      issuer: 'https://op.example',
      authorization_endpoint: 'https://op.example/authorize',
      token_endpoint: 'https://op.example/token',
      end_session_endpoint: 'https://op.example/end-session',
      jwks_uri: 'https://op.example/jwks',
    });
    expect(result).toEqual({
      issuer: 'https://op.example',
      authorization_endpoint: 'https://op.example/authorize',
      token_endpoint: 'https://op.example/token',
      end_session_endpoint: 'https://op.example/end-session',
      jwks_uri: 'https://op.example/jwks',
    });
  });

  it('ignores non-string optional endpoints', () => {
    const result = parseOpenIdDiscovery({
      issuer: 'https://op.example',
      authorization_endpoint: 'https://op.example/authorize',
      token_endpoint: 'https://op.example/token',
      end_session_endpoint: true,
      userinfo_endpoint: 1,
      jwks_uri: null,
    });
    expect(result).toEqual({
      issuer: 'https://op.example',
      authorization_endpoint: 'https://op.example/authorize',
      token_endpoint: 'https://op.example/token',
    });
  });

  it.each([null, 'nope', 42])('rejects a non-object document (%s)', (raw) => {
    expect(() => parseOpenIdDiscovery(raw)).toThrow('Discovery document must be a JSON object.');
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
      kind: 'openid',
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

  it('accepts a discovery issuer that differs only by a trailing slash', () => {
    const result = mergeRuntimeConfig(bootstrap, { ...discovery, issuer: 'https://op.example/' });
    expect(result.issuer).toBe('https://op.example');
  });

  it('falls back to the default scopes when the bootstrap scopes list is empty', () => {
    const result = mergeRuntimeConfig({ ...bootstrap, scopes: [] }, discovery);
    expect(result.scopes).toEqual(['openid', 'read:clients']);
  });

  it('defaults features to an empty set when the bootstrap omits them', () => {
    const result = mergeRuntimeConfig(
      { apiBaseUrl: 'https://api.example', issuer: 'https://op.example' },
      discovery,
    );
    expect(result.features).toEqual({});
  });

  it('maps the optional discovery endpoints and passes branding through', () => {
    const result = mergeRuntimeConfig(
      { ...bootstrap, branding: { displayName: 'Acme', mode: 'dark' } },
      {
        ...discovery,
        end_session_endpoint: 'https://op.example/end-session',
        userinfo_endpoint: 'https://op.example/userinfo',
      },
    );
    expect(result.endSessionEndpoint).toBe('https://op.example/end-session');
    expect(result.userInfoEndpoint).toBe('https://op.example/userinfo');
    expect(result.branding).toEqual({ displayName: 'Acme', mode: 'dark' });
  });
});

describe('parseBffBootstrapDocument', () => {
  const document = {
    apiBaseUrl: '/management/',
    bff: { basePath: '/bff/', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
    features: { dcr: true },
    branding: { displayName: 'Acme' },
  };

  it('parses and normalizes a full document', () => {
    expect(parseBffBootstrapDocument(document)).toEqual({
      apiBaseUrl: '/management',
      bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
      features: { dcr: true },
      branding: { displayName: 'Acme' },
    });
  });

  it('defaults features to an empty set and omits absent branding', () => {
    const result = parseBffBootstrapDocument({ apiBaseUrl: '/management', bff: document.bff });
    expect(result.features).toEqual({});
    expect(result).not.toHaveProperty('branding');
  });

  it.each([null, 'nope', 42])('rejects a non-object document (%s)', (raw) => {
    expect(() => parseBffBootstrapDocument(raw)).toThrow(
      'Bootstrap document must be a JSON object.',
    );
  });

  it('rejects a missing api base url', () => {
    expect(() => parseBffBootstrapDocument({ bff: document.bff })).toThrow(
      "Bootstrap document is missing required field 'apiBaseUrl'.",
    );
  });

  it.each([undefined, null, 'bff', ['/bff']])('rejects a missing bff (%s)', (bff) => {
    expect(() => parseBffBootstrapDocument({ apiBaseUrl: '/management', bff })).toThrow(
      "Bootstrap document is missing required field 'bff'.",
    );
  });

  it('rejects a missing base path', () => {
    expect(() =>
      parseBffBootstrapDocument({
        ...document,
        bff: { antiforgeryHeader: document.bff.antiforgeryHeader },
      }),
    ).toThrow("Bootstrap document is missing required field 'bff.basePath'.");
  });

  it('rejects a base path without a leading slash', () => {
    expect(() =>
      parseBffBootstrapDocument({ ...document, bff: { ...document.bff, basePath: 'bff' } }),
    ).toThrow(RuntimeConfigError);
  });

  it.each([undefined, null, 'X-CSRF'])('rejects a missing anti-forgery header (%s)', (header) => {
    expect(() =>
      parseBffBootstrapDocument({
        ...document,
        bff: { basePath: '/bff', antiforgeryHeader: header },
      }),
    ).toThrow("Bootstrap document is missing required field 'bff.antiforgeryHeader.name'.");
  });

  it('rejects a missing anti-forgery header name', () => {
    expect(() =>
      parseBffBootstrapDocument({
        ...document,
        bff: { basePath: '/bff', antiforgeryHeader: { value: '1' } },
      }),
    ).toThrow("Bootstrap document is missing required field 'bff.antiforgeryHeader.name'.");
  });

  it('rejects a missing anti-forgery header value', () => {
    expect(() =>
      parseBffBootstrapDocument({
        ...document,
        bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF' } },
      }),
    ).toThrow("Bootstrap document is missing required field 'bff.antiforgeryHeader.value'.");
  });
});

describe('toBffRuntimeConfig', () => {
  const bff = { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } };

  it('defaults features to an empty set and omits absent branding', () => {
    expect(toBffRuntimeConfig({ apiBaseUrl: '/management', bff })).toEqual({
      kind: 'bff',
      apiBaseUrl: '/management',
      bff,
      features: {},
    });
  });

  it('passes features and branding through', () => {
    expect(
      toBffRuntimeConfig({
        apiBaseUrl: '/management',
        bff,
        features: { dcr: true },
        branding: { displayName: 'Acme' },
      }),
    ).toEqual({
      kind: 'bff',
      apiBaseUrl: '/management',
      bff,
      features: { dcr: true },
      branding: { displayName: 'Acme' },
    });
  });
});

describe('parseBrandingConfig', () => {
  it('keeps every token that has the expected shape', () => {
    expect(
      parseBrandingConfig({
        displayName: 'Acme',
        logoUrl: 'https://cdn.acme.example/logo.svg',
        primaryColor: '#1565c0',
        secondaryColor: '#9c27b0',
        mode: 'dark',
      }),
    ).toEqual({
      displayName: 'Acme',
      logoUrl: 'https://cdn.acme.example/logo.svg',
      primaryColor: '#1565c0',
      secondaryColor: '#9c27b0',
      mode: 'dark',
    });
  });

  it('drops tokens that are blank, not strings, or not a known mode', () => {
    expect(
      parseBrandingConfig({
        displayName: ' ',
        logoUrl: 7,
        primaryColor: '#1565c0',
        mode: 'system',
      }),
    ).toEqual({ primaryColor: '#1565c0' });
  });

  it.each([null, undefined, 'acme', 42, ['#fff'], {}, { mode: 'Dark' }])(
    'returns undefined for %j, so the default theme applies',
    (raw) => {
      expect(parseBrandingConfig(raw)).toBeUndefined();
    },
  );

  describe('environment', () => {
    it('parses the type and the name', () => {
      expect(
        parseBrandingConfig({ environment: { type: 'development', name: 'sandbox' } }),
      ).toEqual({ environment: { type: 'development', name: 'sandbox' } });
    });

    it('drops a blank name', () => {
      expect(parseBrandingConfig({ environment: { type: 'development', name: '  ' } })).toEqual({
        environment: { type: 'development' },
      });
    });

    it.each([{}, { name: 'sandbox' }, { type: ' ', name: 'sandbox' }, { type: 7 }])(
      'ignores %j, which has no usable type',
      (environment) => {
        expect(parseBrandingConfig({ displayName: 'Acme', environment })).toEqual({
          displayName: 'Acme',
        });
      },
    );

    it.each([null, 'development', 42, ['development']])(
      'ignores the non-object %j',
      (environment) => {
        expect(parseBrandingConfig({ displayName: 'Acme', environment })).toEqual({
          displayName: 'Acme',
        });
      },
    );

    it('yields a defined branding from an environment alone', () => {
      expect(parseBrandingConfig({ environment: { type: 'test' } })).toEqual({
        environment: { type: 'test' },
      });
    });
  });
});

import { describe, expect, it } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { authConfigFromRuntime, endSessionUrlFromRuntime, redirectUri } from './auth.js';

const baseRuntime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid', 'read:clients'],
  features: {},
};

describe('redirectUri', () => {
  it('appends the callback path to the origin', () => {
    expect(redirectUri('https://ui.test')).toBe('https://ui.test/callback');
  });
});

describe('authConfigFromRuntime', () => {
  it('returns null when no client id is configured (open/dev mode)', () => {
    expect(authConfigFromRuntime(baseRuntime, 'https://ui.test')).toBeNull();
  });

  it('builds the client config when a client id is present', () => {
    const config = authConfigFromRuntime(
      { ...baseRuntime, clientId: 'mgmt-spa' },
      'https://ui.test',
    );
    expect(config).toEqual({
      authorizationEndpoint: 'https://op.test/authorize',
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      redirectUri: 'https://ui.test/callback',
      scopes: ['openid', 'read:clients'],
    });
  });
});

describe('endSessionUrlFromRuntime', () => {
  it('returns null when the OP advertises no end_session_endpoint', () => {
    expect(endSessionUrlFromRuntime(baseRuntime, 'id-token', 'https://ui.test')).toBeNull();
  });

  it('builds the RP-Initiated Logout URL with the id token hint, client id, and post-logout redirect', () => {
    const result = endSessionUrlFromRuntime(
      {
        ...baseRuntime,
        clientId: 'mgmt-spa',
        endSessionEndpoint: 'https://op.test/end-session',
      },
      'id-token',
      'https://ui.test',
    );
    const url = new URL(result!);
    expect(url.origin + url.pathname).toBe('https://op.test/end-session');
    expect(url.searchParams.get('id_token_hint')).toBe('id-token');
    expect(url.searchParams.get('client_id')).toBe('mgmt-spa');
    expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://ui.test/');
  });

  it('omits the id token hint when no id token is known', () => {
    const result = endSessionUrlFromRuntime(
      { ...baseRuntime, endSessionEndpoint: 'https://op.test/end-session' },
      null,
      'https://ui.test',
    );
    const url = new URL(result!);
    expect(url.searchParams.has('id_token_hint')).toBe(false);
    expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://ui.test/');
  });
});

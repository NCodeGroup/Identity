import { describe, expect, it } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { authConfigFromRuntime, redirectUri } from './auth.js';

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

import { describe, expect, it } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { configSlice, runtimeConfigLoaded } from './configSlice.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: { dcr: true },
};

describe('configSlice', () => {
  it('starts with no runtime configuration', () => {
    expect(configSlice.reducer(undefined, { type: '@@INIT' })).toEqual({ runtime: null });
  });

  it('stores the discovered runtime configuration', () => {
    const state = configSlice.reducer(undefined, runtimeConfigLoaded(runtime));
    expect(state.runtime).toBe(runtime);
  });

  it('replaces a previously loaded configuration wholesale', () => {
    const first = configSlice.reducer(undefined, runtimeConfigLoaded(runtime));
    const next: RuntimeConfig = {
      apiBaseUrl: 'https://api2.test',
      issuer: 'https://op2.test',
      authorizationEndpoint: 'https://op2.test/authorize',
      tokenEndpoint: 'https://op2.test/token',
      scopes: ['openid', 'read:clients'],
      features: {},
    };

    const state = configSlice.reducer(first, runtimeConfigLoaded(next));

    expect(state).toEqual({ runtime: next });
    expect(state).not.toBe(first);
    expect(first.runtime).toBe(runtime);
  });
});

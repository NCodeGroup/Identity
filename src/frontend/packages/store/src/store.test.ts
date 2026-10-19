import { describe, expect, it } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { signedIn } from './authSlice.js';
import { runtimeConfigLoaded } from './configSlice.js';
import { managementApi } from './managementApi.js';
import { createAppStore } from './store.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

type ApiState = ReturnType<typeof managementApi.reducer>;

describe('createAppStore', () => {
  it('starts with empty config and anonymous auth when nothing is preloaded', () => {
    const store = createAppStore();
    const state = store.getState();

    expect(state.config).toEqual({ runtime: null });
    expect(state.auth).toEqual({
      accessToken: null,
      subject: null,
      expiresAt: null,
      refreshToken: null,
      idToken: null,
    });
    expect(state).toHaveProperty(managementApi.reducerPath);
  });

  it('hydrates the slices from preloaded state', () => {
    const store = createAppStore({
      config: { runtime },
      auth: {
        accessToken: 'token-123',
        subject: 'admin',
        expiresAt: 42,
        refreshToken: null,
        idToken: null,
      },
    });

    expect(store.getState().config.runtime).toBe(runtime);
    expect(store.getState().auth.subject).toBe('admin');
    expect(store.getState().auth.expiresAt).toBe(42);
  });

  it('routes slice actions to the config and auth reducers', () => {
    const store = createAppStore();

    store.dispatch(runtimeConfigLoaded(runtime));
    store.dispatch(signedIn({ accessToken: 'abc', subject: 'user-1' }));

    expect(store.getState().config.runtime).toBe(runtime);
    expect(store.getState().auth.accessToken).toBe('abc');
  });

  it('registers the Management API middleware against its reducer', () => {
    const store = createAppStore();

    store.dispatch(runtimeConfigLoaded(runtime));

    const apiState = store.getState()[managementApi.reducerPath] as ApiState;
    expect(apiState.config.reducerPath).toBe('managementApi');
    expect(apiState.config.middlewareRegistered).toBe(true);
  });

  it('creates independent stores', () => {
    const first = createAppStore();
    const second = createAppStore();

    first.dispatch(runtimeConfigLoaded(runtime));

    expect(second.getState().config.runtime).toBeNull();
  });
});

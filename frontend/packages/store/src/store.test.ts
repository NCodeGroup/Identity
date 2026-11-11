import { describe, expect, it } from 'vitest';
import type { RuntimeConfig } from '@ncode/config';
import { runtimeConfigLoaded } from './configSlice.js';
import { managementApi } from './managementApi.js';
import { sessionEnded, sessionStarted, type BffUser } from './sessionSlice.js';
import { createAppStore } from './store.js';

const runtime: RuntimeConfig = {
  kind: 'openid',
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const user: BffUser = {
  sub: 'admin',
  claims: {},
  session: { expiresAt: 2_000_000_000, idleExpiresAt: 1_900_000_000 },
  logoutUrl: '/bff/logout',
  logoutSid: 'sid-1',
};

type ApiState = ReturnType<typeof managementApi.reducer>;

describe('createAppStore', () => {
  it('starts with empty config and an unknown session when nothing is preloaded', () => {
    const store = createAppStore();
    const state = store.getState();

    expect(state.config).toEqual({ runtime: null });
    expect(state.session).toEqual({ status: 'unknown', user: null });
    expect(state).toHaveProperty(managementApi.reducerPath);
  });

  it('hydrates the slices from preloaded state', () => {
    const store = createAppStore({
      config: { runtime },
      session: { status: 'signed-in', user },
    });

    expect(store.getState().config.runtime).toBe(runtime);
    expect(store.getState().session.status).toBe('signed-in');
    expect(store.getState().session.user).toBe(user);
  });

  it('routes slice actions to the config and session reducers', () => {
    const store = createAppStore();

    store.dispatch(runtimeConfigLoaded(runtime));
    store.dispatch(sessionStarted(user));

    expect(store.getState().config.runtime).toBe(runtime);
    expect(store.getState().session).toEqual({ status: 'signed-in', user });

    store.dispatch(sessionEnded());

    expect(store.getState().session).toEqual({ status: 'signed-out', user: null });
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

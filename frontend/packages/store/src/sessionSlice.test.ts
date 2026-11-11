import { describe, expect, it } from 'vitest';
import { sessionEnded, sessionSlice, sessionStarted, type BffUser } from './sessionSlice.js';

const user: BffUser = {
  sub: 'admin',
  name: 'Ada',
  claims: { email: 'ada@example.test' },
  session: { expiresAt: 2_000, idleExpiresAt: 1_500 },
  logoutUrl: '/bff/logout',
  logoutSid: 'sid-1',
};

describe('sessionSlice', () => {
  it('starts with an unknown session', () => {
    expect(sessionSlice.reducer(undefined, { type: '@@INIT' })).toEqual({
      status: 'unknown',
      user: null,
    });
  });

  it('holds the user once the session has started', () => {
    const state = sessionSlice.reducer(undefined, sessionStarted(user));

    expect(state).toEqual({ status: 'signed-in', user });
    expect(state.user).toBe(user);
  });

  it('clears the user when the session ends', () => {
    const started = sessionSlice.reducer(undefined, sessionStarted(user));

    const state = sessionSlice.reducer(started, sessionEnded());

    expect(state).toEqual({ status: 'signed-out', user: null });
    expect(state).not.toBe(started);
    expect(started.user).toBe(user);
  });

  it('is signed out when a session that never started ends', () => {
    expect(sessionSlice.reducer(undefined, sessionEnded())).toEqual({
      status: 'signed-out',
      user: null,
    });
  });

  it('replaces the user when another session starts', () => {
    const started = sessionSlice.reducer(undefined, sessionStarted(user));
    const other: BffUser = { ...user, sub: 'other', logoutSid: 'sid-2' };

    const state = sessionSlice.reducer(started, sessionStarted(other));

    expect(state).toEqual({ status: 'signed-in', user: other });
  });
});

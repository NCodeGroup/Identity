import { describe, expect, it } from 'vitest';
import { authSlice, signedIn, signedOut } from './authSlice.js';

describe('authSlice', () => {
  it('starts anonymous', () => {
    const state = authSlice.reducer(undefined, { type: '@@INIT' });
    expect(state).toEqual({
      accessToken: null,
      subject: null,
      expiresAt: null,
      refreshToken: null,
      idToken: null,
    });
  });

  it('stores the token, subject, expiry, refresh token, and id token on sign-in', () => {
    const state = authSlice.reducer(
      undefined,
      signedIn({
        accessToken: 'abc',
        subject: 'user-1',
        expiresAt: 123,
        refreshToken: 'rt',
        idToken: 'it',
      }),
    );
    expect(state).toEqual({
      accessToken: 'abc',
      subject: 'user-1',
      expiresAt: 123,
      refreshToken: 'rt',
      idToken: 'it',
    });
  });

  it('defaults expiry, refresh token, and id token to null when omitted', () => {
    const state = authSlice.reducer(undefined, signedIn({ accessToken: 'abc', subject: 'user-1' }));
    expect(state.expiresAt).toBeNull();
    expect(state.refreshToken).toBeNull();
    expect(state.idToken).toBeNull();
  });

  it('clears credentials on sign-out', () => {
    const signedInState = authSlice.reducer(
      undefined,
      signedIn({ accessToken: 'abc', subject: 'user-1', expiresAt: 123, idToken: 'it' }),
    );
    const state = authSlice.reducer(signedInState, signedOut());
    expect(state).toEqual({
      accessToken: null,
      subject: null,
      expiresAt: null,
      refreshToken: null,
      idToken: null,
    });
  });
});

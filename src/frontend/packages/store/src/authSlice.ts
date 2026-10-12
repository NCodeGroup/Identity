import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export type AuthState = {
  readonly accessToken: string | null;
  readonly subject: string | null;
  /** Absolute expiry (epoch milliseconds), or null when unknown/signed-out. */
  readonly expiresAt: number | null;
  readonly refreshToken: string | null;
  /** The ID token from the most recent login; replayed as `id_token_hint` on RP-Initiated Logout. */
  readonly idToken: string | null;
};

const initialState: AuthState = {
  accessToken: null,
  subject: null,
  expiresAt: null,
  refreshToken: null,
  idToken: null,
};

export type SignedInPayload = {
  readonly accessToken: string;
  readonly subject: string;
  readonly expiresAt?: number;
  readonly refreshToken?: string;
  readonly idToken?: string;
};

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    signedIn(state, action: PayloadAction<SignedInPayload>) {
      state.accessToken = action.payload.accessToken;
      state.subject = action.payload.subject;
      state.expiresAt = action.payload.expiresAt ?? null;
      state.refreshToken = action.payload.refreshToken ?? null;
      state.idToken = action.payload.idToken ?? null;
    },
    signedOut(state) {
      state.accessToken = null;
      state.subject = null;
      state.expiresAt = null;
      state.refreshToken = null;
      state.idToken = null;
    },
  },
});

export const { signedIn, signedOut } = authSlice.actions;

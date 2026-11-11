import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/** The lifetime of a BFF session, as the BFF reports it. Both instants are epoch seconds. */
export type BffSessionLifetime = {
  /** The absolute expiry of the session. */
  readonly expiresAt: number;
  /** The expiry of the session's idle window. */
  readonly idleExpiresAt: number;
};

/** The signed-in user, as the BFF's user endpoint describes it. */
export type BffUser = {
  readonly sub: string;
  readonly name?: string;
  readonly claims: Readonly<Record<string, unknown>>;
  readonly session: BffSessionLifetime;
  /** The URL the logout form posts to. */
  readonly logoutUrl: string;
  /** The session id the logout form carries. */
  readonly logoutSid: string;
};

/**
 * Whether the browser holds a BFF session: `unknown` until the first probe answers, then `signed-in` or
 * `signed-out`.
 */
export type SessionStatus = 'unknown' | 'signed-in' | 'signed-out';

export type SessionState = {
  readonly status: SessionStatus;
  readonly user: BffUser | null;
};

const initialState: SessionState = { status: 'unknown', user: null };

export const sessionSlice = createSlice({
  name: 'session',
  initialState,
  reducers: {
    // Return a fresh state rather than mutating the Immer draft: the user is immutable and replaced
    // wholesale, and its readonly fields don't satisfy the writable-draft type.
    sessionStarted(_state, action: PayloadAction<BffUser>): SessionState {
      return { status: 'signed-in', user: action.payload };
    },
    sessionEnded(): SessionState {
      return { status: 'signed-out', user: null };
    },
  },
});

export const { sessionStarted, sessionEnded } = sessionSlice.actions;

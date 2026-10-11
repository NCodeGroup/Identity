import { AuthError, refreshAccessToken, type FetchLike, type TokenSet } from './flow.js';

/** Default seconds before absolute expiry at which a silent renewal fires. */
export const DEFAULT_RENEWAL_SKEW_SECONDS = 60;

/**
 * Milliseconds from `nowEpochMs` until a renewal should fire: `skewSeconds` ahead of the absolute
 * expiry, floored at `0` so an already-due (or past) token renews immediately rather than scheduling
 * a negative delay.
 */
export function renewalDelayMs(
  expiresAtEpochMs: number,
  nowEpochMs: number,
  skewSeconds: number = DEFAULT_RENEWAL_SKEW_SECONDS,
): number {
  return Math.max(0, expiresAtEpochMs - skewSeconds * 1000 - nowEpochMs);
}

/** A running silent-renewal loop. {@link schedule} (re)arms it; {@link stop} cancels it. */
export type TokenRenewer = {
  /**
   * Arms (or re-arms) the renewal timer for the given absolute expiry. Passing `null` cancels any
   * pending renewal (e.g. after sign-out, or when the current token set has no expiry hint).
   */
  schedule(expiresAtEpochMs: number | null): void;
  /** Cancels any pending renewal. Idempotent. */
  stop(): void;
};

/** Dependencies for {@link createTokenRenewer}. Timer/clock are injectable so the loop is testable. */
export type TokenRenewerOptions = {
  readonly tokenEndpoint: string;
  readonly clientId: string;
  readonly fetchFn: FetchLike;
  /** Supplies the current refresh token at fire time; it may rotate between renewals. */
  readonly getRefreshToken: () => string | null;
  /** Invoked with the new token set after a successful renewal. */
  readonly onRenewed: (tokens: TokenSet) => void;
  /** Invoked when renewal cannot proceed (no refresh token) or the refresh request fails. */
  readonly onError: (error: unknown) => void;
  /** Seconds before expiry to renew. Defaults to {@link DEFAULT_RENEWAL_SKEW_SECONDS}. */
  readonly skewSeconds?: number;
};

/**
 * Creates a self-sustaining silent token-renewal loop. On each scheduled fire it exchanges the current
 * refresh token for a fresh token set, reports it through `onRenewed`, and re-arms itself from the new
 * token's `expires_in`. Failures (or a missing refresh token) surface through `onError` and stop the
 * loop, letting the caller fall back to an interactive sign-in.
 *
 * Framework-agnostic: it relies only on the ambient `setTimeout`/`clearTimeout`/`Date.now`, so it is
 * driven deterministically in tests with fake timers.
 */
export function createTokenRenewer(options: TokenRenewerOptions): TokenRenewer {
  const skewSeconds = options.skewSeconds ?? DEFAULT_RENEWAL_SKEW_SECONDS;
  let handle: ReturnType<typeof setTimeout> | null = null;

  const stop = () => {
    if (handle !== null) {
      clearTimeout(handle);
      handle = null;
    }
  };

  const fire = () => {
    handle = null;
    const refreshToken = options.getRefreshToken();
    if (!refreshToken) {
      options.onError(new AuthError('No refresh token is available for silent renewal.'));
      return;
    }
    refreshAccessToken(
      options.tokenEndpoint,
      { clientId: options.clientId, refreshToken },
      options.fetchFn,
    )
      .then((tokens) => {
        options.onRenewed(tokens);
        schedule(tokens.expiresInSeconds ? Date.now() + tokens.expiresInSeconds * 1000 : null);
      })
      .catch((cause: unknown) => {
        options.onError(cause);
      });
  };

  const schedule = (expiresAtEpochMs: number | null) => {
    stop();
    if (expiresAtEpochMs === null) {
      return;
    }
    handle = setTimeout(fire, renewalDelayMs(expiresAtEpochMs, Date.now(), skewSeconds));
  };

  return { schedule, stop };
}

import {
  AuthError,
  buildAuthorizeUrl,
  buildEndSessionUrl,
  decodeJwtClaims,
  deriveCodeChallenge,
  exchangeCodeForToken,
  generateCodeVerifier,
  parseCallbackParams,
  randomUrlSafe,
  refreshAccessToken,
  type FetchLike,
  type RandomBytes,
  type TokenSet,
} from '@ncode/auth';
import type { RuntimeConfig } from '@ncode/config';

/** Path, relative to the app origin, the OpenID Provider redirects back to after authorization. */
export const CALLBACK_PATH = '/callback';

/** `sessionStorage` key under which the in-flight explorer transaction is persisted across the redirect. */
export const TRANSACTION_KEY = 'ncode.playground.tx';

/** Minimal key/value storage (e.g. `sessionStorage`) for the in-flight transaction. */
export type KeyValueStorage = {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
};

/** Injected dependencies so the explorer orchestration is deterministic and DOM-free under test. */
export type FlowDeps = {
  readonly fetchFn: FetchLike;
  readonly storage: KeyValueStorage;
  readonly randomBytes?: RandomBytes;
  readonly subtle?: SubtleCrypto;
};

/** The user-tunable inputs to an authorization-code + PKCE request. */
export type FlowRequest = {
  readonly clientId: string;
  readonly scopes: readonly string[];
};

/** The in-flight authorization transaction and the artifacts the explorer surfaces for inspection. */
export type FlowTransaction = {
  readonly clientId: string;
  readonly redirectUri: string;
  readonly scopes: readonly string[];
  readonly state: string;
  readonly nonce: string;
  readonly codeVerifier: string;
  readonly codeChallenge: string;
};

/** The redirect URI this explorer registers: the app origin plus the callback path. */
export function playgroundRedirectUri(origin: string): string {
  return `${origin}${CALLBACK_PATH}`;
}

/** True when the query string carries an authorization-code response (code+state or error). */
export function isCallback(search: string): boolean {
  return parseCallbackParams(search) !== null;
}

/**
 * Begins an authorization-code + PKCE (S256) request: generates the verifier/challenge, `state` (CSRF), and `nonce`
 * (OIDC replay binding), persists the transaction for the redirect, and returns the `/authorize` URL plus the full
 * set of artifacts so the explorer can display every step. UI-free so it is unit-testable.
 */
export async function beginFlow(
  runtime: RuntimeConfig,
  request: FlowRequest,
  origin: string,
  deps: FlowDeps,
): Promise<{ authorizeUrl: string; transaction: FlowTransaction }> {
  const redirectUri = playgroundRedirectUri(origin);
  const codeVerifier = generateCodeVerifier(deps.randomBytes);
  const codeChallenge = await deriveCodeChallenge(codeVerifier, deps.subtle);
  const state = randomUrlSafe(32, deps.randomBytes);
  const nonce = randomUrlSafe(32, deps.randomBytes);

  const transaction: FlowTransaction = {
    clientId: request.clientId,
    redirectUri,
    scopes: request.scopes,
    state,
    nonce,
    codeVerifier,
    codeChallenge,
  };
  deps.storage.setItem(TRANSACTION_KEY, JSON.stringify(transaction));

  const authorizeUrl = buildAuthorizeUrl(runtime.authorizationEndpoint, {
    clientId: request.clientId,
    redirectUri,
    scopes: request.scopes,
    state,
    codeChallenge,
    nonce,
  });

  return { authorizeUrl, transaction };
}

/** Reads the persisted in-flight transaction, or `null` when none is present. */
export function loadTransaction(deps: FlowDeps): FlowTransaction | null {
  const raw = deps.storage.getItem(TRANSACTION_KEY);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as FlowTransaction;
  } catch {
    return null;
  }
}

/** Clears any persisted in-flight transaction. */
export function clearTransaction(deps: FlowDeps): void {
  deps.storage.removeItem(TRANSACTION_KEY);
}

/** The successfully-parsed authorization-code callback and the transaction it belongs to. */
export type CallbackResult = {
  readonly code: string;
  readonly state: string;
  readonly transaction: FlowTransaction;
};

/**
 * Validates a redirect callback against the persisted transaction: surfaces a server `error`, requires a matching
 * in-flight transaction, and enforces the `state` match (CSRF guard). Does not consume the transaction, so the
 * caller can still display the PKCE verifier used for the subsequent code exchange.
 */
export function readCallback(search: string, deps: FlowDeps): CallbackResult {
  const result = parseCallbackParams(search);
  if (result === null) {
    throw new AuthError('No authorization response is present in the callback.');
  }
  if ('error' in result) {
    throw new AuthError(result.errorDescription ?? result.error, result.error);
  }

  const transaction = loadTransaction(deps);
  if (!transaction) {
    throw new AuthError('No in-flight authorization transaction was found.');
  }
  if (transaction.state !== result.state) {
    throw new AuthError('Authorization state mismatch (possible CSRF).');
  }

  return { code: result.code, state: result.state, transaction };
}

/** Exchanges the authorization code for tokens using the transaction's PKCE verifier, then clears the transaction. */
export async function exchangeCode(
  runtime: RuntimeConfig,
  callback: CallbackResult,
  deps: FlowDeps,
): Promise<TokenSet> {
  const tokens = await exchangeCodeForToken(
    runtime.tokenEndpoint,
    {
      clientId: callback.transaction.clientId,
      code: callback.code,
      redirectUri: callback.transaction.redirectUri,
      codeVerifier: callback.transaction.codeVerifier,
    },
    deps.fetchFn,
  );
  clearTransaction(deps);
  return tokens;
}

/** Exchanges a refresh token for a fresh token set (public client). */
export function refreshTokens(
  runtime: RuntimeConfig,
  clientId: string,
  refreshToken: string,
  deps: FlowDeps,
): Promise<TokenSet> {
  return refreshAccessToken(runtime.tokenEndpoint, { clientId, refreshToken }, deps.fetchFn);
}

/**
 * Calls the UserInfo endpoint with the access token as a Bearer credential. Throws when the OpenID Provider
 * advertises no `userinfo_endpoint` or the call fails.
 */
export async function fetchUserInfo(
  runtime: RuntimeConfig,
  accessToken: string,
  deps: FlowDeps,
): Promise<Record<string, unknown>> {
  if (!runtime.userInfoEndpoint) {
    throw new AuthError('The OpenID Provider does not advertise a userinfo_endpoint.');
  }
  const response = await deps.fetchFn(runtime.userInfoEndpoint, {
    method: 'GET',
    headers: { accept: 'application/json', authorization: `Bearer ${accessToken}` },
    body: '',
  });
  if (!response.ok) {
    throw new AuthError(`UserInfo request failed (HTTP ${response.status}).`);
  }
  return (await response.json()) as Record<string, unknown>;
}

/** The decoded (not verified) header and payload of a JWT. */
export type DecodedJwt = {
  readonly header: Record<string, unknown>;
  readonly payload: Record<string, unknown>;
};

function decodeJwtHeader(token: string): Record<string, unknown> {
  const segment = token.split('.')[0];
  if (!segment) {
    throw new AuthError('Malformed JWT: missing header segment.');
  }
  const padded = segment
    .replace(/-/g, '+')
    .replace(/_/g, '/')
    .padEnd(Math.ceil(segment.length / 4) * 4, '=');
  const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
}

/**
 * Decodes (does **not** verify) a token's header and payload for display, or returns `null` when the token is not a
 * JWT (for example, an opaque access token). Signature verification is the resource server's responsibility.
 */
export function inspectJwt(token: string): DecodedJwt | null {
  try {
    return { header: decodeJwtHeader(token), payload: decodeJwtClaims(token) };
  } catch {
    return null;
  }
}

/**
 * Builds the OpenID Connect RP-Initiated Logout URL from the discovered config, or returns `null` when the OpenID
 * Provider advertises no `end_session_endpoint`. Replays the ID token as `id_token_hint` and requests the app origin
 * as the post-logout redirect.
 */
export function endSessionUrl(
  runtime: RuntimeConfig,
  idToken: string | null,
  clientId: string,
  origin: string,
): string | null {
  if (!runtime.endSessionEndpoint) {
    return null;
  }
  return buildEndSessionUrl(runtime.endSessionEndpoint, {
    ...(idToken ? { idTokenHint: idToken } : {}),
    clientId,
    postLogoutRedirectUri: `${origin}/`,
  });
}

/** Re-export so the explorer error handling can narrow on the shared auth error type. */
export { AuthError };
export type { TokenSet };

import {
  deriveCodeChallenge,
  generateCodeVerifier,
  randomUrlSafe,
  type RandomBytes,
} from './pkce.js';
import {
  AuthError,
  buildAuthorizeUrl,
  exchangeCodeForToken,
  parseCallbackParams,
  type FetchLike,
  type TokenSet,
} from './flow.js';

/** The discovered OpenID endpoints the flow drives. */
export type AuthEndpoints = {
  readonly authorizationEndpoint: string;
  readonly tokenEndpoint: string;
};

/** The deployment-provided client coordinates (discovered at runtime, never baked in). */
export type AuthClientConfig = AuthEndpoints & {
  readonly clientId: string;
  readonly redirectUri: string;
  readonly scopes: readonly string[];
};

/** The in-flight authorization transaction persisted across the redirect. */
export type AuthTransaction = {
  readonly state: string;
  readonly codeVerifier: string;
  readonly redirectUri: string;
};

/** Minimal key/value storage (e.g. `sessionStorage`) for the in-flight transaction. */
export type KeyValueStorage = {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
};

/** Injected dependencies, so the orchestration is deterministic under test. */
export type AuthDeps = {
  readonly fetchFn: FetchLike;
  readonly storage: KeyValueStorage;
  readonly randomBytes?: RandomBytes;
  readonly subtle?: SubtleCrypto;
};

const TRANSACTION_KEY = 'ncode.auth.tx';

/** True when the query string carries an authorization-code response (code+state or error). */
export function isAuthorizationCallback(search: string): boolean {
  return parseCallbackParams(search) !== null;
}

/**
 * Starts an authorization-code + PKCE login: generates the verifier/challenge/state, persists the
 * transaction, and returns the `/authorize` URL the caller should navigate to.
 */
export async function beginAuthorization(
  config: AuthClientConfig,
  deps: AuthDeps,
): Promise<string> {
  const codeVerifier = generateCodeVerifier(deps.randomBytes);
  const codeChallenge = await deriveCodeChallenge(codeVerifier, deps.subtle);
  const state = randomUrlSafe(32, deps.randomBytes);

  const transaction: AuthTransaction = {
    state,
    codeVerifier,
    redirectUri: config.redirectUri,
  };
  deps.storage.setItem(TRANSACTION_KEY, JSON.stringify(transaction));

  return buildAuthorizeUrl(config.authorizationEndpoint, {
    clientId: config.clientId,
    redirectUri: config.redirectUri,
    scopes: config.scopes,
    state,
    codeChallenge,
  });
}

/**
 * Completes a login from the redirect query string: validates `state` against the persisted
 * transaction (CSRF guard), then exchanges the code for tokens. Consumes the transaction.
 */
export async function completeAuthorization(
  config: AuthEndpoints & { readonly clientId: string },
  search: string,
  deps: AuthDeps,
): Promise<TokenSet> {
  const result = parseCallbackParams(search);
  if (result === null) {
    throw new AuthError('No authorization response is present in the callback.');
  }
  if ('error' in result) {
    throw new AuthError(result.errorDescription ?? result.error, result.error);
  }

  const raw = deps.storage.getItem(TRANSACTION_KEY);
  if (!raw) {
    throw new AuthError('No in-flight authorization transaction was found.');
  }
  deps.storage.removeItem(TRANSACTION_KEY);

  const transaction = JSON.parse(raw) as AuthTransaction;
  if (transaction.state !== result.state) {
    throw new AuthError('Authorization state mismatch (possible CSRF).');
  }

  return exchangeCodeForToken(
    config.tokenEndpoint,
    {
      clientId: config.clientId,
      code: result.code,
      redirectUri: transaction.redirectUri,
      codeVerifier: transaction.codeVerifier,
    },
    deps.fetchFn,
  );
}

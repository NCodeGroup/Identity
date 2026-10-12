import { buildEndSessionUrl, type AuthClientConfig, type AuthDeps } from '@ncode/auth';
import type { RuntimeConfig } from '@ncode/config';

/** Path, relative to the app origin, the OpenID Provider redirects back to after authorization. */
export const CALLBACK_PATH = '/callback';

/** The redirect URI registered for this SPA: the app origin + the callback path. */
export function redirectUri(origin: string = window.location.origin): string {
  return `${origin}${CALLBACK_PATH}`;
}

/**
 * Builds the auth client config from the discovered runtime config, or returns `null` when no client
 * id is configured — in which case the app runs without an interactive login (e.g. local dev before a
 * SPA client is seeded). UI-free so it is unit-testable.
 */
export function authConfigFromRuntime(
  runtime: RuntimeConfig,
  origin?: string,
): AuthClientConfig | null {
  if (!runtime.clientId) {
    return null;
  }
  return {
    authorizationEndpoint: runtime.authorizationEndpoint,
    tokenEndpoint: runtime.tokenEndpoint,
    clientId: runtime.clientId,
    redirectUri: redirectUri(origin),
    scopes: runtime.scopes,
  };
}

/** The runtime dependencies for the auth flow: global fetch and session-scoped transaction storage. */
export function createAuthDeps(): AuthDeps {
  return {
    fetchFn: (url, init) => fetch(url, init),
    storage: window.sessionStorage,
  };
}

/**
 * Builds the OpenID Connect RP-Initiated Logout URL from the discovered runtime config, or returns
 * `null` when the OpenID Provider does not advertise an `end_session_endpoint` — in which case sign-out
 * can only clear local token state. The ID token (when known) is replayed as `id_token_hint`, and the
 * app origin is requested as the `post_logout_redirect_uri`. UI-free so it is unit-testable.
 */
export function endSessionUrlFromRuntime(
  runtime: RuntimeConfig,
  idToken: string | null,
  origin: string = window.location.origin,
): string | null {
  if (!runtime.endSessionEndpoint) {
    return null;
  }
  return buildEndSessionUrl(runtime.endSessionEndpoint, {
    ...(idToken ? { idTokenHint: idToken } : {}),
    ...(runtime.clientId ? { clientId: runtime.clientId } : {}),
    postLogoutRedirectUri: `${origin}/`,
  });
}

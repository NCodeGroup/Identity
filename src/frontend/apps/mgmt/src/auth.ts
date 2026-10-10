import type { AuthClientConfig, AuthDeps } from '@ncode/auth';
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

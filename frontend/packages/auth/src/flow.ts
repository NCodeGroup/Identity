/** Minimal fetch surface so the token exchange is testable without a DOM/network. */
export type FetchLike = (
  url: string,
  init: {
    readonly method: string;
    readonly headers: Readonly<Record<string, string>>;
    readonly body: string;
  },
) => Promise<{
  readonly ok: boolean;
  readonly status: number;
  json(): Promise<unknown>;
  text(): Promise<string>;
}>;

/** Thrown when an authorization or token request fails. Carries the OAuth error code when available. */
export class AuthError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'AuthError';
  }
}

/** Parameters for the authorization redirect. */
export type AuthRequestParams = {
  readonly clientId: string;
  readonly redirectUri: string;
  readonly scopes: readonly string[];
  readonly state: string;
  readonly codeChallenge: string;
  /** OpenID Connect nonce, bound into the issued ID token to mitigate replay. Emitted only when present. */
  readonly nonce?: string;
  /** OpenID Connect `prompt` (for example, `consent` or `login`). Emitted only when present. */
  readonly prompt?: string;
};

/** Builds the `/authorize` redirect URL for an authorization-code + PKCE (S256) request. */
export function buildAuthorizeUrl(
  authorizationEndpoint: string,
  params: AuthRequestParams,
): string {
  const url = new URL(authorizationEndpoint);
  url.searchParams.set('response_type', 'code');
  url.searchParams.set('client_id', params.clientId);
  url.searchParams.set('redirect_uri', params.redirectUri);
  url.searchParams.set('scope', params.scopes.join(' '));
  url.searchParams.set('state', params.state);
  url.searchParams.set('code_challenge', params.codeChallenge);
  url.searchParams.set('code_challenge_method', 'S256');
  if (params.nonce) {
    url.searchParams.set('nonce', params.nonce);
  }
  if (params.prompt) {
    url.searchParams.set('prompt', params.prompt);
  }
  return url.toString();
}

/** Parameters for the OpenID Connect RP-Initiated Logout redirect to the `end_session_endpoint`. */
export type EndSessionParams = {
  /** An ID token previously issued to the client; identifies the session and requesting client. */
  readonly idTokenHint?: string;
  /** The client id requesting the logout; lets the OP validate the post-logout redirect. */
  readonly clientId?: string;
  /** Where the OP returns the user agent after logout; honored only when registered for the client. */
  readonly postLogoutRedirectUri?: string;
  /** Opaque value echoed back on the post-logout redirect. */
  readonly state?: string;
};

/**
 * Builds the `end_session_endpoint` redirect URL for OpenID Connect RP-Initiated Logout. Only the
 * parameters that are present are emitted, so a caller may rely on the OP login cookie alone.
 */
export function buildEndSessionUrl(endSessionEndpoint: string, params: EndSessionParams): string {
  const url = new URL(endSessionEndpoint);
  if (params.idTokenHint) {
    url.searchParams.set('id_token_hint', params.idTokenHint);
  }
  if (params.clientId) {
    url.searchParams.set('client_id', params.clientId);
  }
  if (params.postLogoutRedirectUri) {
    url.searchParams.set('post_logout_redirect_uri', params.postLogoutRedirectUri);
  }
  if (params.state) {
    url.searchParams.set('state', params.state);
  }
  return url.toString();
}

/** A successful authorization-code callback. */
export type CallbackSuccess = { readonly code: string; readonly state: string };
/** An error returned on the authorization callback. */
export type CallbackError = {
  readonly error: string;
  readonly errorDescription?: string;
  readonly state?: string;
};

/**
 * Parses an authorization-redirect query string. Returns the code/state on success, an error object
 * when the server returned `error`, or `null` when the query is not an authorization callback.
 */
export function parseCallbackParams(search: string): CallbackSuccess | CallbackError | null {
  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);
  const error = params.get('error');
  if (error) {
    const description = params.get('error_description');
    const state = params.get('state');
    return {
      error,
      ...(description ? { errorDescription: description } : {}),
      ...(state ? { state } : {}),
    };
  }
  const code = params.get('code');
  const state = params.get('state');
  if (code && state) {
    return { code, state };
  }
  return null;
}

/** The normalized token response. */
export type TokenSet = {
  readonly accessToken: string;
  readonly tokenType: string;
  readonly expiresInSeconds?: number;
  readonly refreshToken?: string;
  readonly idToken?: string;
  readonly scope?: string;
};

async function postToken(
  tokenEndpoint: string,
  body: URLSearchParams,
  fetchFn: FetchLike,
): Promise<TokenSet> {
  const response = await fetchFn(tokenEndpoint, {
    method: 'POST',
    headers: {
      'content-type': 'application/x-www-form-urlencoded',
      accept: 'application/json',
    },
    body: body.toString(),
  });

  if (!response.ok) {
    let code: string | undefined;
    let description: string | undefined;
    try {
      const payload = (await response.json()) as Record<string, unknown>;
      code = typeof payload.error === 'string' ? payload.error : undefined;
      description =
        typeof payload.error_description === 'string' ? payload.error_description : undefined;
    } catch {
      // Non-JSON error body; fall back to the status.
    }
    throw new AuthError(description ?? `Token request failed (HTTP ${response.status}).`, code);
  }

  const payload = (await response.json()) as Record<string, unknown>;
  if (typeof payload.access_token !== 'string') {
    throw new AuthError('Token response did not include an access_token.');
  }
  return {
    accessToken: payload.access_token,
    tokenType: typeof payload.token_type === 'string' ? payload.token_type : 'Bearer',
    ...(typeof payload.expires_in === 'number' ? { expiresInSeconds: payload.expires_in } : {}),
    ...(typeof payload.refresh_token === 'string' ? { refreshToken: payload.refresh_token } : {}),
    ...(typeof payload.id_token === 'string' ? { idToken: payload.id_token } : {}),
    ...(typeof payload.scope === 'string' ? { scope: payload.scope } : {}),
  };
}

/** Exchanges an authorization code for tokens (public client; PKCE, no client secret). */
export function exchangeCodeForToken(
  tokenEndpoint: string,
  params: {
    readonly clientId: string;
    readonly code: string;
    readonly redirectUri: string;
    readonly codeVerifier: string;
  },
  fetchFn: FetchLike,
): Promise<TokenSet> {
  const body = new URLSearchParams({
    grant_type: 'authorization_code',
    client_id: params.clientId,
    code: params.code,
    redirect_uri: params.redirectUri,
    code_verifier: params.codeVerifier,
  });
  return postToken(tokenEndpoint, body, fetchFn);
}

/** Exchanges a refresh token for a new token set (public client). */
export function refreshAccessToken(
  tokenEndpoint: string,
  params: { readonly clientId: string; readonly refreshToken: string },
  fetchFn: FetchLike,
): Promise<TokenSet> {
  const body = new URLSearchParams({
    grant_type: 'refresh_token',
    client_id: params.clientId,
    refresh_token: params.refreshToken,
  });
  return postToken(tokenEndpoint, body, fetchFn);
}

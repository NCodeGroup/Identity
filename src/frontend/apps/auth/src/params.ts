/**
 * UI-free helpers for the interactive login/consent surface: reading the return URL, the antiforgery
 * request token (echoed from a cookie the host sets), the error code, and which page to render. Every
 * path is relative to the surface: the host serves the bundle beneath the tenant's route and sets the
 * document's base to the surface there (`<base href="/{tenant}/account/">`), so the bundle resolves its
 * paths against `document.baseURI` and never assumes where it is mounted (ADR-0193). The helpers take
 * the base as a parameter, defaulting to the document's, so they are unit-testable.
 */

/** The server-set, JavaScript-readable cookie whose value is echoed back as the antiforgery token. */
export const ANTIFORGERY_COOKIE = 'XSRF-TOKEN';

/** The antiforgery form field the ASP.NET Core antiforgery middleware validates on POST. */
export const ANTIFORGERY_FIELD = '__RequestVerificationToken';

/** The surface path the login form posts credentials to. */
export const LOGIN_ACTION = 'login';

/** The surface path the consent form posts its outcome to. */
export const CONSENT_ACTION = 'consent';

/** The surface JSON endpoint returning the pending authorization request the consent page presents. */
export const CONSENT_DETAILS_ENDPOINT = 'consent/details';

/** The surface path the account page's sign-out form posts to (first-party RP-Initiated Logout). */
export const LOGOUT_ACTION = 'logout';

/** The account/session ("me") page, served at the interactive surface root. */
export const ACCOUNT_PATH = './';

/** The surface JSON endpoint returning the current end-user's session for the account page. */
export const ME_ENDPOINT = 'me';

/** The surface JSON endpoint returning the tenant's branding, which the bundle themes itself with before it renders. */
export const BRANDING_ENDPOINT = 'branding';

/** The self-service "authorized applications" page, where the end-user reviews and revokes granted access. */
export const APPLICATIONS_PATH = 'applications';

/** The surface JSON endpoint returning the end-user's authorized applications. */
export const APPLICATIONS_LIST_ENDPOINT = 'applications/list';

/** The surface path the authorized-applications page posts to in order to revoke a single application. */
export const APPLICATIONS_REVOKE_ACTION = 'applications/revoke';

/** The self-service "active sessions" page, where the end-user reviews and signs out of their sessions. */
export const SESSIONS_PATH = 'sessions';

/** The surface JSON endpoint returning the end-user's active sessions. */
export const SESSIONS_LIST_ENDPOINT = 'sessions/list';

/** The surface path the active-sessions page posts to in order to revoke a single session by its `sid`. */
export const SESSIONS_REVOKE_ACTION = 'sessions/revoke';

/** The surface path the active-sessions page posts to in order to revoke every session (optionally the others only). */
export const SESSIONS_REVOKE_ALL_ACTION = 'sessions/revoke-all';

/** The path that renders the consent page (every other path renders login). */
export const CONSENT_PATH = 'consent';

/**
 * Resolves a surface path to the same-origin absolute path the host serves it at, for example `me` to
 * `/acme/account/me` beneath the `acme` tenant.
 */
export function resolveSurfacePath(
  surfacePath: string,
  baseUri: string = document.baseURI,
): string {
  return new URL(surfacePath, baseUri).pathname;
}

/** True when the given absolute path is the given surface path, ignoring a trailing slash. */
function isSurfacePath(pathname: string, surfacePath: string, baseUri: string): boolean {
  const trim = (path: string) => path.replace(/\/+$/, '');
  return trim(pathname) === trim(resolveSurfacePath(surfacePath, baseUri));
}

/** Reads a single query-string parameter, returning `null` when absent. */
export function getQueryParam(search: string, name: string): string | null {
  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);
  return params.get(name);
}

/** Reads a cookie value from a `document.cookie`-style string, returning `null` when absent. */
export function readCookie(cookieString: string, name: string): string | null {
  const prefix = `${name}=`;
  for (const part of cookieString.split(';')) {
    const trimmed = part.trim();
    if (trimmed.startsWith(prefix)) {
      return decodeURIComponent(trimmed.slice(prefix.length));
    }
  }
  return null;
}

/** True when the given path should render the consent page rather than the login page. */
export function isConsentPath(pathname: string, baseUri: string = document.baseURI): boolean {
  return isSurfacePath(pathname, CONSENT_PATH, baseUri);
}

/** True when the given path should render the account/session ("me") page. */
export function isAccountPath(pathname: string, baseUri: string = document.baseURI): boolean {
  return isSurfacePath(pathname, ACCOUNT_PATH, baseUri);
}

/** True when the given path should render the authorized-applications page. */
export function isApplicationsPath(pathname: string, baseUri: string = document.baseURI): boolean {
  return isSurfacePath(pathname, APPLICATIONS_PATH, baseUri);
}

/** True when the given path should render the active-sessions page. */
export function isSessionsPath(pathname: string, baseUri: string = document.baseURI): boolean {
  return isSurfacePath(pathname, SESSIONS_PATH, baseUri);
}

/** Maps a login error code to a human-readable message. */
export function describeLoginError(error: string | null): string | null {
  switch (error) {
    case null:
      return null;
    case 'invalid_credentials':
      return 'The username or password is incorrect.';
    case 'invalid_request':
      return 'Enter both a username and a password.';
    default:
      return 'Sign-in failed. Please try again.';
  }
}

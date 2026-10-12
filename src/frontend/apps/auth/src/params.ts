/**
 * UI-free helpers for the interactive login/consent surface: reading the return URL, the antiforgery
 * request token (echoed from a cookie the host sets), the error code, and which page to render. Kept
 * free of the DOM so they are unit-testable.
 */

/** The server-set, JavaScript-readable cookie whose value is echoed back as the antiforgery token. */
export const ANTIFORGERY_COOKIE = 'XSRF-TOKEN';

/** The antiforgery form field the ASP.NET Core antiforgery middleware validates on POST. */
export const ANTIFORGERY_FIELD = '__RequestVerificationToken';

/** The same-origin path the login form posts credentials to. */
export const LOGIN_ACTION = '/account/login';

/** The same-origin path the consent form posts its outcome to. */
export const CONSENT_ACTION = '/account/consent';

/** The same-origin JSON endpoint returning the pending authorization request the consent page presents. */
export const CONSENT_DETAILS_ENDPOINT = '/account/consent/details';

/** The same-origin path the account page's sign-out form posts to (first-party RP-Initiated Logout). */
export const LOGOUT_ACTION = '/account/logout';

/** The account/session ("me") page, served at the interactive surface root. */
export const ACCOUNT_PATH = '/account';

/** The same-origin JSON endpoint returning the current end-user's session for the account page. */
export const ME_ENDPOINT = '/account/me';

/** The path that renders the consent page (every other path renders login). */
export const CONSENT_PATH = '/account/consent';

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
export function isConsentPath(pathname: string): boolean {
  return pathname.replace(/\/+$/, '') === CONSENT_PATH;
}

/** True when the given path should render the account/session ("me") page. */
export function isAccountPath(pathname: string): boolean {
  return pathname.replace(/\/+$/, '') === ACCOUNT_PATH;
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

import { ConsentForm } from './ConsentForm.js';
import { LoginForm } from './LoginForm.js';
import {
  ANTIFORGERY_COOKIE,
  describeLoginError,
  getQueryParam,
  isConsentPath,
  readCookie,
} from './params.js';

export type AppProps = {
  /** The current path (defaults to the live location); selects the login vs consent surface. */
  readonly pathname?: string;
  /** The current query string (defaults to the live location). */
  readonly search?: string;
  /** The current `document.cookie` string (defaults to the live document). */
  readonly cookie?: string;
};

/**
 * The interactive surface shell: it reads the return URL, the antiforgery token, and the error code from
 * the environment and renders either the consent or the login form. All inputs are injectable so it is
 * testable without the DOM globals.
 */
export function App({
  pathname = window.location.pathname,
  search = window.location.search,
  cookie = document.cookie,
}: AppProps) {
  const returnUrl = getQueryParam(search, 'ReturnUrl') ?? '/';
  const antiforgeryToken = readCookie(cookie, ANTIFORGERY_COOKIE) ?? '';

  if (isConsentPath(pathname)) {
    return <ConsentForm returnUrl={returnUrl} antiforgeryToken={antiforgeryToken} />;
  }

  const errorMessage = describeLoginError(getQueryParam(search, 'error'));
  return (
    <LoginForm
      returnUrl={returnUrl}
      antiforgeryToken={antiforgeryToken}
      {...(errorMessage ? { errorMessage } : {})}
    />
  );
}

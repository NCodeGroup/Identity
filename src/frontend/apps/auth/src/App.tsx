import type { BrandingConfig } from '@ncode/config';
import { BrandHeader } from './BrandHeader.js';
import { EnvironmentBanner } from './EnvironmentBanner.js';
import { ANTIFORGERY_COOKIE, describeLoginError, getQueryParam, readCookie } from './params.js';
import { resolveRoute } from './routes.js';
import { EMPTY_SERVER_PAGE, type ServerPage } from './serverPage.js';

export type AppProps = {
  /** The current path (defaults to the live location); selects which interactive surface to render. */
  readonly pathname?: string;
  /** The current query string (defaults to the live location). */
  readonly search?: string;
  /** The current `document.cookie` string (defaults to the live document). */
  readonly cookie?: string;
  /** The tenant's branding the host embeds in the page, or `undefined` for the default presentation. */
  readonly branding?: BrandingConfig;
  /** The state of the page the host rendered before the bundle mounted (an empty page by default). */
  readonly serverPage?: ServerPage;
};

/**
 * The interactive surface shell: it reads the return URL, the antiforgery token, and the error code from
 * the environment, then delegates to the declarative route table to render the matching surface. All
 * inputs are injectable so it is testable without the DOM globals.
 */
export function App({
  pathname = window.location.pathname,
  search = window.location.search,
  cookie = document.cookie,
  branding,
  serverPage = EMPTY_SERVER_PAGE,
}: AppProps) {
  return (
    <>
      {branding?.environment ? <EnvironmentBanner environment={branding.environment} /> : null}
      {branding ? <BrandHeader branding={branding} /> : null}
      {resolveRoute({
        pathname,
        returnUrl: getQueryParam(search, 'ReturnUrl') ?? '/',
        antiforgeryToken: readCookie(cookie, ANTIFORGERY_COOKIE) ?? '',
        errorMessage: describeLoginError(getQueryParam(search, 'error')),
        errorCode: getQueryParam(search, 'error'),
        status: getQueryParam(search, 'status'),
        serverPage,
        ...(branding ? { branding } : {}),
      })}
    </>
  );
}

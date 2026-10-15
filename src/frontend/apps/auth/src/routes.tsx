import type { ReactElement } from 'react';
import { ConsentForm } from './ConsentForm.js';
import { LoginForm } from './LoginForm.js';
import { AccountPage } from './AccountPage.js';
import { AuthorizedAppsPage } from './AuthorizedAppsPage.js';
import { SessionsPage } from './SessionsPage.js';
import { isAccountPath, isApplicationsPath, isConsentPath, isSessionsPath } from './params.js';

/** The per-request inputs the interactive surfaces render against, derived from the environment. */
export type RouteContext = {
  readonly pathname: string;
  readonly returnUrl: string;
  readonly antiforgeryToken: string;
  readonly errorMessage?: string | null;
};

type Route = {
  readonly match: (pathname: string) => boolean;
  readonly render: (context: RouteContext) => ReactElement;
};

/**
 * The declarative surface table, resolved top-to-bottom. Adding a surface is a single entry; the login
 * surface is the fallback, since the OP's challenge redirects to a surface path and any unmatched path
 * should still present a login.
 */
const ROUTES: readonly Route[] = [
  {
    match: isSessionsPath,
    render: ({ antiforgeryToken }) => <SessionsPage antiforgeryToken={antiforgeryToken} />,
  },
  {
    match: isApplicationsPath,
    render: ({ antiforgeryToken }) => <AuthorizedAppsPage antiforgeryToken={antiforgeryToken} />,
  },
  {
    match: isAccountPath,
    render: ({ antiforgeryToken }) => <AccountPage antiforgeryToken={antiforgeryToken} />,
  },
  {
    match: isConsentPath,
    render: ({ returnUrl, antiforgeryToken }) => (
      <ConsentForm returnUrl={returnUrl} antiforgeryToken={antiforgeryToken} />
    ),
  },
];

function renderLogin({ returnUrl, antiforgeryToken, errorMessage }: RouteContext): ReactElement {
  return (
    <LoginForm
      returnUrl={returnUrl}
      antiforgeryToken={antiforgeryToken}
      {...(errorMessage ? { errorMessage } : {})}
    />
  );
}

/** Resolves the interactive surface for the given request context (login is the fallback). */
export function resolveRoute(context: RouteContext): ReactElement {
  const route = ROUTES.find((candidate) => candidate.match(context.pathname));
  return route ? route.render(context) : renderLogin(context);
}

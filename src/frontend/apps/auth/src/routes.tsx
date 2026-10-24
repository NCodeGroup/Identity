import type { ReactElement } from 'react';
import type { BrandingConfig } from '@ncode/config';
import { AuthorizationErrorPage } from './AuthorizationErrorPage.js';
import { ConsentForm } from './ConsentForm.js';
import { LoginForm } from './LoginForm.js';
import { AccountPage } from './AccountPage.js';
import { AuthorizedAppsPage } from './AuthorizedAppsPage.js';
import { PasswordPage } from './PasswordPage.js';
import { SessionsPage } from './SessionsPage.js';
import {
  describePasswordError,
  isAccountPath,
  isApplicationsPath,
  isConsentPath,
  isPasswordPath,
  isSessionsPath,
} from './params.js';
import { EMPTY_SERVER_PAGE, type ServerPage } from './serverPage.js';

/** The per-request inputs the interactive surfaces render against, derived from the environment. */
export type RouteContext = {
  readonly pathname: string;
  readonly returnUrl: string;
  readonly antiforgeryToken: string;
  readonly errorMessage?: string | null;
  /** The raw `error` query code, which each surface maps to its own message. */
  readonly errorCode?: string | null;
  /** The `status` query value a surface reports a completed operation with. */
  readonly status?: string | null;
  /** The tenant's branding; its display name titles the surfaces that present the tenant. */
  readonly branding?: BrandingConfig;
  /** The state of the page the host rendered: typed values, focus, cleared scopes and any authorization error. */
  readonly serverPage?: ServerPage;
};

type Route = {
  readonly match: (pathname: string) => boolean;
  readonly render: (context: RouteContext) => ReactElement;
};

/**
 * The tenant's display name as the product name of a surface that presents the tenant. The consent page names the
 * requesting application instead, so it never receives it.
 */
function productNameOf(branding: BrandingConfig | undefined): { productName?: string } {
  return branding?.displayName ? { productName: branding.displayName } : {};
}

/**
 * The declarative surface table, resolved top-to-bottom. Adding a surface is a single entry; the login
 * surface is the fallback, since the OP's challenge redirects to a surface path and any unmatched path
 * should still present a login.
 */
const ROUTES: readonly Route[] = [
  {
    match: isPasswordPath,
    render: ({ antiforgeryToken, branding, errorCode, status }) => (
      <PasswordPage
        antiforgeryToken={antiforgeryToken}
        errorMessage={describePasswordError(errorCode ?? null)}
        changed={status === 'changed'}
        {...productNameOf(branding)}
      />
    ),
  },
  {
    match: isSessionsPath,
    render: ({ antiforgeryToken, branding }) => (
      <SessionsPage antiforgeryToken={antiforgeryToken} {...productNameOf(branding)} />
    ),
  },
  {
    match: isApplicationsPath,
    render: ({ antiforgeryToken, branding }) => (
      <AuthorizedAppsPage antiforgeryToken={antiforgeryToken} {...productNameOf(branding)} />
    ),
  },
  {
    match: isAccountPath,
    render: ({ antiforgeryToken, branding }) => (
      <AccountPage antiforgeryToken={antiforgeryToken} {...productNameOf(branding)} />
    ),
  },
  {
    match: isConsentPath,
    render: ({ returnUrl, antiforgeryToken, serverPage = EMPTY_SERVER_PAGE }) => (
      <ConsentForm
        returnUrl={returnUrl}
        antiforgeryToken={antiforgeryToken}
        clearedScopes={serverPage.clearedScopes}
      />
    ),
  },
];

function renderLogin({
  returnUrl,
  antiforgeryToken,
  errorMessage,
  branding,
  serverPage = EMPTY_SERVER_PAGE,
}: RouteContext): ReactElement {
  return (
    <LoginForm
      returnUrl={returnUrl}
      antiforgeryToken={antiforgeryToken}
      {...(errorMessage ? { errorMessage } : {})}
      {...productNameOf(branding)}
      carriedValues={serverPage.fieldValues}
      focusedFieldId={serverPage.focusedFieldId}
    />
  );
}

/** Resolves the interactive surface for the given request context (login is the fallback). */
export function resolveRoute(context: RouteContext): ReactElement {
  // The error page is served at the protocol endpoint, outside the surface's paths, so the page the host rendered
  // selects it, not the path (ADR-0253).
  const authorizationError = context.serverPage?.authorizationError;
  if (authorizationError) {
    return (
      <AuthorizationErrorPage
        code={authorizationError.code}
        description={authorizationError.description}
      />
    );
  }

  const route = ROUTES.find((candidate) => candidate.match(context.pathname));
  return route ? route.render(context) : renderLogin(context);
}

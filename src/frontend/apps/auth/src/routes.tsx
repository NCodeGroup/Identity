import type { ReactElement } from 'react';
import type { BrandingConfig } from '@ncode/config';
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
    render: ({ returnUrl, antiforgeryToken }) => (
      <ConsentForm returnUrl={returnUrl} antiforgeryToken={antiforgeryToken} />
    ),
  },
];

function renderLogin({
  returnUrl,
  antiforgeryToken,
  errorMessage,
  branding,
}: RouteContext): ReactElement {
  return (
    <LoginForm
      returnUrl={returnUrl}
      antiforgeryToken={antiforgeryToken}
      {...(errorMessage ? { errorMessage } : {})}
      {...productNameOf(branding)}
    />
  );
}

/** Resolves the interactive surface for the given request context (login is the fallback). */
export function resolveRoute(context: RouteContext): ReactElement {
  const route = ROUTES.find((candidate) => candidate.match(context.pathname));
  return route ? route.render(context) : renderLogin(context);
}

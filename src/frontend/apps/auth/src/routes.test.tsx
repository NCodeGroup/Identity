import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { resolveRoute, type RouteContext } from './routes.js';
import { EMPTY_SERVER_PAGE } from './serverPage.js';

const baseContext: RouteContext = {
  pathname: '/account/login',
  returnUrl: '/authorize',
  antiforgeryToken: 'tok',
};

function renderRoute(context: RouteContext) {
  return render(<AppThemeProvider>{resolveRoute(context)}</AppThemeProvider>);
}

describe('resolveRoute', () => {
  // The data-loading surfaces use their default same-origin loaders; a request that never settles keeps them in their
  // initial state and the test hermetic, while still recording which endpoint each surface reads.
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn(() => new Promise<Response>(() => {}));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders the account surface for the account path', async () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account' });
    expect(container.querySelector('#logout')).not.toBeNull();
    expect(screen.getByTestId('account-loading')).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/account/me', expect.anything()));
  });

  it('renders the consent surface for the consent path', async () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account/consent' });
    expect(container.querySelector('#consent-allow')).not.toBeNull();
    expect(
      (container.querySelector('input[name="ReturnUrl"]') as HTMLInputElement | null)?.value,
    ).toBe('/authorize');
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/account/consent/details?ReturnUrl=%2Fauthorize',
        expect.anything(),
      ),
    );
  });

  it('renders the authorized-applications surface for the applications path', async () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account/applications' });
    expect(container.querySelector('[data-testid="applications-loading"]')).not.toBeNull();
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/account/applications/list', expect.anything()),
    );
  });

  it('renders the active-sessions surface for the sessions path', async () => {
    renderRoute({ ...baseContext, pathname: '/account/sessions' });
    expect(screen.getByTestId('sessions-loading')).toBeInTheDocument();
    expect(screen.getByTestId('sessions-revoke-all')).toBeInTheDocument();
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/account/sessions/list', expect.anything()),
    );
  });

  it('renders the change-password surface for the password path', () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account/password' });
    expect(container.querySelector('#change-password')).not.toBeNull();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByTestId('password-changed')).not.toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('maps the password error code and status', () => {
    renderRoute({
      ...baseContext,
      pathname: '/account/password',
      errorCode: 'invalid_current_password',
      status: 'changed',
    });
    expect(screen.getByText('The current password is incorrect.')).toBeInTheDocument();
    expect(screen.getByTestId('password-changed')).toBeInTheDocument();
  });

  it.each([
    ['/account/', 'account-loading'],
    ['/account/applications/', 'applications-loading'],
    ['/account/sessions//', 'sessions-loading'],
  ])('tolerates a trailing slash on %s', (pathname, testId) => {
    renderRoute({ ...baseContext, pathname });
    expect(screen.getByTestId(testId)).toBeInTheDocument();
  });

  it('renders the login surface for the login path, carrying the return URL and antiforgery token', () => {
    const { container } = renderRoute(baseContext);
    expect(container.querySelector('#login')).not.toBeNull();
    expect(
      (container.querySelector('input[name="ReturnUrl"]') as HTMLInputElement | null)?.value,
    ).toBe('/authorize');
    expect(
      (
        container.querySelector(
          'input[name="__RequestVerificationToken"]',
        ) as HTMLInputElement | null
      )?.value,
    ).toBe('tok');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('passes the login error message through to the login surface', () => {
    renderRoute({ ...baseContext, errorMessage: 'The username or password is incorrect.' });
    expect(screen.getByRole('alert')).toHaveTextContent('The username or password is incorrect.');
  });

  it('omits the login error alert when the error message is null', () => {
    const { container } = renderRoute({ ...baseContext, errorMessage: null });
    expect(container.querySelector('#login')).not.toBeNull();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('falls back to the login surface for any unmatched path', () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/anything/else' });
    expect(container.querySelector('#login')).not.toBeNull();
  });

  it('does not treat a sub-path of a surface as that surface', () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account/sessions/other' });
    expect(container.querySelector('#login')).not.toBeNull();
    expect(screen.queryByTestId('sessions-loading')).not.toBeInTheDocument();
  });

  it.each([
    ['/account/login', 'Sign in to Acme'],
    ['/account', 'Acme account'],
    ['/account/applications', 'Acme — authorized applications'],
    ['/account/sessions', 'Acme — active sessions'],
    ['/account/password', 'Acme — change password'],
  ])('titles %s with the tenant display name', (pathname, title) => {
    renderRoute({ ...baseContext, pathname, branding: { displayName: 'Acme' } });
    expect(screen.getByText(title)).toBeInTheDocument();
  });

  it('names the requesting application, not the tenant, on the consent surface', () => {
    renderRoute({
      ...baseContext,
      pathname: '/account/consent',
      branding: { displayName: 'Acme' },
    });
    expect(screen.queryByText(/Acme/)).toBeNull();
  });

  it('keeps the default titles when the branding has no display name', () => {
    renderRoute({ ...baseContext, branding: { primaryColor: '#1565c0' } });
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('redraws the authorization error the server page reports, whatever the path', () => {
    const { container } = renderRoute({
      ...baseContext,
      pathname: '/oauth2/authorize',
      serverPage: {
        ...EMPTY_SERVER_PAGE,
        authorizationError: { code: 'invalid_client', description: null },
      },
    });

    expect(container.querySelector('#authorization-error')).not.toBeNull();
    expect(container.querySelector('#username')).toBeNull();
  });

  it("carries the server page's typed username into the login surface", () => {
    const { container } = renderRoute({
      ...baseContext,
      serverPage: { ...EMPTY_SERVER_PAGE, fieldValues: { username: 'alice' } },
    });

    expect(container.querySelector<HTMLInputElement>('#username')?.value).toBe('alice');
  });
});

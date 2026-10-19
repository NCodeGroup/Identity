import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { resolveRoute, type RouteContext } from './routes.js';

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
});

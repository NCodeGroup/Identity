import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { AccountPage, type AccountSession } from './AccountPage.js';

function renderPage(props: Parameters<typeof AccountPage>[0]) {
  return render(
    <AppThemeProvider>
      <AccountPage {...props} />
    </AppThemeProvider>,
  );
}

function deferred<T>() {
  let resolve: (value: T) => void = () => {};
  const promise = new Promise<T>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

describe('AccountPage', () => {
  it('renders the loaded session details and a sign-out form carrying the antiforgery token', async () => {
    const session: AccountSession = {
      subject: 'user-123',
      name: 'Ada Lovelace',
      authTime: 1700000000,
      amr: ['pwd', 'mfa'],
      idp: 'local',
      sessionId: 'session-abc',
    };
    const { container } = renderPage({
      antiforgeryToken: 'the-token',
      loadSession: () => Promise.resolve(session),
    });

    expect(await screen.findByTestId('account-session')).toBeInTheDocument();
    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByTestId('account-subject')).toHaveTextContent('user-123');
    expect(screen.getByTestId('account-auth-time')).toHaveTextContent(
      new Date(1700000000 * 1000).toLocaleString(),
    );
    expect(screen.getByTestId('account-amr')).toHaveTextContent('pwd, mfa');
    expect(screen.getByTestId('account-idp')).toHaveTextContent('local');
    expect(screen.getByTestId('account-sid')).toHaveTextContent('session-abc');
    expect(screen.queryByTestId('account-loading')).not.toBeInTheDocument();

    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/account/logout');
    expect(form?.getAttribute('method')).toBe('post');
    const token = container.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.value).toBe('the-token');
    const returnUrl = container.querySelector('input[name="ReturnUrl"]') as HTMLInputElement | null;
    expect(returnUrl?.value).toBe('/account/login');
    expect(screen.getByRole('button', { name: 'Sign out' })).toHaveAttribute('value', 'logout');
  });

  it('renders only the subject when the session carries no optional details', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSession: () =>
        Promise.resolve({
          subject: 'user-123',
          name: null,
          authTime: null,
          amr: [],
          idp: null,
          sessionId: null,
        }),
    });

    expect(await screen.findByTestId('account-session')).toHaveTextContent('Signed in as user-123');
    expect(screen.getByTestId('account-subject')).toHaveTextContent('user-123');
    expect(screen.queryByTestId('account-auth-time')).not.toBeInTheDocument();
    expect(screen.queryByTestId('account-amr')).not.toBeInTheDocument();
    expect(screen.queryByTestId('account-idp')).not.toBeInTheDocument();
    expect(screen.queryByTestId('account-sid')).not.toBeInTheDocument();
  });

  it('titles the page with the product name when one is provided', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      productName: 'Contoso',
      loadSession: () => Promise.resolve({ subject: 'user-123' }),
    });

    expect(await screen.findByTestId('account-session')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Contoso account');
  });

  it('titles the page generically and links to the active-sessions page', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSession: () => Promise.resolve({ subject: 'user-123' }),
    });

    expect(await screen.findByTestId('account-session')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Your account');
    expect(screen.getByRole('link', { name: 'Manage active sessions' })).toHaveAttribute(
      'href',
      '/account/sessions',
    );
  });

  it('shows a loading state until the session resolves, and keeps sign-out available', () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSession: () => new Promise<AccountSession | null>(() => {}),
    });

    expect(screen.getByTestId('account-loading')).toBeInTheDocument();
    expect(document.querySelector('#logout')).not.toBeNull();
  });

  it('stays in the loading state, with sign-out available, when the loader fails', async () => {
    const loadSession = vi.fn(() => Promise.reject(new Error('network down')));
    renderPage({ antiforgeryToken: 'the-token', loadSession });

    await waitFor(() => expect(loadSession).toHaveBeenCalledTimes(1));
    await act(async () => {});
    expect(screen.getByTestId('account-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('account-session')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeEnabled();
  });

  it('ignores a stale load when the loader changes before it resolves', async () => {
    const stale = deferred<AccountSession | null>();
    const { rerender } = renderPage({
      antiforgeryToken: 'the-token',
      loadSession: () => stale.promise,
    });
    rerender(
      <AppThemeProvider>
        <AccountPage
          antiforgeryToken="the-token"
          loadSession={() => Promise.resolve({ subject: 'fresh-user' })}
        />
      </AppThemeProvider>,
    );

    expect(await screen.findByTestId('account-subject')).toHaveTextContent('fresh-user');
    await act(async () => {
      stale.resolve({ subject: 'stale-user' });
    });

    expect(screen.getByTestId('account-subject')).toHaveTextContent('fresh-user');
    expect(screen.queryByText('stale-user')).not.toBeInTheDocument();
  });

  describe('default loader', () => {
    const originalLocation = window.location;
    let assignMock: ReturnType<typeof vi.fn>;

    beforeEach(() => {
      assignMock = vi.fn();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: { origin: 'https://op.test', href: 'https://op.test/account', assign: assignMock },
      });
    });

    afterEach(() => {
      vi.unstubAllGlobals();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation,
      });
    });

    it('fetches the me endpoint and renders the returned session', async () => {
      const fetchMock = vi.fn(() =>
        Promise.resolve(
          new Response(JSON.stringify({ subject: 'user-123', name: 'Ada Lovelace' }), {
            status: 200,
            headers: { 'content-type': 'application/json' },
          }),
        ),
      );
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
      expect(screen.getByTestId('account-subject')).toHaveTextContent('user-123');
      // The payload omits the optional `amr`, which the page treats like an empty list.
      expect(screen.queryByTestId('account-amr')).not.toBeInTheDocument();
      expect(fetchMock).toHaveBeenCalledWith('/account/me', {
        headers: { accept: 'application/json' },
      });
      expect(assignMock).not.toHaveBeenCalled();
    });

    it('redirects to login on 401', async () => {
      const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(assignMock).toHaveBeenCalledTimes(1));
      expect(fetchMock).toHaveBeenCalledWith('/account/me', expect.anything());
      expect(assignMock.mock.calls[0]![0]).toBe('/account/login?ReturnUrl=%2Faccount');
      expect(screen.getByTestId('account-loading')).toBeInTheDocument();
    });

    it('stays in the loading state without redirecting on any other failure', async () => {
      const response = new Response('boom', { status: 500 });
      const json = vi.spyOn(response, 'json');
      const fetchMock = vi.fn(() => Promise.resolve(response));
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
      await act(async () => {});
      expect(json).not.toHaveBeenCalled();
      expect(assignMock).not.toHaveBeenCalled();
      expect(screen.getByTestId('account-loading')).toBeInTheDocument();
    });

    it('stays in the loading state when fetch is unavailable', async () => {
      vi.stubGlobal('fetch', undefined);

      renderPage({ antiforgeryToken: 'the-token' });
      await act(async () => {});

      expect(screen.getByTestId('account-loading')).toBeInTheDocument();
      expect(assignMock).not.toHaveBeenCalled();
    });
  });
});

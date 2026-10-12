import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { AccountPage, type AccountSession } from './AccountPage.js';

function renderPage(props: Parameters<typeof AccountPage>[0]) {
  return render(
    <AppThemeProvider>
      <AccountPage {...props} />
    </AppThemeProvider>,
  );
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
    expect(screen.getByTestId('account-amr')).toHaveTextContent('pwd, mfa');
    expect(screen.getByTestId('account-idp')).toHaveTextContent('local');
    expect(screen.getByTestId('account-sid')).toHaveTextContent('session-abc');

    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/account/logout');
    expect(form?.getAttribute('method')).toBe('post');
    const token = container.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.value).toBe('the-token');
  });

  it('shows a loading state until the session resolves, and keeps sign-out available', () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSession: () => new Promise<AccountSession | null>(() => {}),
    });

    expect(screen.getByTestId('account-loading')).toBeInTheDocument();
    expect(document.querySelector('#logout')).not.toBeNull();
  });

  it('fetches the me endpoint and redirects to login on 401', async () => {
    const assignMock = vi.fn();
    const originalLocation = window.location;
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { origin: 'https://op.test', href: 'https://op.test/account', assign: assignMock },
    });
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
    vi.stubGlobal('fetch', fetchMock);

    try {
      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(assignMock).toHaveBeenCalledTimes(1));
      expect(fetchMock).toHaveBeenCalledWith('/account/me', expect.anything());
      expect(assignMock.mock.calls[0]![0]).toBe('/account/login?ReturnUrl=%2Faccount');
    } finally {
      vi.unstubAllGlobals();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation,
      });
    }
  });
});

import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { SessionsPage, type ActiveSession } from './SessionsPage.js';

function renderPage(props: Parameters<typeof SessionsPage>[0]) {
  return render(
    <AppThemeProvider>
      <SessionsPage {...props} />
    </AppThemeProvider>,
  );
}

describe('SessionsPage', () => {
  it('renders the active sessions, marks the current one, and offers revoke controls', async () => {
    const sessions: ActiveSession[] = [
      {
        sessionId: 's1',
        isCurrent: true,
        createdWhen: 1_700_000_000,
        lastActivityWhen: 1_700_000_500,
        userAgent: 'Mozilla/5.0 Current',
        ipAddress: '10.0.0.1',
      },
      {
        sessionId: 's2',
        isCurrent: false,
        createdWhen: 1_700_000_100,
        lastActivityWhen: 1_700_000_200,
        userAgent: 'Mozilla/5.0 Other',
        ipAddress: '10.0.0.2',
      },
    ];
    const { container } = renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () => Promise.resolve(sessions),
    });

    expect(await screen.findByTestId('sessions-list')).toBeInTheDocument();
    expect(screen.getByText('Mozilla/5.0 Current')).toBeInTheDocument();
    expect(screen.getByText('Mozilla/5.0 Other')).toBeInTheDocument();
    // The current session is marked and has no per-session revoke form.
    expect(screen.getByTestId('session-current')).toBeInTheDocument();

    const revokeOne = container.querySelector(
      'form[action="/account/sessions/revoke"]',
    ) as HTMLFormElement | null;
    expect(revokeOne?.getAttribute('method')).toBe('post');
    const sid = revokeOne?.querySelector('input[name="sid"]') as HTMLInputElement | null;
    expect(sid?.value).toBe('s2');

    const others = screen.getByTestId('sessions-revoke-others');
    expect(others.querySelector('input[name="scope"]')?.getAttribute('value')).toBe('others');
    const all = screen.getByTestId('sessions-revoke-all');
    expect(all.querySelector('input[name="scope"]')?.getAttribute('value')).toBe('all');
    const token = all.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.value).toBe('the-token');
  });

  it('hides "sign out everywhere else" when every session is the current one', async () => {
    const sessions: ActiveSession[] = [
      {
        sessionId: 's1',
        isCurrent: true,
        createdWhen: 1_700_000_000,
        lastActivityWhen: 1_700_000_000,
      },
    ];
    renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () => Promise.resolve(sessions),
    });

    expect(await screen.findByTestId('sessions-list')).toBeInTheDocument();
    expect(screen.queryByTestId('sessions-revoke-others')).not.toBeInTheDocument();
    // The hard "sign out everywhere" control is always available.
    expect(screen.getByTestId('sessions-revoke-all')).toBeInTheDocument();
  });

  it('shows an empty state when there are no active sessions', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('sessions-empty')).toBeInTheDocument();
  });

  it('fetches the sessions endpoint and redirects to login on 401', async () => {
    const assignMock = vi.fn();
    const originalLocation = window.location;
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        origin: 'https://op.test',
        href: 'https://op.test/account/sessions',
        assign: assignMock,
      },
    });
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
    vi.stubGlobal('fetch', fetchMock);

    try {
      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(assignMock).toHaveBeenCalledTimes(1));
      expect(fetchMock).toHaveBeenCalledWith('/account/sessions/list', expect.anything());
      expect(assignMock.mock.calls[0]![0]).toBe('/account/login?ReturnUrl=%2Faccount%2Fsessions');
    } finally {
      vi.unstubAllGlobals();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation,
      });
    }
  });
});

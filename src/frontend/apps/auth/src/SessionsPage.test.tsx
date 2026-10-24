import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { SessionsPage, type ActiveSession } from './SessionsPage.js';

function renderPage(props: Parameters<typeof SessionsPage>[0]) {
  return render(
    <AppThemeProvider>
      <SessionsPage {...props} />
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

  it('shows the device details and lifecycle timestamps of each session', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () =>
        Promise.resolve([
          {
            sessionId: 's1',
            isCurrent: true,
            createdWhen: 1_700_000_000,
            lastActivityWhen: 1_700_000_500,
            userAgent: 'Mozilla/5.0 Current',
            ipAddress: '10.0.0.1',
          },
        ]),
    });

    const item = await screen.findByTestId('session-item');
    expect(within(item).getByText('IP address').nextElementSibling).toHaveTextContent('10.0.0.1');
    expect(within(item).getByText('Last active').nextElementSibling).toHaveTextContent(
      new Date(1_700_000_500 * 1000).toLocaleString(),
    );
    expect(within(item).getByText('Signed in').nextElementSibling).toHaveTextContent(
      new Date(1_700_000_000 * 1000).toLocaleString(),
    );
  });

  it('labels a session without a captured device and omits its missing IP address', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () =>
        Promise.resolve([
          {
            sessionId: 's2',
            isCurrent: false,
            createdWhen: 1_700_000_000,
            lastActivityWhen: 1_700_000_000,
            userAgent: null,
            ipAddress: null,
          },
        ]),
    });

    const item = await screen.findByTestId('session-item');
    expect(within(item).getByText('Unknown device')).toBeInTheDocument();
    expect(within(item).queryByText('IP address')).not.toBeInTheDocument();
    expect(within(item).getByText('Last active')).toBeInTheDocument();
    expect(within(item).getByRole('button', { name: 'Sign out' })).toHaveAttribute(
      'value',
      'revoke',
    );
    // Another session exists, so the soft "sign out everywhere else" control is offered.
    expect(screen.getByTestId('sessions-revoke-others')).toBeInTheDocument();
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
    expect(screen.queryByTestId('sessions-revoke-others')).not.toBeInTheDocument();
    expect(screen.getByTestId('sessions-revoke-all')).toBeInTheDocument();
  });

  it('titles the page with the product name when one is provided', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      productName: 'Contoso',
      loadSessions: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('sessions-empty')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Contoso — active sessions',
    );
  });

  it('titles the page generically and links back to the account page', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('sessions-empty')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Active sessions');
    expect(screen.getByRole('link', { name: 'Back to your account' })).toHaveAttribute(
      'href',
      '/account/',
    );
  });

  it('stays in the loading state when the loader fails', async () => {
    const loadSessions = vi.fn(() => Promise.reject(new Error('network down')));
    renderPage({ antiforgeryToken: 'the-token', loadSessions });

    await waitFor(() => expect(loadSessions).toHaveBeenCalledTimes(1));
    await act(async () => {});
    expect(screen.getByTestId('sessions-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('sessions-empty')).not.toBeInTheDocument();
    // Signing out everywhere does not depend on the list, so it stays available.
    expect(screen.getByTestId('sessions-revoke-all')).toBeInTheDocument();
  });

  it('ignores a stale load when the loader changes before it resolves', async () => {
    const stale = deferred<readonly ActiveSession[] | null>();
    const { rerender } = renderPage({
      antiforgeryToken: 'the-token',
      loadSessions: () => stale.promise,
    });
    rerender(
      <AppThemeProvider>
        <SessionsPage
          antiforgeryToken="the-token"
          loadSessions={() =>
            Promise.resolve([
              {
                sessionId: 'fresh',
                isCurrent: true,
                createdWhen: 1_700_000_000,
                lastActivityWhen: 1_700_000_000,
                userAgent: 'Fresh device',
              },
            ])
          }
        />
      </AppThemeProvider>,
    );

    expect(await screen.findByText('Fresh device')).toBeInTheDocument();
    await act(async () => {
      stale.resolve([
        {
          sessionId: 'stale',
          isCurrent: false,
          createdWhen: 1_700_000_000,
          lastActivityWhen: 1_700_000_000,
          userAgent: 'Stale device',
        },
      ]);
    });

    expect(screen.getAllByTestId('session-item')).toHaveLength(1);
    expect(screen.queryByText('Stale device')).not.toBeInTheDocument();
  });

  describe('default loader', () => {
    const originalLocation = window.location;
    let assignMock: ReturnType<typeof vi.fn>;

    beforeEach(() => {
      assignMock = vi.fn();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: {
          origin: 'https://op.test',
          href: 'https://op.test/account/sessions',
          assign: assignMock,
        },
      });
    });

    afterEach(() => {
      vi.unstubAllGlobals();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation,
      });
    });

    function jsonResponse(body: unknown): Response {
      return new Response(JSON.stringify(body), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      });
    }

    it('fetches the sessions endpoint and renders the returned sessions', async () => {
      const fetchMock = vi.fn(() =>
        Promise.resolve(
          jsonResponse({
            sessions: [
              {
                sessionId: 's1',
                isCurrent: true,
                createdWhen: 1_700_000_000,
                lastActivityWhen: 1_700_000_500,
                userAgent: 'Mozilla/5.0 Current',
              },
            ],
          }),
        ),
      );
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      expect(await screen.findByText('Mozilla/5.0 Current')).toBeInTheDocument();
      expect(fetchMock).toHaveBeenCalledWith('/account/sessions/list', {
        headers: { accept: 'application/json' },
      });
      expect(assignMock).not.toHaveBeenCalled();
    });

    it('omits the timestamp rows a payload does not carry', async () => {
      // The OP's payload is untyped JSON: a session missing its timestamps must not render "Invalid Date".
      vi.stubGlobal(
        'fetch',
        vi.fn(() =>
          Promise.resolve(
            jsonResponse({
              sessions: [{ sessionId: 's1', isCurrent: true, userAgent: 'Mozilla/5.0 Current' }],
            }),
          ),
        ),
      );

      renderPage({ antiforgeryToken: 'the-token' });

      const item = await screen.findByTestId('session-item');
      expect(within(item).getByText('Mozilla/5.0 Current')).toBeInTheDocument();
      expect(within(item).queryByText('Last active')).not.toBeInTheDocument();
      expect(within(item).queryByText('Signed in')).not.toBeInTheDocument();
      expect(within(item).queryByText(/Invalid Date/)).not.toBeInTheDocument();
    });

    it('shows the empty state when the payload carries no sessions', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn(() => Promise.resolve(jsonResponse({}))),
      );

      renderPage({ antiforgeryToken: 'the-token' });

      expect(await screen.findByTestId('sessions-empty')).toBeInTheDocument();
    });

    it('redirects to login on 401', async () => {
      const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(assignMock).toHaveBeenCalledTimes(1));
      expect(fetchMock).toHaveBeenCalledWith('/account/sessions/list', expect.anything());
      expect(assignMock.mock.calls[0]![0]).toBe('/account/login?ReturnUrl=%2Faccount%2Fsessions');
      expect(screen.getByTestId('sessions-loading')).toBeInTheDocument();
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
      expect(screen.getByTestId('sessions-loading')).toBeInTheDocument();
    });

    it('stays in the loading state when fetch is unavailable', async () => {
      vi.stubGlobal('fetch', undefined);

      renderPage({ antiforgeryToken: 'the-token' });
      await act(async () => {});

      expect(screen.getByTestId('sessions-loading')).toBeInTheDocument();
      expect(assignMock).not.toHaveBeenCalled();
    });
  });
});

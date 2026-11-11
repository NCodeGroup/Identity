import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { AuthorizedAppsPage, type AuthorizedApplication } from './AuthorizedAppsPage.js';

function renderPage(props: Parameters<typeof AuthorizedAppsPage>[0]) {
  return render(
    <AppThemeProvider>
      <AuthorizedAppsPage {...props} />
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

describe('AuthorizedAppsPage', () => {
  it('renders the authorized applications with their granted scopes and a revoke form', async () => {
    const applications: AuthorizedApplication[] = [
      {
        clientId: 'app-1',
        clientName: 'Contoso',
        scopes: [
          { value: 'openid', description: 'Sign you in.' },
          { value: 'profile', description: null },
        ],
      },
    ];
    const { container } = renderPage({
      antiforgeryToken: 'the-token',
      loadApplications: () => Promise.resolve(applications),
    });

    expect(await screen.findByTestId('applications-list')).toBeInTheDocument();
    expect(screen.getByText('Contoso')).toBeInTheDocument();
    expect(screen.getByText('Sign you in.')).toBeInTheDocument();
    expect(screen.getByText('profile')).toBeInTheDocument();

    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/account/applications/revoke');
    expect(form?.getAttribute('method')).toBe('post');
    const clientId = container.querySelector('input[name="client_id"]') as HTMLInputElement | null;
    expect(clientId?.value).toBe('app-1');
    const token = container.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.value).toBe('the-token');
  });

  it('gives each application its own revoke form and falls back to the client id without a name', async () => {
    const applications: AuthorizedApplication[] = [
      { clientId: 'app-1', clientName: 'Contoso', scopes: [{ value: 'openid' }] },
      { clientId: 'app-2', clientName: null, scopes: [] },
    ];
    renderPage({
      antiforgeryToken: 'the-token',
      loadApplications: () => Promise.resolve(applications),
    });

    const items = await screen.findAllByTestId('application-item');
    expect(items).toHaveLength(2);

    const [named, unnamed] = items as [HTMLElement, HTMLElement];
    expect(within(named).getByText('Contoso')).toBeInTheDocument();
    expect(within(named).getByText('openid')).toBeInTheDocument();
    expect((named.querySelector('input[name="client_id"]') as HTMLInputElement | null)?.value).toBe(
      'app-1',
    );

    // An unnamed application shows its client id, and one with no scopes shows no scope chips.
    expect(within(unnamed).getByText('app-2')).toBeInTheDocument();
    expect(unnamed.querySelector('.MuiChip-root')).toBeNull();
    expect(
      (unnamed.querySelector('input[name="client_id"]') as HTMLInputElement | null)?.value,
    ).toBe('app-2');
    expect(within(unnamed).getByRole('button', { name: 'Revoke' })).toHaveAttribute(
      'value',
      'revoke',
    );
  });

  it('shows an empty state when there are no authorized applications', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadApplications: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('applications-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('applications-list')).not.toBeInTheDocument();
  });

  it('titles the page with the product name when one is provided', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      productName: 'Contoso',
      loadApplications: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('applications-empty')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Contoso — authorized applications',
    );
  });

  it('titles the page generically and links back to the account page', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadApplications: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('applications-empty')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Authorized applications');
    expect(screen.getByRole('link', { name: 'Back to your account' })).toHaveAttribute(
      'href',
      '/account/',
    );
  });

  it('stays in the loading state when the loader fails', async () => {
    const loadApplications = vi.fn(() => Promise.reject(new Error('network down')));
    renderPage({ antiforgeryToken: 'the-token', loadApplications });

    await waitFor(() => expect(loadApplications).toHaveBeenCalledTimes(1));
    await act(async () => {});
    expect(screen.getByTestId('applications-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('applications-empty')).not.toBeInTheDocument();
  });

  it('ignores a stale load when the loader changes before it resolves', async () => {
    const stale = deferred<readonly AuthorizedApplication[] | null>();
    const { rerender } = renderPage({
      antiforgeryToken: 'the-token',
      loadApplications: () => stale.promise,
    });
    rerender(
      <AppThemeProvider>
        <AuthorizedAppsPage
          antiforgeryToken="the-token"
          loadApplications={() =>
            Promise.resolve([{ clientId: 'fresh', clientName: 'Fresh App', scopes: [] }])
          }
        />
      </AppThemeProvider>,
    );

    expect(await screen.findByText('Fresh App')).toBeInTheDocument();
    await act(async () => {
      stale.resolve([{ clientId: 'stale', clientName: 'Stale App', scopes: [] }]);
    });

    expect(screen.getAllByTestId('application-item')).toHaveLength(1);
    expect(screen.queryByText('Stale App')).not.toBeInTheDocument();
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
          href: 'https://op.test/account/applications',
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

    it('fetches the applications endpoint and renders the returned applications', async () => {
      const fetchMock = vi.fn(() =>
        Promise.resolve(
          jsonResponse({
            applications: [{ clientId: 'app-1', clientName: 'Contoso', scopes: [] }],
          }),
        ),
      );
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      expect(await screen.findByText('Contoso')).toBeInTheDocument();
      expect(fetchMock).toHaveBeenCalledWith('/account/applications/list', {
        headers: { accept: 'application/json' },
      });
      expect(assignMock).not.toHaveBeenCalled();
    });

    it('shows the empty state when the payload carries no applications', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn(() => Promise.resolve(jsonResponse({}))),
      );

      renderPage({ antiforgeryToken: 'the-token' });

      expect(await screen.findByTestId('applications-empty')).toBeInTheDocument();
    });

    it('redirects to login on 401', async () => {
      const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
      vi.stubGlobal('fetch', fetchMock);

      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(assignMock).toHaveBeenCalledTimes(1));
      expect(fetchMock).toHaveBeenCalledWith('/account/applications/list', expect.anything());
      expect(assignMock.mock.calls[0]![0]).toBe(
        '/account/login?ReturnUrl=%2Faccount%2Fapplications',
      );
      expect(screen.getByTestId('applications-loading')).toBeInTheDocument();
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
      expect(screen.getByTestId('applications-loading')).toBeInTheDocument();
    });

    it('stays in the loading state when fetch is unavailable', async () => {
      vi.stubGlobal('fetch', undefined);

      renderPage({ antiforgeryToken: 'the-token' });
      await act(async () => {});

      expect(screen.getByTestId('applications-loading')).toBeInTheDocument();
      expect(assignMock).not.toHaveBeenCalled();
    });
  });
});

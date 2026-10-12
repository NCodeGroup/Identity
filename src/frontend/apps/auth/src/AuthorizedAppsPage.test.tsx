import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { AuthorizedAppsPage, type AuthorizedApplication } from './AuthorizedAppsPage.js';

function renderPage(props: Parameters<typeof AuthorizedAppsPage>[0]) {
  return render(
    <AppThemeProvider>
      <AuthorizedAppsPage {...props} />
    </AppThemeProvider>,
  );
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

  it('shows an empty state when there are no authorized applications', async () => {
    renderPage({
      antiforgeryToken: 'the-token',
      loadApplications: () => Promise.resolve([]),
    });

    expect(await screen.findByTestId('applications-empty')).toBeInTheDocument();
  });

  it('fetches the applications endpoint and redirects to login on 401', async () => {
    const assignMock = vi.fn();
    const originalLocation = window.location;
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        origin: 'https://op.test',
        href: 'https://op.test/account/applications',
        assign: assignMock,
      },
    });
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 401 })));
    vi.stubGlobal('fetch', fetchMock);

    try {
      renderPage({ antiforgeryToken: 'the-token' });

      await waitFor(() => expect(assignMock).toHaveBeenCalledTimes(1));
      expect(fetchMock).toHaveBeenCalledWith('/account/applications/list', expect.anything());
      expect(assignMock.mock.calls[0]![0]).toBe(
        '/account/login?ReturnUrl=%2Faccount%2Fapplications',
      );
    } finally {
      vi.unstubAllGlobals();
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: originalLocation,
      });
    }
  });
});

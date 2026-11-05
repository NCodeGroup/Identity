import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { HomePage } from './HomePage.js';
import { platformRoute, teamRoute, tenantRoute } from '../routes.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage(onNavigate = vi.fn()) {
  const store = createAppStore({
    config: { runtime },
    auth: {
      accessToken: 'token',
      subject: 'admin',
      expiresAt: null,
      refreshToken: null,
      idToken: null,
    },
  });
  render(
    <Provider store={store}>
      <HomePage onNavigate={onNavigate} />
    </Provider>,
  );
  return onNavigate;
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const team = {
  teamId: 'support',
  concurrencyToken: 'tc1',
  displayName: 'Support',
  isDisabled: false,
};

const tenant = {
  tenantId: 'acme',
  teamId: 'support',
  concurrencyToken: 'tn1',
  displayName: 'Acme',
  issuer: 'https://acme.test',
  mtlsEndpointAliasAuthority: null,
  isDisabled: false,
};

/** Serves the caller's resource, its teams and its tenants; any other request is unexpected. */
function installFetch(options: {
  readonly isPlatformAdministrator: boolean;
  readonly teams: readonly unknown[];
  readonly tenants: readonly unknown[];
}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    if (request.method === 'GET' && request.url.endsWith('/api/me')) {
      return jsonResponse({
        principalId: 'principal-1',
        isPlatformAdministrator: options.isPlatformAdministrator,
        assignments: [],
      });
    }
    if (request.method === 'GET' && request.url.endsWith('/api/me/teams')) {
      return jsonResponse({ items: options.teams });
    }
    if (request.method === 'GET' && request.url.endsWith('/api/me/tenants')) {
      return jsonResponse({ items: options.tenants });
    }
    throw new Error(`Unexpected ${request.method} ${request.url}`);
  });
  vi.stubGlobal('fetch', fetchMock);
}

describe('HomePage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('lists the platform, the teams and the tenants and navigates on each click', async () => {
    installFetch({
      isPlatformAdministrator: true,
      teams: [team, { ...team, teamId: 'sales', displayName: 'Sales' }],
      tenants: [tenant],
    });

    const onNavigate = renderPage();

    fireEvent.click(await screen.findByTestId('home-open-platform'));
    expect(onNavigate).toHaveBeenLastCalledWith(platformRoute('teams'));

    fireEvent.click(screen.getByTestId('team-row-support'));
    expect(onNavigate).toHaveBeenLastCalledWith(teamRoute('support'));

    fireEvent.click(screen.getByTestId('tenant-row-acme'));
    expect(onNavigate).toHaveBeenLastCalledWith(tenantRoute('acme'));
    expect(onNavigate).toHaveBeenCalledTimes(3);
  });

  it('opens the only team with replace', async () => {
    installFetch({ isPlatformAdministrator: false, teams: [team], tenants: [] });

    const onNavigate = renderPage();

    await waitFor(() =>
      expect(onNavigate).toHaveBeenCalledWith(teamRoute('support'), { replace: true }),
    );
    expect(onNavigate).toHaveBeenCalledTimes(1);
  });

  it('opens the platform for an administrator with no role of its own', async () => {
    installFetch({ isPlatformAdministrator: true, teams: [], tenants: [] });

    const onNavigate = renderPage();

    await waitFor(() =>
      expect(onNavigate).toHaveBeenCalledWith(platformRoute('teams'), { replace: true }),
    );
    expect(onNavigate).toHaveBeenCalledTimes(1);
  });

  it('shows the empty message when the caller holds no role', async () => {
    installFetch({ isPlatformAdministrator: false, teams: [], tenants: [] });

    const onNavigate = renderPage();

    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'You hold no role in this console. Ask an administrator for access to a team or a tenant.',
    );
    expect(onNavigate).not.toHaveBeenCalled();
  });

  it('shows a loading indicator while the access loads', async () => {
    installFetch({ isPlatformAdministrator: false, teams: [], tenants: [] });

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
  });

  it('shows an error when the access cannot be loaded', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse({ title: 'Internal Server Error' }, 500)),
    );

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent(
      'Failed to load your access.',
    );
  });
});

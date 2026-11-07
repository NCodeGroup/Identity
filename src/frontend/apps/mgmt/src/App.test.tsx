import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { act, render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore, type BffUser } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import { App } from './App.js';
// Each surface is a lazily loaded chunk (ADR-1175). Loading the modules first lets each lazy surface resolve from the
// module cache, so a test waits for the surface's data and render rather than a cold transform of its chunk.
import './containers/ClientsPage.js';
import './containers/GrantsPage.js';
import './containers/HomePage.js';
import './containers/MachineClientsPage.js';
import './containers/ResourceServersPage.js';
import './containers/PlatformSettingsPage.js';
import './containers/TeamDetailPage.js';
import './containers/TeamsPage.js';
import './containers/TenantDetailPage.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const adminUser: BffUser = {
  sub: 'admin-sub',
  name: 'admin',
  claims: {},
  session: { expiresAt: 4_102_444_800, idleExpiresAt: 4_102_444_800 },
  logoutUrl: '/bff/logout',
  logoutSid: 'guard',
};

const namelessUser: BffUser = {
  sub: adminUser.sub,
  claims: {},
  session: adminUser.session,
  logoutUrl: adminUser.logoutUrl,
  logoutSid: adminUser.logoutSid,
};

function renderApp() {
  const store = createAppStore({
    config: { runtime },
    session: { status: 'signed-in', user: adminUser },
  });
  return render(
    <Provider store={store}>
      <App />
    </Provider>,
  );
}

function jsonResponse(body: unknown) {
  return Promise.resolve(
    new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'content-type': 'application/json' },
    }),
  );
}

const acmeTeam = {
  teamId: 'acme',
  concurrencyToken: 'tc',
  displayName: 'Acme team',
  isDisabled: false,
  parentTeamId: null,
};

const acmeWebTeam = {
  ...acmeTeam,
  teamId: 'acme-web',
  displayName: 'Acme web team',
  parentTeamId: 'acme',
};

const acmeTenant = {
  tenantId: 'acme',
  teamId: 'acme',
  displayName: 'Acme',
  issuer: null,
  mtlsEndpointAliasAuthority: null,
  isDisabled: false,
  concurrencyToken: 'c',
};

const acmeDevTenant = { ...acmeTenant, tenantId: 'acme-dev', displayName: 'Acme dev' };

const globexTeam = { ...acmeTeam, teamId: 'globex', displayName: 'Globex team' };

/** What the caller's own resource and its teams and tenants answer. */
type CallerFixture = {
  readonly isPlatformAdministrator: boolean;
  readonly teams: readonly unknown[];
  readonly tenants: readonly unknown[];
};

/** A platform administrator that holds no role of its own. */
const platformAdministrator: CallerFixture = {
  isPlatformAdministrator: true,
  teams: [],
  tenants: [],
};

/** Serves the caller's access: `/api/me`, `/api/me/teams` and `/api/me/tenants`. */
function callerResponse(
  url: string,
  caller: CallerFixture = platformAdministrator,
): Promise<Response> | undefined {
  if (url.endsWith('/api/me/teams')) {
    return jsonResponse({ items: caller.teams });
  }
  if (url.endsWith('/api/me/tenants')) {
    return jsonResponse({ items: caller.tenants });
  }
  if (url.endsWith('/api/me')) {
    return jsonResponse({
      principalId: 'admin-principal',
      isPlatformAdministrator: caller.isPlatformAdministrator,
      assignments: [],
    });
  }
  return undefined;
}

/**
 * Serves the caller's access and the directory the console browses to reach a tenant: the teams, a team's tenants
 * and one tenant.
 */
function directoryResponse(
  url: string,
  caller: CallerFixture = platformAdministrator,
): Promise<Response> | undefined {
  const access = callerResponse(url, caller);
  if (access) {
    return access;
  }
  if (url.endsWith('/api/teams/acme/tenants')) {
    return jsonResponse({ items: [acmeTenant, acmeDevTenant] });
  }
  if (url.endsWith('/api/teams/acme/teams')) {
    return jsonResponse({ items: [acmeWebTeam] });
  }
  if (url.endsWith('/api/teams/acme-web/tenants')) {
    return jsonResponse({ items: [] });
  }
  if (url.endsWith('/api/teams/acme-web')) {
    return jsonResponse(acmeWebTeam);
  }
  if (url.endsWith('/api/teams/acme')) {
    return jsonResponse(acmeTeam);
  }
  if (url.endsWith('/api/teams')) {
    return jsonResponse({ items: [acmeTeam] });
  }
  if (url.endsWith('/api/tenants/acme')) {
    return jsonResponse(acmeTenant);
  }
  if (url.endsWith('/api/tenants/acme-dev')) {
    return jsonResponse(acmeDevTenant);
  }
  return undefined;
}

describe('App (platform ↔ team ↔ tenant boundary)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('browses teams on the platform, then crosses into a team and a tenant scope', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url =
        input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
      if (url.includes('/api/tenants/acme/clients')) {
        return jsonResponse({
          items: [
            {
              clientId: 'web',
              tenantId: 'acme',
              isDisabled: false,
              concurrencyToken: 'c',
            },
          ],
        });
      }
      const directory = directoryResponse(url);
      if (directory) {
        return directory;
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderApp();

    // Platform scope: the team list renders and the badge reads "Platform".
    expect(await screen.findByText('Acme team')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('Platform');

    // Cross into the team: its tenants render and the badge names the team.
    fireEvent.click(screen.getByTestId('team-row-acme'));
    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('acme');
    // The team and the tenant share an id, so the badge's icon tells the scopes apart.
    expect(within(screen.getByTestId('scope-badge')).getByTestId('GroupsIcon')).toBeInTheDocument();

    // Cross into the tenant.
    fireEvent.click(screen.getByTestId('tenant-row-acme'));

    expect(await screen.findByText('web')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('acme');
    expect(
      within(screen.getByTestId('scope-badge')).getByTestId('BusinessIcon'),
    ).toBeInTheDocument();
    expect(within(screen.getByTestId('scope-badge')).queryByTestId('GroupsIcon')).toBeNull();
    const requestedUrls = fetchMock.mock.calls.map(([input]) =>
      input instanceof Request ? input.url : String(input),
    );
    expect(requestedUrls).toContain('https://api.test/api/tenants/acme/clients');
  });

  it('switches between the clients and resource-servers surfaces within a tenant', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url =
        input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
      if (url.includes('/api/tenants/acme/resource-servers')) {
        return jsonResponse({
          items: [
            {
              tenantId: 'acme',
              resourceServerId: 'rs-orders',
              identifier: 'https://orders.example.com',
              concurrencyToken: 'c',
              name: 'Orders API',
              isSystem: false,
              isDisabled: false,
              scopes: [],
            },
          ],
        });
      }
      if (url.includes('/api/tenants/acme/clients')) {
        return jsonResponse({
          items: [{ clientId: 'web', tenantId: 'acme', isDisabled: false, concurrencyToken: 'c' }],
        });
      }
      const directory = directoryResponse(url);
      if (directory) {
        return directory;
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    window.history.replaceState(null, '', '/teams/acme/tenants');
    renderApp();

    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    expect(await screen.findByText('web')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-tab-resource-servers'));
    expect(await screen.findByText('Orders API')).toBeInTheDocument();

    const requestedUrls = fetchMock.mock.calls.map(([input]) =>
      input instanceof Request ? input.url : String(input),
    );
    expect(requestedUrls).toContain('https://api.test/api/tenants/acme/resource-servers');
  });

  it('switches to the platform Settings surface', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url =
        input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
      if (url.endsWith('/api/platform/settings')) {
        return jsonResponse({ concurrencyToken: 'st', settings: { a: 1 } });
      }
      if (url.endsWith('/settings/limits')) {
        return jsonResponse({ names: [], canChange: true });
      }
      if (url.endsWith('/api/platform/effective-settings')) {
        return jsonResponse({ settings: { a: 1 } });
      }
      const directory = directoryResponse(url);
      if (directory) {
        return directory;
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderApp();

    fireEvent.click(await screen.findByTestId('platform-tab-settings'));
    expect(await screen.findByRole('heading', { name: 'Platform settings' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/settings');

    const requestedUrls = fetchMock.mock.calls.map(([input]) =>
      input instanceof Request ? input.url : String(input),
    );
    expect(requestedUrls).toContain('https://api.test/api/platform/settings');
  });

  it('switches to the platform Encryption keys surface', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url =
        input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
      if (url.endsWith('/api/platform/encryption-keys')) {
        return jsonResponse({
          recoveryConfigured: true,
          rootKey: null,
          rootKeyProviders: [],
          keys: [
            {
              keyId: 'k-1',
              version: 1,
              state: 'active',
              createdWhen: '2026-10-01T09:00:00Z',
              retiredWhen: null,
              rootKeyProvider: 'local',
              rootKeyId: 'root-1',
              recoveryKeyId: null,
              rootKeyReference: null,
            },
          ],
        });
      }
      const directory = directoryResponse(url);
      if (directory) {
        return directory;
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderApp();

    fireEvent.click(await screen.findByTestId('platform-tab-encryption-keys'));
    expect(await screen.findByTestId('encryption-key-row-k-1')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/encryption-keys');
  });

  it('switches to the platform Clients surface', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url =
          input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
        if (url.endsWith('/api/platform/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    renderApp();

    fireEvent.click(await screen.findByTestId('platform-tab-clients'));
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'The platform has no machine clients yet.',
    );
    expect(window.location.pathname).toBe('/clients');
  });

  it('lists teams and crosses into a team scope', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    renderApp();

    fireEvent.click(await screen.findByTestId('team-row-acme'));

    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams/acme/tenants');
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('acme');
    expect(screen.getByRole('tab', { name: 'Tenants' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('exit-scope')).toHaveTextContent('Platform');

    fireEvent.click(screen.getByTestId('exit-scope'));

    expect(await screen.findByTestId('team-row-acme')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('Platform');
    expect(window.location.pathname).toBe('/teams');
  });

  it('switches between the surfaces of a team scope', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/teams/acme/usage')) {
          return jsonResponse({ from: '2026-09-21', to: '2026-10-21', entries: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    renderApp();

    fireEvent.click(await screen.findByTestId('team-row-acme'));
    fireEvent.click(await screen.findByTestId('team-tab-usage'));

    expect(await screen.findByTestId('usage-range')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams/acme/usage');
  });

  it("switches to a team's child teams", async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    renderApp();

    fireEvent.click(await screen.findByTestId('team-row-acme'));
    fireEvent.click(await screen.findByTestId('team-tab-teams'));

    expect(await screen.findByTestId('team-row-acme-web')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams/acme/teams');
  });

  it("switches to a team's machine clients", async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url =
          input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
        if (url.endsWith('/api/teams/acme/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    renderApp();

    fireEvent.click(await screen.findByTestId('team-row-acme'));
    fireEvent.click(await screen.findByTestId('team-tab-clients'));

    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This team has no machine clients yet.',
    );
    expect(window.location.pathname).toBe('/teams/acme/clients');
  });

  it('exits a child team to its parent team', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    window.history.replaceState(null, '', '/teams/acme-web/tenants');
    renderApp();

    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Team'));

    fireEvent.click(screen.getByTestId('exit-scope'));

    expect(await screen.findByTestId('team-row-acme-web')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams/acme/teams');
  });

  it('exits a tenant back to its team, then the team back to the platform', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants/acme-dev/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    window.history.replaceState(null, '', '/teams/acme/tenants');
    renderApp();
    fireEvent.click(await screen.findByTestId('tenant-row-acme-dev'));
    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();
    // Once the tenant loads, the exit names its team.
    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Team'));

    fireEvent.click(screen.getByTestId('exit-scope'));

    // Swapping the scope's tab set re-measures the tab indicator asynchronously; await the settled UI.
    expect(await screen.findByTestId('tenant-row-acme-dev')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams/acme/tenants');
    expect(screen.getByTestId('exit-scope')).toHaveTextContent('Platform');

    fireEvent.click(screen.getByTestId('exit-scope'));

    expect(await screen.findByTestId('team-row-acme')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('Platform');
    expect(screen.queryByTestId('exit-scope')).not.toBeInTheDocument();
  });

  it('exits a tenant that has not loaded to the platform team list', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants/acme-dev')) {
          return new Promise<Response>(() => undefined);
        }
        if (url.endsWith('/api/tenants/acme-dev/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    window.history.replaceState(null, '', '/tenants/acme-dev/clients');
    renderApp();

    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();
    expect(
      within(screen.getByTestId('scope-badge')).getByTestId('BusinessIcon'),
    ).toBeInTheDocument();
    expect(screen.getByTestId('exit-scope')).toHaveTextContent('Platform');

    fireEvent.click(screen.getByTestId('exit-scope'));

    expect(await screen.findByTestId('team-row-acme')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams');
  });

  it('labels the exit from the current tenant only, not from a tenant viewed before it', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants/globex-dev')) {
          return new Promise<Response>(() => undefined);
        }
        if (url.endsWith('/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    window.history.replaceState(null, '', '/tenants/acme-dev/clients');
    renderApp();
    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Team'));

    act(() => {
      window.history.pushState(null, '', '/tenants/globex-dev/clients');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });

    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Platform'));
    fireEvent.click(screen.getByTestId('exit-scope'));

    expect(await screen.findByTestId('team-row-acme')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams');
  });

  it('reaches the grants and tenant surfaces, and re-enters a tenant on its clients', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = (input as Request).url;
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
        return jsonResponse({ concurrencyToken: 'c', secrets: [] });
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [] });
      }
      if (url.endsWith('/api/tenants/acme/clients')) {
        return jsonResponse({ items: [] });
      }
      const directory = directoryResponse(url);
      if (directory) {
        return directory;
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    window.history.replaceState(null, '', '/teams/acme/tenants');
    renderApp();
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-tab-grants'));
    expect(await screen.findByText('No grants match the current filter.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-tab-secrets'));
    expect(await screen.findByText('This tenant has no secrets yet.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/secrets');

    // Leaving and re-entering a tenant starts again on its clients, not the last surface visited.
    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Team'));
    fireEvent.click(screen.getByTestId('exit-scope'));
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    // Swapping the scope's tab set re-measures the tab indicator asynchronously; await the settled UI.
    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();

    const requestedUrls = fetchMock.mock.calls.map(([input]) => (input as Request).url);
    expect(requestedUrls.some((url) => url.includes('/api/tenants/acme/grants'))).toBe(true);
    expect(requestedUrls).toContain('https://api.test/api/tenants/acme/secrets');
  });
});

describe('App (routing)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  function stubTenantApi() {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.includes('/api/tenants/acme/grants')) {
          return jsonResponse({ items: [] });
        }
        if (url.endsWith('/api/tenants/acme/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );
  }

  it('rewrites the bare origin to the platform team list', async () => {
    stubTenantApi();

    renderApp();

    expect(await screen.findByTestId('team-row-acme')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams');
  });

  it('opens a deep link on its tenant scope and surface', async () => {
    stubTenantApi();
    window.history.replaceState(null, '', '/tenants/acme/grants');

    renderApp();

    expect(await screen.findByText('No grants match the current filter.')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('acme');
  });

  it("opens a deep link on one of the tenant's own surfaces", async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants/acme/usage')) {
          return jsonResponse({
            tenantId: 'acme',
            from: '2026-09-21',
            to: '2026-10-21',
            entries: [],
          });
        }
        const directory = directoryResponse(url);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );
    window.history.replaceState(null, '', '/tenants/acme/usage');

    renderApp();

    expect(await screen.findByText('No usage recorded in this range.')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('acme');
    expect(screen.getByRole('tab', { name: 'Usage' })).toHaveAttribute('aria-selected', 'true');
  });

  it('records each navigation in the address bar and restores it on back', async () => {
    stubTenantApi();
    window.history.replaceState(null, '', '/teams/acme/tenants');

    renderApp();
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/clients');

    fireEvent.click(screen.getByTestId('tenant-tab-grants'));
    expect(await screen.findByText('No grants match the current filter.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/grants');

    act(() => window.history.back());
    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/clients');
  });

  it('crosses back to the platform scope when the history leaves the tenant', async () => {
    stubTenantApi();
    window.history.replaceState(null, '', '/teams');
    window.history.pushState(null, '', '/tenants/acme/clients');

    renderApp();
    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();

    act(() => window.history.back());

    await waitFor(() => expect(screen.getByTestId('scope-badge')).toHaveTextContent('Platform'));
    expect(screen.queryByTestId('exit-scope')).not.toBeInTheDocument();
  });
});

describe('App (caller access)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  function stubCaller(caller: CallerFixture) {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants/acme/clients')) {
          return jsonResponse({ items: [] });
        }
        const directory = directoryResponse(url, caller);
        if (directory) {
          return directory;
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );
  }

  const teamAdministrator: CallerFixture = {
    isPlatformAdministrator: false,
    teams: [acmeTeam],
    tenants: [],
  };

  it("opens a team administrator's only team from the bare origin", async () => {
    stubCaller(teamAdministrator);

    renderApp();

    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/teams/acme/tenants');
  });

  it('sends a caller without platform authority from a platform route to its home', async () => {
    stubCaller({
      isPlatformAdministrator: false,
      teams: [acmeTeam, globexTeam],
      tenants: [],
    });
    window.history.replaceState(null, '', '/teams');

    renderApp();

    expect(await screen.findByTestId('team-row-globex')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/home');
    expect(screen.queryByTestId('exit-scope')).not.toBeInTheDocument();
  });

  it("leads a team administrator's team back to its home", async () => {
    stubCaller(teamAdministrator);
    window.history.replaceState(null, '', '/teams/acme/tenants');

    renderApp();

    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Home'));
  });

  it('exits a child team home for a caller without a role at its parent', async () => {
    stubCaller({ isPlatformAdministrator: false, teams: [acmeWebTeam], tenants: [] });
    window.history.replaceState(null, '', '/teams/acme-web/tenants');

    renderApp();

    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Home'));
  });

  it("leads a tenant-only caller's tenant back to its home", async () => {
    stubCaller({ isPlatformAdministrator: false, teams: [], tenants: [acmeTenant] });
    window.history.replaceState(null, '', '/tenants/acme/clients');

    renderApp();

    expect(await screen.findByText('No clients in this tenant yet.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('exit-scope')).toHaveTextContent('Home'));
  });

  it('returns to the home from a scope with the Home control', async () => {
    stubCaller({ isPlatformAdministrator: true, teams: [acmeTeam], tenants: [] });
    window.history.replaceState(null, '', '/teams/acme/tenants');

    renderApp();
    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('go-home'));

    expect(await screen.findByTestId('home-open-platform')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/home');
    expect(screen.queryByTestId('go-home')).not.toBeInTheDocument();
  });
});

describe('App (session)', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/');
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function renderSession(session: { status: 'signed-in' | 'signed-out'; user: BffUser | null }) {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        const caller = callerResponse(url);
        if (caller) {
          return caller;
        }
        if (url.endsWith('/api/teams')) {
          return jsonResponse({ items: [] });
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );
    const store = createAppStore({ config: { runtime }, session });
    render(
      <Provider store={store}>
        <App />
      </Provider>,
    );
    return store;
  }

  it('labels the session with the name', async () => {
    renderSession({
      status: 'signed-in',
      user: { ...adminUser, name: 'Ada Admin', claims: { preferred_username: 'ada' } },
    });
    expect(await screen.findByText('No teams exist yet.')).toBeInTheDocument();

    expect(screen.getByTestId('session-subject')).toHaveTextContent('Ada Admin');
  });

  it('labels the session with the preferred_username claim when the user has no name', async () => {
    renderSession({
      status: 'signed-in',
      user: { ...namelessUser, claims: { preferred_username: 'ada' } },
    });
    expect(await screen.findByText('No teams exist yet.')).toBeInTheDocument();

    expect(screen.getByTestId('session-subject')).toHaveTextContent('ada');
  });

  it('labels the session with the subject when there is neither a name nor a username', async () => {
    renderSession({
      status: 'signed-in',
      user: { ...namelessUser, claims: { preferred_username: 42 } },
    });
    expect(await screen.findByText('No teams exist yet.')).toBeInTheDocument();

    expect(screen.getByTestId('session-subject')).toHaveTextContent('admin-sub');
  });

  it('signs out by posting the BFF logout form', async () => {
    const submitted: HTMLFormElement[] = [];
    vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(function (
      this: HTMLFormElement,
    ) {
      submitted.push(this);
    });
    renderSession({ status: 'signed-in', user: adminUser });
    expect(await screen.findByText('No teams exist yet.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('sign-out'));

    expect(submitted).toHaveLength(1);
    const form = submitted[0]!;
    expect(form.method).toBe('post');
    expect(form.getAttribute('action')).toBe('/bff/logout');
    const fields = Object.fromEntries(new FormData(form).entries());
    expect(fields).toEqual({ sid: 'guard', returnUrl: '/' });
  });

  it('hides the session controls when no one is signed in', async () => {
    renderSession({ status: 'signed-out', user: null });
    expect(await screen.findByText('No teams exist yet.')).toBeInTheDocument();

    expect(screen.queryByTestId('sign-out')).not.toBeInTheDocument();
    expect(screen.queryByTestId('session-subject')).not.toBeInTheDocument();
  });
});

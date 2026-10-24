import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { act, render, screen, waitFor, fireEvent } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { App } from './App.js';
// Each surface is a lazily loaded chunk (ADR-0175). Loading the modules first lets each lazy surface resolve from the
// module cache, so a test waits for the surface's data and render rather than a cold transform of its chunk.
import './containers/ClientsPage.js';
import './containers/GrantsPage.js';
import './containers/ResourceServersPage.js';
import './containers/ServersPage.js';
import './containers/TenantDetailPage.js';
import './containers/TenantsPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid', 'read:clients'],
  features: {},
};

function renderApp() {
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

const acmeTenant = {
  tenantId: 'acme',
  displayName: 'Acme',
  issuer: null,
  mtlsEndpointAliasAuthority: null,
  isDisabled: false,
  concurrencyToken: 'c',
};

describe('App (global ↔ tenant boundary)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('browses tenants globally, then crosses into a tenant scope', async () => {
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
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({
          items: [
            {
              tenantId: 'acme',
              displayName: 'Acme',
              issuer: null,
              mtlsEndpointAliasAuthority: null,
              isDisabled: false,
              concurrencyToken: 'c',
            },
          ],
        });
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderApp();

    // Global scope: the tenant list renders and the badge reads "Global".
    expect(await screen.findByText('Acme')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('Global');

    // Cross the boundary into the tenant.
    fireEvent.click(screen.getByTestId('tenant-row-acme'));

    await waitFor(() => expect(screen.getByTestId('scope-badge')).toHaveTextContent('acme'));
    expect(await screen.findByText('web')).toBeInTheDocument();
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
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({
          items: [
            {
              tenantId: 'acme',
              displayName: 'Acme',
              issuer: null,
              mtlsEndpointAliasAuthority: null,
              isDisabled: false,
              concurrencyToken: 'c',
            },
          ],
        });
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

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

  it('switches to the global Servers surface and lists servers', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url =
        input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
      if (url.endsWith('/api/servers')) {
        return jsonResponse({
          items: [{ serverId: 'root', concurrencyToken: 'c', displayName: 'Root server' }],
        });
      }
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderApp();

    fireEvent.click(await screen.findByTestId('global-tab-servers'));
    expect(await screen.findByText('Root server')).toBeInTheDocument();

    const requestedUrls = fetchMock.mock.calls.map(([input]) =>
      input instanceof Request ? input.url : String(input),
    );
    expect(requestedUrls).toContain('https://api.test/api/servers');
  });

  it('exits a tenant back to the global scope', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants/acme/clients')) {
          return jsonResponse({ items: [] });
        }
        if (url.endsWith('/api/tenants')) {
          return jsonResponse({ items: [acmeTenant] });
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );

    renderApp();
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    expect(await screen.findByText('No clients in this scope yet.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('exit-tenant'));

    // Swapping the scope's tab set re-measures the tab indicator asynchronously; await the settled UI.
    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();
    expect(screen.getByTestId('scope-badge')).toHaveTextContent('Global');
    expect(screen.queryByTestId('exit-tenant')).not.toBeInTheDocument();
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
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [acmeTenant] });
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderApp();
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    expect(await screen.findByText('No clients in this scope yet.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-tab-grants'));
    expect(await screen.findByText('No grants match the current filter.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-tab-secrets'));
    expect(await screen.findByText('This tenant has no secrets yet.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/secrets');

    // Leaving and re-entering a tenant starts again on its clients, not the last surface visited.
    fireEvent.click(screen.getByTestId('exit-tenant'));
    fireEvent.click(screen.getByTestId('tenant-row-acme'));
    // Swapping the scope's tab set re-measures the tab indicator asynchronously; await the settled UI.
    expect(await screen.findByText('No clients in this scope yet.')).toBeInTheDocument();

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
        if (url.endsWith('/api/servers')) {
          return jsonResponse({ items: [] });
        }
        if (url.endsWith('/api/tenants')) {
          return jsonResponse({ items: [acmeTenant] });
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );
  }

  it('rewrites the bare origin to the global tenant list', async () => {
    stubTenantApi();

    renderApp();

    expect(await screen.findByTestId('tenant-row-acme')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants');
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

    renderApp();
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));
    expect(await screen.findByText('No clients in this scope yet.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/clients');

    fireEvent.click(screen.getByTestId('tenant-tab-grants'));
    expect(await screen.findByText('No grants match the current filter.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/grants');

    act(() => window.history.back());
    expect(await screen.findByText('No clients in this scope yet.')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/tenants/acme/clients');
  });

  it('crosses back to the global scope when the history leaves the tenant', async () => {
    stubTenantApi();
    window.history.replaceState(null, '', '/servers');
    window.history.pushState(null, '', '/tenants/acme/clients');

    renderApp();
    expect(await screen.findByText('No clients in this scope yet.')).toBeInTheDocument();

    act(() => window.history.back());

    await waitFor(() => expect(screen.getByTestId('scope-badge')).toHaveTextContent('Global'));
    expect(screen.queryByTestId('exit-tenant')).not.toBeInTheDocument();
  });
});

describe('App (sign-out)', () => {
  const originalLocation = window.location;

  afterEach(() => {
    vi.restoreAllMocks();
    Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
  });

  it('clears local tokens and redirects to the OP RP-Initiated Logout', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url =
        input instanceof Request ? input.url : input instanceof URL ? input.href : String(input);
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected fetch: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    const assignMock = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        origin: 'https://ui.test',
        href: 'https://ui.test/tenants',
        pathname: '/tenants',
        assign: assignMock,
      },
    });

    const store = createAppStore({
      config: {
        runtime: {
          ...runtime,
          clientId: 'mgmt-spa',
          endSessionEndpoint: 'https://op.test/end-session',
        },
      },
      auth: {
        accessToken: 'token',
        subject: 'admin',
        expiresAt: null,
        refreshToken: null,
        idToken: 'id-token',
      },
    });
    render(
      <Provider store={store}>
        <App />
      </Provider>,
    );

    expect(await screen.findByTestId('session-subject')).toHaveTextContent('admin');
    fireEvent.click(screen.getByTestId('sign-out'));

    expect(store.getState().auth.accessToken).toBeNull();
    const target = new URL(assignMock.mock.calls[0]![0] as string);
    expect(target.origin + target.pathname).toBe('https://op.test/end-session');
    expect(target.searchParams.get('id_token_hint')).toBe('id-token');
    expect(target.searchParams.get('client_id')).toBe('mgmt-spa');
    expect(target.searchParams.get('post_logout_redirect_uri')).toBe('https://ui.test/');
  });

  function renderSession(
    preloadedRuntime: RuntimeConfig | null,
    auth: { readonly accessToken: string | null; readonly subject: string | null },
  ) {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = (input as Request).url;
        if (url.endsWith('/api/tenants')) {
          return jsonResponse({ items: [] });
        }
        throw new Error(`Unexpected fetch: ${url}`);
      }),
    );
    const assign = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        origin: 'https://ui.test',
        href: 'https://ui.test/tenants',
        pathname: '/tenants',
        assign,
      },
    });
    const store = createAppStore({
      config: { runtime: preloadedRuntime },
      auth: { ...auth, expiresAt: null, refreshToken: null, idToken: 'id-token' },
    });
    render(
      <Provider store={store}>
        <App />
      </Provider>,
    );
    return { store, assign };
  }

  it('clears local tokens without a redirect when the OP advertises no end_session_endpoint', async () => {
    const { store, assign } = renderSession(
      { ...runtime, clientId: 'mgmt-spa' },
      { accessToken: 'token', subject: 'admin' },
    );
    expect(await screen.findByText('No tenants exist yet.')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('sign-out'));

    expect(store.getState().auth.accessToken).toBeNull();
    expect(assign).not.toHaveBeenCalled();
    expect(screen.queryByTestId('sign-out')).not.toBeInTheDocument();
    expect(screen.queryByTestId('session-subject')).not.toBeInTheDocument();
  });

  it('clears local tokens without a redirect when no runtime config is loaded', async () => {
    const { store, assign } = renderSession(null, { accessToken: 'token', subject: 'admin' });
    // Without discovered config there is no API base URL, so the tenant list cannot load.
    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load tenants.');

    fireEvent.click(screen.getByTestId('sign-out'));

    expect(store.getState().auth.accessToken).toBeNull();
    expect(assign).not.toHaveBeenCalled();
  });

  it('offers sign-out without a subject label when the session has no subject', async () => {
    renderSession(runtime, { accessToken: 'token', subject: null });
    expect(await screen.findByText('No tenants exist yet.')).toBeInTheDocument();

    expect(screen.getByTestId('sign-out')).toBeInTheDocument();
    expect(screen.queryByTestId('session-subject')).not.toBeInTheDocument();
  });

  it('hides the session controls when no one is signed in', async () => {
    renderSession(runtime, { accessToken: null, subject: 'admin' });
    expect(await screen.findByText('No tenants exist yet.')).toBeInTheDocument();

    expect(screen.queryByTestId('sign-out')).not.toBeInTheDocument();
    expect(screen.queryByTestId('session-subject')).not.toBeInTheDocument();
  });
});

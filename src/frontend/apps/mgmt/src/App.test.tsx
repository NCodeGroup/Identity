import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { App } from './App.js';

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
    auth: { accessToken: 'token', subject: 'admin', expiresAt: null, refreshToken: null },
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

describe('App (global ↔ tenant boundary)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
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
              displayName: 'Web App',
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
              domainName: null,
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
    expect(await screen.findByText('Web App')).toBeInTheDocument();
    const requestedUrls = fetchMock.mock.calls.map(([input]) =>
      input instanceof Request ? input.url : String(input),
    );
    expect(requestedUrls).toContain('https://api.test/api/tenants/acme/clients');
  });
});

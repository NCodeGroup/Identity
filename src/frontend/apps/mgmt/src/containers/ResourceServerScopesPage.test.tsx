import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { tenantScope, type ResourceServerResource } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { ResourceServerScopesPage } from './ResourceServerScopesPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const resourceServer: ResourceServerResource = {
  tenantId: 'acme',
  resourceServerId: 'rs-orders',
  identifier: 'https://orders.example.com',
  concurrencyToken: 'c1',
  name: 'Orders API',
  isSystem: false,
  isDisabled: false,
  scopes: [
    { value: 'read:orders', description: 'Read orders', isSystem: false },
    { value: 'openid', description: 'OpenID', isSystem: true },
  ],
};

function renderPage(onBack = vi.fn()) {
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
      <ResourceServerScopesPage
        scope={tenantScope('acme')}
        resourceServer={resourceServer}
        onBack={onBack}
      />
    </Provider>,
  );
  return onBack;
}

type Call = { readonly url: string; readonly method: string; readonly body: string };

function installFetch(handler: (url: string, method: string) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    calls.push({
      url: request.url,
      method: request.method,
      body: request.method === 'GET' ? '' : await request.clone().text(),
    });
    return handler(request.url, request.method);
  });
  vi.stubGlobal('fetch', fetchMock);
  return calls;
}

describe('ResourceServerScopesPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders the owned scopes and hides delete for system scopes', () => {
    installFetch(() => new Response(null, { status: 204 }));
    renderPage();
    expect(screen.getByText('read:orders')).toBeInTheDocument();
    expect(screen.getByTestId('scope-delete-read:orders')).toBeInTheDocument();
    expect(screen.queryByTestId('scope-delete-openid')).not.toBeInTheDocument();
  });

  it('PUTs a description-only update when editing a scope', async () => {
    const calls = installFetch((url, method) => {
      if (
        url.endsWith('/api/tenants/acme/resource-servers/rs-orders/scopes/read%3Aorders') &&
        method === 'PUT'
      ) {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('scope-edit-read:orders'));
    expect(screen.getByTestId('scope-value')).toBeDisabled();
    fireEvent.change(screen.getByTestId('scope-description'), {
      target: { value: 'Read all orders' },
    });
    fireEvent.click(screen.getByTestId('scope-form-submit'));

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true));
    const put = calls.find((c) => c.method === 'PUT');
    expect(JSON.parse(put?.body ?? '{}')).toEqual({ description: 'Read all orders' });
  });

  it('deletes a scope after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (
        url.endsWith('/api/tenants/acme/resource-servers/rs-orders/scopes/read%3Aorders') &&
        method === 'DELETE'
      ) {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('scope-delete-read:orders'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    await waitFor(() => expect(calls.some((c) => c.method === 'DELETE')).toBe(true));
  });

  it('returns to the resource-server list via the back control', () => {
    installFetch(() => new Response(null, { status: 204 }));
    const onBack = renderPage();
    fireEvent.click(screen.getByTestId('scopes-back'));
    expect(onBack).toHaveBeenCalled();
  });
});

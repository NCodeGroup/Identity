import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { tenantScope } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { ResourceServersPage } from './ResourceServersPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage() {
  const store = createAppStore({
    config: { runtime },
    auth: { accessToken: 'token', subject: 'admin', expiresAt: null, refreshToken: null },
  });
  return render(
    <Provider store={store}>
      <ResourceServersPage scope={tenantScope('acme')} />
    </Provider>,
  );
}

const orders = {
  tenantId: 'acme',
  resourceServerId: 'rs-orders',
  identifier: 'https://orders.example.com',
  concurrencyToken: 'c1',
  name: 'Orders API',
  isSystem: false,
  isDisabled: false,
  scopes: [{ value: 'read:orders', description: 'Read orders', isSystem: false }],
};

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
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

describe('ResourceServersPage (tenant-scoped CRUD + scope drill-in)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a resource server under the tenant path', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/resource-servers') && method === 'POST') {
        return jsonResponse({ ...orders, resourceServerId: 'rs-new' }, 201);
      }
      if (url.endsWith('/api/tenants/acme/resource-servers')) {
        return jsonResponse({ items: [orders] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Orders API')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-resource-server'));
    fireEvent.change(screen.getByTestId('resource-server-identifier'), {
      target: { value: 'urn:new' },
    });
    fireEvent.change(screen.getByTestId('resource-server-name'), { target: { value: 'New API' } });
    fireEvent.click(screen.getByTestId('resource-server-form-submit'));

    await waitFor(() =>
      expect(screen.queryByTestId('resource-server-form-submit')).not.toBeInTheDocument(),
    );
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/tenants/acme/resource-servers');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      identifier: 'urn:new',
      name: 'New API',
      isDisabled: false,
      settings: {},
    });
  });

  it('deletes a resource server after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/resource-servers/rs-orders') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants/acme/resource-servers')) {
        return jsonResponse({ items: [orders] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Orders API')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('resource-server-delete-rs-orders'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    await waitFor(() =>
      expect(
        calls.some(
          (c) =>
            c.method === 'DELETE' && c.url.endsWith('/api/tenants/acme/resource-servers/rs-orders'),
        ),
      ).toBe(true),
    );
  });

  it('drills into a resource server and adds a scope', async () => {
    const calls = installFetch((url, method) => {
      if (
        url.endsWith('/api/tenants/acme/resource-servers/rs-orders/scopes') &&
        method === 'POST'
      ) {
        return jsonResponse({ value: 'write:orders', description: null, isSystem: false }, 201);
      }
      if (url.endsWith('/api/tenants/acme/resource-servers')) {
        return jsonResponse({ items: [orders] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-row-rs-orders'));

    expect(await screen.findByText('read:orders')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-scope'));
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: 'write:orders' } });
    fireEvent.click(screen.getByTestId('scope-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('scope-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/tenants/acme/resource-servers/rs-orders/scopes');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({ value: 'write:orders', description: null });

    fireEvent.click(screen.getByTestId('scopes-back'));
    expect(await screen.findByText('Orders API')).toBeInTheDocument();
  });

  it('surfaces a server error in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/resource-servers') && method === 'POST') {
        return jsonResponse({ detail: 'Identifier already in use.' }, 409);
      }
      if (url.endsWith('/api/tenants/acme/resource-servers')) {
        return jsonResponse({ items: [orders] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Orders API')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-resource-server'));
    fireEvent.change(screen.getByTestId('resource-server-identifier'), {
      target: { value: 'urn:dup' },
    });
    fireEvent.change(screen.getByTestId('resource-server-name'), { target: { value: 'Dup' } });
    fireEvent.click(screen.getByTestId('resource-server-form-submit'));

    expect(await screen.findByTestId('resource-server-form-error')).toHaveTextContent(
      'Identifier already in use.',
    );
    expect(screen.getByTestId('resource-server-form-submit')).toBeInTheDocument();
  });
});

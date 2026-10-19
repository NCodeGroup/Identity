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

type Call = {
  readonly url: string;
  readonly method: string;
  readonly body: string;
  readonly ifMatch: string | null;
};

function installFetch(handler: (url: string, method: string) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    calls.push({
      url: request.url,
      method: request.method,
      body: request.method === 'GET' ? '' : await request.clone().text(),
      ifMatch: request.headers.get('if-match'),
    });
    return handler(request.url, request.method);
  });
  vi.stubGlobal('fetch', fetchMock);
  return calls;
}

/** Serves only the tenant's resource-server list; any write is unexpected. */
function listOnly(items: readonly unknown[]) {
  return (url: string, method: string) => {
    if (url.endsWith('/api/tenants/acme/resource-servers') && method === 'GET') {
      return jsonResponse({ items });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  };
}

describe('ResourceServersPage (list states)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when the tenant has no resource servers', async () => {
    installFetch(listOnly([]));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'No resource servers in this tenant yet.',
    );
  });

  it('reports a failure to load the resource-server list', async () => {
    installFetch(() => jsonResponse({ title: 'Internal Server Error' }, 500));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent(
      'Failed to load resource servers.',
    );
  });
});

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

  it('edits a resource server with an If-Match JSON Patch, keeping the identifier immutable', async () => {
    let renamed = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/resource-servers/rs-orders') && method === 'PATCH') {
        renamed = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants/acme/resource-servers')) {
        return jsonResponse({ items: [renamed ? { ...orders, name: 'Orders API v2' } : orders] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-edit-rs-orders'));

    expect(screen.getByRole('dialog', { name: 'Edit resource server' })).toBeInTheDocument();
    expect(screen.getByTestId('resource-server-identifier')).toHaveValue(
      'https://orders.example.com',
    );
    expect(screen.getByTestId('resource-server-identifier')).toBeDisabled();
    expect(screen.getByTestId('resource-server-name')).toHaveValue('Orders API');
    expect(screen.getByTestId('resource-server-form-submit')).toHaveTextContent('Save');

    fireEvent.change(screen.getByTestId('resource-server-name'), {
      target: { value: 'Orders API v2' },
    });
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('resource-server-form-submit'));

    expect(await screen.findByText('Orders API v2')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.queryByTestId('resource-server-form-submit')).not.toBeInTheDocument(),
    );
    const patch = calls.find((c) => c.method === 'PATCH');
    expect(patch?.url).toBe('https://api.test/api/tenants/acme/resource-servers/rs-orders');
    expect(patch?.ifMatch).toBe('c1');
    expect(JSON.parse(patch?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/name', value: 'Orders API v2' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('discards the edit form on cancel', async () => {
    const calls = installFetch(listOnly([orders]));

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-edit-rs-orders'));
    fireEvent.click(screen.getByTestId('resource-server-form-cancel'));

    await waitFor(() =>
      expect(screen.queryByTestId('resource-server-form-submit')).not.toBeInTheDocument(),
    );
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch(listOnly([orders]));

    renderPage();
    fireEvent.click(await screen.findByTestId('create-resource-server'));
    fireEvent.change(screen.getByTestId('resource-server-identifier'), {
      target: { value: 'urn:late' },
    });
    fireEvent.change(screen.getByTestId('resource-server-name'), { target: { value: 'Late' } });
    fireEvent.click(screen.getByTestId('resource-server-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('resource-server-form-submit'));

    await waitFor(() =>
      expect(screen.queryByTestId('resource-server-form-submit')).not.toBeInTheDocument(),
    );
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
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

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/resource-servers/rs-orders') && method === 'DELETE') {
        return jsonResponse({ detail: 'Clients are still granted this API.' }, 409);
      }
      if (url.endsWith('/api/tenants/acme/resource-servers')) {
        return jsonResponse({ items: [orders] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-delete-rs-orders'));
    expect(screen.getByRole('dialog', { name: 'Delete resource server' })).toHaveTextContent(
      'Delete “Orders API”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'Clients are still granted this API.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch(listOnly([orders]));

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-delete-rs-orders'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
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

    fireEvent.click(screen.getByTestId('resource-server-detail-back'));
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

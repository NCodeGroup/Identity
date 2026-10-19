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
    { value: 'write:orders', description: null, isSystem: false },
    { value: 'openid', description: 'OpenID', isSystem: true },
  ],
};

function renderPage(
  onBack = vi.fn(),
  scopes: ResourceServerResource['scopes'] = resourceServer.scopes,
) {
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
        resourceServer={{ ...resourceServer, scopes }}
        onBack={onBack}
      />
    </Provider>,
  );
  return onBack;
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const scopesPath = 'https://api.test/api/tenants/acme/resource-servers/rs-orders/scopes';

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

  it('heads the standalone panel with the resource server and shows the empty state', () => {
    installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage(vi.fn(), []);

    expect(screen.getByRole('heading', { name: 'Scopes' })).toBeInTheDocument();
    expect(screen.getByText('Orders API · https://orders.example.com')).toBeInTheDocument();
    expect(screen.getByTestId('data-empty')).toHaveTextContent(
      'This resource server has no scopes yet.',
    );
  });

  it('renders only the scopes panel when embedded in the detail page', () => {
    installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    render(
      <Provider store={createAppStore({ config: { runtime } })}>
        <ResourceServerScopesPage
          scope={tenantScope('acme')}
          resourceServer={resourceServer}
          embedded
        />
      </Provider>,
    );

    expect(screen.queryByTestId('scopes-back')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Scopes' })).not.toBeInTheDocument();
    expect(screen.getByTestId('create-scope')).toBeInTheDocument();
    expect(screen.getByText('read:orders')).toBeInTheDocument();
  });

  it('adds a scope with a description', async () => {
    const calls = installFetch((url, method) => {
      if (url === scopesPath && method === 'POST') {
        return jsonResponse({ value: 'admin:orders', description: 'Administer', isSystem: false });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('create-scope'));
    expect(screen.getByRole('dialog', { name: 'Add scope' })).toBeInTheDocument();
    expect(screen.getByTestId('scope-value')).toBeEnabled();
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: 'admin:orders' } });
    fireEvent.change(screen.getByTestId('scope-description'), {
      target: { value: 'Administer' },
    });
    fireEvent.click(screen.getByTestId('scope-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('scope-form-submit')).not.toBeInTheDocument());
    expect(calls).toHaveLength(1);
    expect(JSON.parse(calls[0]!.body)).toEqual({
      value: 'admin:orders',
      description: 'Administer',
    });
  });

  it('surfaces a server error in the scope form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url === scopesPath && method === 'POST') {
        return jsonResponse({ detail: 'The scope already exists.' }, 409);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('create-scope'));
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: 'read:orders' } });
    fireEvent.click(screen.getByTestId('scope-form-submit'));

    expect(await screen.findByTestId('scope-form-error')).toHaveTextContent(
      'The scope already exists.',
    );
    expect(screen.getByTestId('scope-form-submit')).toBeEnabled();
  });

  it('prefills an undescribed scope with an empty description and saves it as null', async () => {
    const calls = installFetch((url, method) => {
      if (url === `${scopesPath}/write%3Aorders` && method === 'PUT') {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('scope-edit-write:orders'));

    expect(screen.getByRole('dialog', { name: 'Edit scope' })).toBeInTheDocument();
    expect(screen.getByTestId('scope-value')).toHaveValue('write:orders');
    expect(screen.getByTestId('scope-description')).toHaveValue('');
    fireEvent.click(screen.getByTestId('scope-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('scope-form-submit')).not.toBeInTheDocument());
    expect(JSON.parse(calls[0]!.body)).toEqual({ description: null });
  });

  it('discards the scope form on cancel', async () => {
    const calls = installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('scope-edit-read:orders'));
    fireEvent.click(screen.getByTestId('scope-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('scope-form-submit')).not.toBeInTheDocument());
    expect(calls).toHaveLength(0);
  });

  it('ignores a submit fired while the scope form is closing', async () => {
    const calls = installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('create-scope'));
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: 'late:orders' } });
    fireEvent.click(screen.getByTestId('scope-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('scope-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('scope-form-submit')).not.toBeInTheDocument());
    expect(calls).toHaveLength(0);
  });

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url === `${scopesPath}/read%3Aorders` && method === 'DELETE') {
        return jsonResponse({ detail: 'The scope is still granted to clients.' }, 409);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('scope-delete-read:orders'));
    expect(screen.getByRole('dialog', { name: 'Delete scope' })).toHaveTextContent(
      'Delete scope “read:orders”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The scope is still granted to clients.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('scope-delete-read:orders'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls).toHaveLength(0);
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

import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { GLOBAL_SCOPE, tenantScope, type ManagementScope } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { ClientsPage } from './ClientsPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage(scope: ManagementScope = tenantScope('acme')) {
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
      <ClientsPage scope={scope} />
    </Provider>,
  );
}

const web = { clientId: 'web', tenantId: 'acme', isDisabled: false, concurrencyToken: 'c1' };
const legacy = { clientId: 'legacy', tenantId: 'acme', isDisabled: true, concurrencyToken: 'c2' };

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

/** Serves only the tenant's client list; any write is unexpected. */
function listOnly(items: readonly unknown[]) {
  return (url: string, method: string) => {
    if (url.endsWith('/api/tenants/acme/clients') && method === 'GET') {
      return jsonResponse({ items });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  };
}

describe('ClientsPage (list states and scope)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when the scope has no clients', async () => {
    installFetch(listOnly([]));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'No clients in this scope yet.',
    );
  });

  it('reports a failure to load the client list', async () => {
    installFetch(() => jsonResponse({ title: 'Forbidden' }, 403));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load clients.');
  });

  it('lists global-scope clients from the global path, never a tenant path', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/clients') && method === 'GET') {
        return jsonResponse({ items: [{ ...web, clientId: 'system-cli' }] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage(GLOBAL_SCOPE);

    expect(await screen.findByText('system-cli')).toBeInTheDocument();
    expect(calls.map((c) => c.url)).toEqual(['https://api.test/api/clients']);
  });

  it('drills into a client and returns to the list', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/acme/clients/web/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/api/tenants/acme/clients') && method === 'GET') {
        return jsonResponse({ items: [web] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-row-web'));

    expect(screen.getByRole('heading', { name: 'web' })).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This client has no secrets yet.',
    );

    fireEvent.click(screen.getByTestId('client-detail-back'));

    expect(screen.getByTestId('create-client')).toBeInTheDocument();
    expect(screen.getByTestId('client-row-web')).toBeInTheDocument();
  });
});

describe('ClientsPage (tenant-scoped CRUD wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a client under the tenant path', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/clients') && method === 'POST') {
        return jsonResponse({ ...web, clientId: 'new' }, 201);
      }
      if (url.endsWith('/api/tenants/acme/clients')) {
        return jsonResponse({ items: [web] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('web')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-client'));
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/tenants/acme/clients');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({ isDisabled: false, settings: {} });
  });

  it('edits a client through the form dialog with an If-Match JSON Patch', async () => {
    let disabled = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/clients/web') && method === 'PATCH') {
        disabled = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants/acme/clients')) {
        return jsonResponse({ items: [{ ...web, isDisabled: disabled }] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-edit-web'));

    expect(screen.getByRole('dialog', { name: 'Edit client' })).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'Disabled' })).not.toBeChecked();
    expect(screen.getByTestId('client-form-submit')).toHaveTextContent('Save');

    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() =>
      expect(
        within(screen.getByTestId('client-row-web')).getByText('Disabled'),
      ).toBeInTheDocument(),
    );
    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    const patch = calls.find((c) => c.method === 'PATCH');
    expect(patch?.url).toBe('https://api.test/api/tenants/acme/clients/web');
    expect(patch?.ifMatch).toBe('c1');
    expect(JSON.parse(patch?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('prefills the edit form from a disabled client and discards it on cancel', async () => {
    const calls = installFetch(listOnly([web, legacy]));

    renderPage();
    fireEvent.click(await screen.findByTestId('client-edit-legacy'));

    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
    fireEvent.click(screen.getByTestId('client-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch(listOnly([web]));

    renderPage();
    fireEvent.click(await screen.findByTestId('create-client'));
    fireEvent.click(screen.getByTestId('client-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('deletes a client after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/clients/web') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants/acme/clients')) {
        return jsonResponse({ items: [web] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('web')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('client-delete-web'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    await waitFor(() =>
      expect(
        calls.some((c) => c.method === 'DELETE' && c.url.endsWith('/api/tenants/acme/clients/web')),
      ).toBe(true),
    );
  });

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/clients/web') && method === 'DELETE') {
        return jsonResponse({ detail: 'The client still has active grants.' }, 409);
      }
      if (url.endsWith('/api/tenants/acme/clients')) {
        return jsonResponse({ items: [web] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-delete-web'));
    expect(screen.getByRole('dialog', { name: 'Delete client' })).toHaveTextContent(
      'Delete client “web”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The client still has active grants.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch(listOnly([web]));

    renderPage();
    fireEvent.click(await screen.findByTestId('client-delete-web'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('surfaces a server error in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/clients') && method === 'POST') {
        return jsonResponse({ detail: 'Client quota exceeded.' }, 409);
      }
      if (url.endsWith('/api/tenants/acme/clients')) {
        return jsonResponse({ items: [web] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('web')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-client'));
    fireEvent.click(screen.getByTestId('client-form-submit'));

    expect(await screen.findByTestId('client-form-error')).toHaveTextContent(
      'Client quota exceeded.',
    );
    expect(screen.getByTestId('client-form-submit')).toBeInTheDocument();
  });
});

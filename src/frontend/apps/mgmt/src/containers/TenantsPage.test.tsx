import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { TenantsPage } from './TenantsPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage(onOpenTenant = vi.fn()) {
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
      <TenantsPage onOpenTenant={onOpenTenant} />
    </Provider>,
  );
  return onOpenTenant;
}

const acme = {
  tenantId: 'acme',
  displayName: 'Acme',
  domainName: null,
  isDisabled: false,
  concurrencyToken: 'c1',
};

const globex = {
  tenantId: 'globex',
  displayName: 'Globex',
  domainName: 'globex.test',
  isDisabled: true,
  concurrencyToken: 'c2',
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

describe('TenantsPage (list states)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when no tenants exist', async () => {
    installFetch(() => jsonResponse({ items: [] }));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent('No tenants exist yet.');
  });

  it('reports a failure to load the tenant list', async () => {
    installFetch(() => jsonResponse({ title: 'Internal Server Error' }, 500));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load tenants.');
  });

  it('crosses into a tenant when its row is selected', async () => {
    installFetch(() => jsonResponse({ items: [acme] }));

    const onOpenTenant = renderPage();
    fireEvent.click(await screen.findByTestId('tenant-row-acme'));

    expect(onOpenTenant).toHaveBeenCalledWith('acme');
  });
});

describe('TenantsPage (CRUD wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a tenant through the form dialog', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants') && method === 'POST') {
        return jsonResponse({ ...acme, tenantId: 'new', displayName: 'New Co' }, 201);
      }
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'New Co' } });
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/tenants');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      domainName: null,
      displayName: 'New Co',
      isDisabled: false,
      settings: {},
    });
  });

  it('edits a tenant through the form dialog with an If-Match JSON Patch', async () => {
    let renamed = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme') && method === 'PATCH') {
        renamed = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({
          items: [renamed ? { ...acme, displayName: 'Acme Corp' } : acme, globex],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-edit-acme'));

    expect(screen.getByRole('dialog', { name: 'Edit tenant' })).toBeInTheDocument();
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Acme');
    expect(screen.getByTestId('tenant-domain-name')).toHaveValue('');
    expect(screen.getByTestId('tenant-form-submit')).toHaveTextContent('Save');

    fireEvent.change(screen.getByTestId('tenant-display-name'), {
      target: { value: 'Acme Corp' },
    });
    fireEvent.change(screen.getByTestId('tenant-domain-name'), {
      target: { value: 'id.acme.test' },
    });
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    expect(await screen.findByText('Acme Corp')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    const patch = calls.find((c) => c.method === 'PATCH');
    expect(patch?.url).toBe('https://api.test/api/tenants/acme');
    expect(patch?.ifMatch).toBe('c1');
    expect(JSON.parse(patch?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/domainName', value: 'id.acme.test' },
      { op: 'replace', path: '/displayName', value: 'Acme Corp' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('prefills the edit form from a tenant with a domain and discards it on cancel', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-edit-globex'));

    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Globex');
    expect(screen.getByTestId('tenant-domain-name')).toHaveValue('globex.test');
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();

    fireEvent.click(screen.getByTestId('tenant-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'Late Co' } });
    fireEvent.click(screen.getByTestId('tenant-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('deletes a tenant after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-delete-acme'));
    expect(await screen.findByTestId('confirm-accept')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Delete tenant' })).toHaveTextContent(
      'Delete “Acme”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(calls.some((c) => c.method === 'DELETE' && c.url.endsWith('/api/tenants/acme'))).toBe(
        true,
      ),
    );
  });

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme') && method === 'DELETE') {
        return jsonResponse({ detail: 'The tenant still has clients.' }, 409);
      }
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-delete-acme'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The tenant still has clients.',
    );
    expect(screen.getByTestId('confirm-accept')).toBeEnabled();

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-delete-acme'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('surfaces a server error in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants') && method === 'POST') {
        return jsonResponse({ detail: 'A tenant with that domain already exists.' }, 409);
      }
      if (url.endsWith('/api/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'Dup' } });
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    expect(await screen.findByTestId('tenant-form-error')).toHaveTextContent(
      'A tenant with that domain already exists.',
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeInTheDocument();
  });
});

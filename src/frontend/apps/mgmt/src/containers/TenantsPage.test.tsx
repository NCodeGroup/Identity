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
      <TenantsPage onOpenTenant={vi.fn()} />
    </Provider>,
  );
}

const acme = {
  tenantId: 'acme',
  displayName: 'Acme',
  domainName: null,
  isDisabled: false,
  concurrencyToken: 'c1',
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
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(calls.some((c) => c.method === 'DELETE' && c.url.endsWith('/api/tenants/acme'))).toBe(
        true,
      ),
    );
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

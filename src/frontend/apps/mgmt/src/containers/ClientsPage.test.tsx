import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { tenantScope } from '@ncode/api';
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

function renderPage() {
  const store = createAppStore({
    config: { runtime },
    auth: { accessToken: 'token', subject: 'admin', expiresAt: null, refreshToken: null },
  });
  return render(
    <Provider store={store}>
      <ClientsPage scope={tenantScope('acme')} />
    </Provider>,
  );
}

const web = { clientId: 'web', tenantId: 'acme', isDisabled: false, concurrencyToken: 'c1' };

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

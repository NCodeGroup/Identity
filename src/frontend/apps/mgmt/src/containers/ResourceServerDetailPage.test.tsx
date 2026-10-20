import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { tenantScope, type ResourceServerResource } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { ResourceServerDetailPage } from './ResourceServerDetailPage.js';

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
  concurrencyToken: 'rs-ct',
  name: 'Orders API',
  isSystem: false,
  isDisabled: false,
  scopes: [],
};

function renderPage(onBack = vi.fn(), overrides: Partial<ResourceServerResource> = {}) {
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
      <ResourceServerDetailPage
        scope={tenantScope('acme')}
        resourceServer={{ ...resourceServer, ...overrides }}
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

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const createdSecret = {
  secretId: 's-new',
  concurrencyToken: 'sc1',
  use: null,
  algorithm: null,
  createdWhen: '2026-01-01T00:00:00Z',
  expiresWhen: '2027-01-01T00:00:00Z',
  secretType: 'symmetric',
  keySizeBits: 256,
  secretMaterial: 'one-time-material',
};

describe('ResourceServerDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('opens on the embedded scopes panel and returns to the list via the back control', () => {
    installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    const onBack = renderPage();

    expect(screen.getByRole('heading', { name: 'Orders API' })).toBeInTheDocument();
    expect(screen.getByText('Resource server · https://orders.example.com')).toBeInTheDocument();
    expect(screen.getByTestId('create-scope')).toBeInTheDocument();
    // Embedded, the scopes panel defers the back control and heading to the detail page.
    expect(screen.queryByTestId('scopes-back')).not.toBeInTheDocument();
    expect(screen.getByTestId('data-empty')).toHaveTextContent(
      'This resource server has no scopes yet.',
    );

    fireEvent.click(screen.getByTestId('resource-server-detail-back'));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('lists only the clients that act for the resource server', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/clients?') && method === 'GET') {
        return jsonResponse({
          items: [
            {
              clientId: 'orders-introspector',
              tenantId: 'acme',
              concurrencyToken: 'cc1',
              isDisabled: false,
              resourceServerId: 'rs-orders',
            },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('resource-server-tab-clients'));

    expect(await screen.findByText('orders-introspector')).toBeInTheDocument();
    // Embedded, the clients panel defers its heading to the detail page.
    expect(screen.queryByRole('heading', { name: 'Clients' })).not.toBeInTheDocument();
    expect(calls.find((c) => c.method === 'GET')?.url).toBe(
      'https://api.test/api/tenants/acme/clients?resourceServerId=rs-orders',
    );
  });

  it('creates a client bound to the resource server', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/tenants/acme/clients') && method === 'POST') {
        return jsonResponse(
          {
            clientId: 'orders-introspector',
            tenantId: 'acme',
            concurrencyToken: 'cc1',
            isDisabled: false,
            resourceServerId: 'rs-orders',
          },
          201,
        );
      }
      if (url.includes('/clients?') && method === 'GET') {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('resource-server-tab-clients'));
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'No client acts for this resource server yet.',
    );
    fireEvent.click(screen.getByTestId('create-client'));
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() => {
      const post = calls.find((c) => c.method === 'POST');
      expect(post?.url).toBe('https://api.test/api/tenants/acme/clients');
      expect(JSON.parse(post?.body ?? '{}')).toEqual({
        isDisabled: false,
        resourceServerId: 'rs-orders',
        settings: {},
      });
    });
  });

  it('offers no secrets or clients for a system resource server', () => {
    installFetch((url, method) => {
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage(vi.fn(), { isSystem: true });

    expect(screen.getByTestId('resource-server-tab-scopes')).toBeInTheDocument();
    expect(screen.getByTestId('resource-server-tab-settings')).toBeInTheDocument();
    expect(screen.queryByTestId('resource-server-tab-secrets')).not.toBeInTheDocument();
    expect(screen.queryByTestId('resource-server-tab-clients')).not.toBeInTheDocument();
  });

  it('deletes a resource-server secret under the nested path after confirmation', async () => {
    let deleted = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/resource-servers/rs-orders/secrets/s-old') && method === 'DELETE') {
        deleted = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/resource-servers/rs-orders/secrets')) {
        return jsonResponse({
          concurrencyToken: 'c1',
          secrets: deleted ? [] : [{ ...createdSecret, secretId: 's-old' }],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('resource-server-tab-secrets'));
    fireEvent.click(await screen.findByTestId('secret-delete-s-old'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This resource server has no secrets yet.',
    );
    expect(calls.find((c) => c.method === 'DELETE')?.url).toBe(
      'https://api.test/api/tenants/acme/resource-servers/rs-orders/secrets/s-old',
    );
  });

  it('exports a resource-server secret public key', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({
          secretTypes: [{ secretType: 'ecc', isAsymmetric: true, supportsPublicKeyExport: true }],
        });
      }
      if (url.endsWith('/resource-servers/rs-orders/secrets/s-ecc/public-key')) {
        return jsonResponse({
          secretId: 's-ecc',
          secretType: 'ecc',
          jwk: { kty: 'EC', crv: 'P-256', x: 'eA', y: 'eQ' },
          pem: '-----BEGIN PUBLIC KEY-----\nMFkw\n-----END PUBLIC KEY-----',
        });
      }
      if (url.endsWith('/resource-servers/rs-orders/secrets')) {
        return jsonResponse({
          concurrencyToken: 'c1',
          secrets: [{ ...createdSecret, secretId: 's-ecc', secretType: 'ecc' }],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(screen.getByTestId('resource-server-tab-secrets'));
    fireEvent.click(await screen.findByTestId('secret-export-s-ecc'));

    expect(await screen.findByTestId('export-public-key-jwk-value')).toHaveTextContent(
      '"crv": "P-256"',
    );
    expect(screen.getByTestId('export-public-key-pem-value')).toHaveTextContent('BEGIN PUBLIC KEY');
  });

  it('creates a resource-server secret under the nested path and reveals its material', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/resource-servers/rs-orders/secrets') && method === 'POST') {
        return jsonResponse(createdSecret, 201);
      }
      if (url.endsWith('/resource-servers/rs-orders/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-tab-secrets'));
    fireEvent.click(await screen.findByTestId('create-secret'));
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(await screen.findByTestId('reveal-secret-material')).toHaveTextContent(
      'one-time-material',
    );
  });

  it('saves edited resource-server settings as a JSON Patch diff', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/resource-servers/rs-orders/settings') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/resource-servers/rs-orders/settings')) {
        return jsonResponse({ concurrencyToken: 'sc9', settings: { a: 1 } });
      }
      if (url.endsWith('/resource-servers/rs-orders/effective-settings')) {
        return jsonResponse({ settings: { a: 1, issuer: 'https://op.test' } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('resource-server-tab-settings'));

    const editor = await screen.findByTestId('settings-json');
    fireEvent.change(editor, { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe(
        'https://api.test/api/tenants/acme/resource-servers/rs-orders/settings',
      );
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([{ op: 'replace', path: '/a', value: 2 }]);
    });
  });
});

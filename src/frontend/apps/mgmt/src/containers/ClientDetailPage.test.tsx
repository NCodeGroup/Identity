import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import { tenantScope, type ClientResource } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { ClientDetailPage } from './ClientDetailPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const client: ClientResource = {
  clientId: 'web',
  tenantId: 'acme',
  concurrencyToken: 'c1',
  isDisabled: false,
  resourceServerId: null,
};

function renderPage(overrides: Partial<ClientResource> = {}, onBack = vi.fn()) {
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
      <ClientDetailPage
        scope={tenantScope('acme')}
        client={{ ...client, ...overrides }}
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

describe('ClientDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('identifies an active client and returns to the list via the back control', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    const onBack = renderPage();

    expect(screen.getByRole('heading', { name: 'web' })).toBeInTheDocument();
    expect(screen.getByText('Client · Active')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This client has no secrets yet.',
    );

    fireEvent.click(screen.getByTestId('client-detail-back'));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('labels a disabled client', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage({ isDisabled: true });

    expect(screen.getByText('Client · Disabled')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
  });

  it('names the resource server a bound client acts for', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage({ resourceServerId: 'rs-orders' });

    expect(
      screen.getByText('Client · Active · Acts for resource server rs-orders'),
    ).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
  });

  it('deletes a client secret under the client path after confirmation', async () => {
    let deleted = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/clients/web/secrets/s-old') && method === 'DELETE') {
        deleted = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({
          concurrencyToken: 'c1',
          secrets: deleted ? [] : [{ ...createdSecret, secretId: 's-old' }],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('secret-delete-s-old'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
    expect(calls.find((c) => c.method === 'DELETE')?.url).toBe(
      'https://api.test/api/tenants/acme/clients/web/secrets/s-old',
    );
  });

  it('creates a secret and reveals its one-time material', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/clients/web/secrets') && method === 'POST') {
        return jsonResponse(createdSecret, 201);
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('create-secret'));
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(await screen.findByTestId('reveal-secret-material')).toHaveTextContent(
      'one-time-material',
    );
    expect(screen.getByTestId('reveal-secret-id')).toHaveTextContent('s-new');
  });

  it('exports an asymmetric secret public key as JWK and PEM', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({
          secretTypes: [{ secretType: 'rsa', isAsymmetric: true, supportsPublicKeyExport: true }],
        });
      }
      if (url.endsWith('/clients/web/secrets/s-rsa/public-key')) {
        return jsonResponse({
          secretId: 's-rsa',
          secretType: 'rsa',
          jwk: { kty: 'RSA', n: 'bW9k', e: 'AQAB' },
          pem: '-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----',
        });
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({
          concurrencyToken: 'c1',
          secrets: [
            {
              secretId: 's-rsa',
              concurrencyToken: 'sc1',
              use: null,
              algorithm: null,
              createdWhen: '2026-01-01T00:00:00Z',
              expiresWhen: '2099-01-01T00:00:00Z',
              secretType: 'rsa',
              keySizeBits: 2048,
            },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('secret-export-s-rsa'));

    expect(await screen.findByTestId('export-public-key-pem-value')).toHaveTextContent(
      'BEGIN PUBLIC KEY',
    );
    expect(screen.getByTestId('export-public-key-jwk-value')).toHaveTextContent('"kty": "RSA"');
  });

  it('saves edited settings as a JSON Patch diff on the settings tab', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/clients/web/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/clients/web/settings') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/clients/web/settings')) {
        return jsonResponse({ concurrencyToken: 'sc9', settings: { a: 1 } });
      }
      if (url.endsWith('/clients/web/effective-settings')) {
        return jsonResponse({ settings: { a: 1, issuer: 'https://op.test' } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-tab-settings'));

    const editor = await screen.findByTestId('settings-json');
    fireEvent.change(editor, { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe('https://api.test/api/tenants/acme/clients/web/settings');
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([{ op: 'replace', path: '/a', value: 2 }]);
    });
  });
});

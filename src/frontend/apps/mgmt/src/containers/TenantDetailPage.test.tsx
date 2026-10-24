import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { TenantDetailPage } from './TenantDetailPage.js';
import type { TenantOwnSurface } from '../routes.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage(surface: TenantOwnSurface = 'secrets') {
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
      <TenantDetailPage tenantId="acme" surface={surface} />
    </Provider>,
  );
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

describe('TenantDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('identifies the tenant whose own secrets are listed', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();

    expect(screen.getByRole('heading', { name: 'Tenant' })).toBeInTheDocument();
    expect(screen.getByText('acme')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This tenant has no secrets yet.',
    );
  });

  it('deletes a tenant secret under the tenant path after confirmation', async () => {
    let deleted = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/acme/secrets/s-old') && method === 'DELETE') {
        deleted = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
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
      'https://api.test/api/tenants/acme/secrets/s-old',
    );
  });

  it('exports a tenant secret public key', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({
          secretTypes: [{ secretType: 'rsa', isAsymmetric: true, supportsPublicKeyExport: true }],
        });
      }
      if (url.endsWith('/api/tenants/acme/secrets/s-rsa/public-key')) {
        return jsonResponse({
          secretId: 's-rsa',
          secretType: 'rsa',
          jwk: { kty: 'RSA', n: 'bW9k', e: 'AQAB' },
          pem: '-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----',
        });
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
        return jsonResponse({
          concurrencyToken: 'c1',
          secrets: [{ ...createdSecret, secretId: 's-rsa', secretType: 'rsa', keySizeBits: 2048 }],
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

  it('creates a tenant secret under the tenant path and reveals its material', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/secrets') && method === 'POST') {
        return jsonResponse(createdSecret, 201);
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
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
  });

  it('saves edited tenant settings as a JSON Patch diff on the settings tab', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/api/tenants/acme/settings') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/tenants/acme/settings')) {
        return jsonResponse({ concurrencyToken: 'sc9', settings: { a: 1 } });
      }
      if (url.endsWith('/api/tenants/acme/effective-settings')) {
        return jsonResponse({ settings: { a: 1, issuer: 'https://op.test' } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage('settings');

    const editor = await screen.findByTestId('settings-json');
    fireEvent.change(editor, { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe('https://api.test/api/tenants/acme/settings');
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([{ op: 'replace', path: '/a', value: 2 }]);
    });
  });

  it('shows the tenant usage ledger on the usage tab and narrows it to a chosen range', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.includes('/api/tenants/acme/usage?from=2026-10-21&to=2026-10-21')) {
        return jsonResponse({
          tenantId: 'acme',
          from: '2026-10-21',
          to: '2026-10-21',
          entries: [{ day: '2026-10-21', meter: 'requests', count: 3 }],
        });
      }
      if (url.includes('/api/tenants/acme/usage?from=2026-10-21')) {
        return jsonResponse({
          tenantId: 'acme',
          from: '2026-10-21',
          to: '2026-10-21',
          entries: [],
        });
      }
      if (url.endsWith('/api/tenants/acme/usage')) {
        return jsonResponse({
          tenantId: 'acme',
          from: '2026-09-21',
          to: '2026-10-21',
          entries: [
            { day: '2026-10-21', meter: 'requests', count: 42 },
            { day: '2026-10-21', meter: 'token_requests', count: 7 },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage('usage');

    expect(await screen.findByTestId('usage-row-2026-10-21')).toHaveTextContent('2026-10-21427');
    expect(screen.getByTestId('usage-range')).toHaveTextContent('2026-09-21 to 2026-10-21');

    fireEvent.change(screen.getByTestId('usage-from'), { target: { value: '2026-10-21' } });

    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'No usage recorded in this range.',
    );
    expect(
      calls.some((c) => c.url === 'https://api.test/api/tenants/acme/usage?from=2026-10-21'),
    ).toBe(true);

    fireEvent.change(screen.getByTestId('usage-to'), { target: { value: '2026-10-21' } });

    expect(await screen.findByTestId('usage-row-2026-10-21')).toHaveTextContent('2026-10-213');
    expect(
      calls.some(
        (c) => c.url === 'https://api.test/api/tenants/acme/usage?from=2026-10-21&to=2026-10-21',
      ),
    ).toBe(true);
  });

  it('shows the server refusal of an invalid usage range', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/acme/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.includes('/api/tenants/acme/usage')) {
        return jsonResponse(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: {
              from: ["The range must run forward from 'from' to 'to' and span at most 366 days."],
            },
          },
          400,
        );
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage('usage');

    expect(await screen.findByTestId('data-error')).toBeInTheDocument();
  });
});

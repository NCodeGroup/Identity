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
};

function renderPage() {
  const store = createAppStore({
    config: { runtime },
    auth: { accessToken: 'token', subject: 'admin', expiresAt: null, refreshToken: null },
  });
  return render(
    <Provider store={store}>
      <ClientDetailPage scope={tenantScope('acme')} client={client} onBack={vi.fn()} />
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

describe('ClientDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a secret and reveals its one-time material', async () => {
    installFetch((url, method) => {
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

  it('saves edited settings as a JSON Patch diff on the settings tab', async () => {
    const calls = installFetch((url, method) => {
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

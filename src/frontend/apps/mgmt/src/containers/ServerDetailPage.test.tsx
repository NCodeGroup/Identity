import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { ServerResource } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { ServerDetailPage } from './ServerDetailPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const server: ServerResource = { serverId: 'root', concurrencyToken: 'sc1', displayName: 'Root' };

function renderPage(onBack = vi.fn()) {
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
      <ServerDetailPage server={server} onBack={onBack} />
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

const rsaSecret = {
  secretId: 's-rsa',
  concurrencyToken: 'sc1',
  use: 'sig',
  algorithm: 'RS256',
  createdWhen: '2026-01-01T00:00:00Z',
  expiresWhen: '2099-01-01T00:00:00Z',
  secretType: 'rsa',
  keySizeBits: 2048,
};

const capabilities = {
  secretTypes: [{ secretType: 'rsa', isAsymmetric: true, supportsPublicKeyExport: true }],
};

describe('ServerDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('identifies the server and returns to the list via the back control', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse(capabilities);
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    const onBack = renderPage();

    expect(screen.getByRole('heading', { name: 'Root' })).toBeInTheDocument();
    expect(screen.getByText('Server · root')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This server has no secrets yet.',
    );

    fireEvent.click(screen.getByTestId('server-detail-back'));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('creates a server secret under the server path and reveals its material', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse(capabilities);
      }
      if (url.endsWith('/api/servers/root/secrets') && method === 'POST') {
        return jsonResponse(
          { ...rsaSecret, secretId: 's-new', secretType: 'symmetric', secretMaterial: 'one-time' },
          201,
        );
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('create-secret'));
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(await screen.findByTestId('reveal-secret-material')).toHaveTextContent('one-time');
    expect(calls.find((c) => c.method === 'POST')?.url).toBe(
      'https://api.test/api/servers/root/secrets',
    );
  });

  it('deletes a server secret after confirmation', async () => {
    let deleted = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse(capabilities);
      }
      if (url.endsWith('/api/servers/root/secrets/s-rsa') && method === 'DELETE') {
        deleted = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: deleted ? [] : [rsaSecret] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('secret-delete-s-rsa'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
    expect(calls.some((c) => c.method === 'DELETE')).toBe(true);
  });

  it('exports a server secret public key', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse(capabilities);
      }
      if (url.endsWith('/api/servers/root/secrets/s-rsa/public-key')) {
        return jsonResponse({
          secretId: 's-rsa',
          secretType: 'rsa',
          jwk: { kty: 'RSA', n: 'bW9k', e: 'AQAB' },
          pem: '-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----',
        });
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [rsaSecret] });
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

  it('shows the server settings against the effective settings on the settings tab', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse(capabilities);
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/api/servers/root/settings')) {
        return jsonResponse({ concurrencyToken: 'st1', settings: { a: 1 } });
      }
      if (url.endsWith('/api/servers/root/effective-settings')) {
        return jsonResponse({ settings: { a: 1, b: true } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('server-tab-settings'));

    await waitFor(() => {
      expect(screen.getByTestId('settings-json')).toHaveValue(JSON.stringify({ a: 1 }, null, 2));
      expect(screen.getByTestId('effective-settings-json')).toHaveValue(
        JSON.stringify({ a: 1, b: true }, null, 2),
      );
    });
  });
});

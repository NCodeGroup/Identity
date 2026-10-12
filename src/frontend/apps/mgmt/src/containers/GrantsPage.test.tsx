import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { GrantsPage } from './GrantsPage.js';

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
      <GrantsPage tenantId="acme" />
    </Provider>,
  );
}

const grant = {
  grantId: 'g-1',
  grantType: 'authorization_code',
  tenantId: 'acme',
  clientId: 'web',
  subjectId: 'user-1',
  createdWhen: '2026-01-01T12:00:00Z',
  expiresWhen: '2099-01-01T12:00:00Z',
  revokedWhen: null,
  consumedWhen: null,
};

type Call = { readonly url: string; readonly method: string };

function installFetch(handler: (url: string, method: string) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    calls.push({ url: request.url, method: request.method });
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

describe('GrantsPage (tenant-scoped revocation wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('revokes a single grant after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/g-1') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [grant] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-1')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('grant-revoke-g-1'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(
        calls.find((c) => c.method === 'DELETE' && c.url.includes('/grants/g-1')),
      ).toBeDefined(),
    );
  });

  it('bulk-revokes with the active filters once a filter is set', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'DELETE') {
        return jsonResponse({ revoked: 2 });
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [grant] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-1')).toBeInTheDocument();

    expect(screen.getByTestId('revoke-matching-grants')).toBeDisabled();
    fireEvent.change(screen.getByTestId('grant-filter-client').querySelector('input')!, {
      target: { value: 'web' },
    });
    fireEvent.click(screen.getByTestId('revoke-matching-grants'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => {
      const del = calls.find((c) => c.method === 'DELETE');
      expect(del?.url).toContain('clientId=web');
    });
  });

  it('mints an initial access token and reveals its one-time value', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/initial-access-tokens') && method === 'POST') {
        return jsonResponse(
          {
            initialAccessToken: 'iat-one-time',
            grantId: 'g-iat',
            tenantId: 'acme',
            description: 'Partner X',
            createdWhen: '2026-01-01T00:00:00Z',
            expiresWhen: null,
          },
          201,
        );
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    await screen.findByTestId('mint-initial-access-token');

    fireEvent.click(screen.getByTestId('mint-initial-access-token'));
    fireEvent.change(screen.getByTestId('iat-description'), { target: { value: 'Partner X' } });
    fireEvent.click(screen.getByTestId('iat-form-submit'));

    expect(await screen.findByTestId('reveal-iat-token')).toHaveTextContent('iat-one-time');
    expect(
      calls.find((c) => c.method === 'POST' && c.url.includes('/grants/initial-access-tokens')),
    ).toBeDefined();
  });
});

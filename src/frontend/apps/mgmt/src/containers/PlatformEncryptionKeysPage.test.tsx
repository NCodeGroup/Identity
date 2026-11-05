import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { PlatformEncryptionKeysPage } from './PlatformEncryptionKeysPage.js';

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
  render(
    <Provider store={store}>
      <PlatformEncryptionKeysPage />
    </Provider>,
  );
}

function installFetch(handler: (url: string, method: string) => Response) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    return handler(request.url, request.method);
  });
  vi.stubGlobal('fetch', fetchMock);
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('PlatformEncryptionKeysPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows the platform encryption keys', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/platform/encryption-keys')) {
        return jsonResponse({
          recoveryConfigured: false,
          rootKey: null,
          rootKeyProviders: [],
          keys: [
            {
              keyId: 'k-1',
              version: 1,
              state: 'active',
              createdWhen: '2026-10-01T09:00:00Z',
              retiredWhen: null,
              rootKeyProvider: 'local',
              rootKeyId: 'root-1',
              recoveryKeyId: null,
              rootKeyReference: null,
            },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();

    expect(screen.getByRole('heading', { name: 'Platform encryption keys' })).toBeInTheDocument();
    expect(await screen.findByTestId('encryption-key-row-k-1')).toBeInTheDocument();
    expect(screen.getByTestId('encryption-keys-no-recovery')).toBeInTheDocument();
  });
});

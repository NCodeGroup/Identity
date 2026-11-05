import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { EncryptionKeyOwner, EncryptionKeyResource, EncryptionKeysResource } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { OwnerEncryptionKeysTab } from './OwnerEncryptionKeysTab.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderTab(owner: EncryptionKeyOwner, ownerLabel: string) {
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
      <OwnerEncryptionKeysTab owner={owner} ownerLabel={ownerLabel} />
    </Provider>,
  );
}

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

const activeKey: EncryptionKeyResource = {
  keyId: 'k-2',
  version: 2,
  state: 'active',
  createdWhen: '2026-10-01T09:00:00Z',
  retiredWhen: null,
  rootKeyProvider: 'aws-kms',
  rootKeyId: 'arn:aws:kms:key',
  recoveryKeyId: 'recovery-1',
};

const retiredKey: EncryptionKeyResource = {
  ...activeKey,
  keyId: 'k-1',
  version: 1,
  state: 'retired',
  retiredWhen: '2026-10-01T09:00:00Z',
  recoveryKeyId: null,
};

const keys: EncryptionKeysResource = {
  keys: [activeKey, retiredKey],
  recoveryConfigured: true,
};

describe('OwnerEncryptionKeysTab', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('lists the keys of a tenant from its encryption-keys path', async () => {
    const calls = installFetch(() => jsonResponse(keys));

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');

    expect(await screen.findByTestId('encryption-key-row-k-2')).toBeInTheDocument();
    expect(screen.getByTestId('encryption-key-state-k-1')).toHaveTextContent('retired');
    expect(calls[0]?.url).toBe('https://api.test/api/tenants/acme/encryption-keys');
    expect(screen.queryByTestId('encryption-keys-no-recovery')).not.toBeInTheDocument();
  });

  it('lists the keys of the server from its encryption-keys path', async () => {
    const calls = installFetch(() => jsonResponse(keys));

    renderTab({ kind: 'server', serverId: 'root' }, 'server');

    expect(await screen.findByTestId('encryption-key-row-k-2')).toBeInTheDocument();
    expect(calls[0]?.url).toBe('https://api.test/api/servers/root/encryption-keys');
  });

  it('warns when new keys are not wrapped under a recovery key', async () => {
    installFetch(() => jsonResponse({ keys: [activeKey], recoveryConfigured: false }));

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');

    expect(await screen.findByTestId('encryption-keys-no-recovery')).toHaveTextContent(
      'this tenant stores',
    );
  });

  it('reports a load failure', async () => {
    installFetch(() => jsonResponse({ title: 'boom', status: 500 }, 500));

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');

    expect(await screen.findByTestId('data-error')).toHaveTextContent(
      'Failed to load encryption keys.',
    );
  });

  it('rekeys after confirmation and reloads the keys', async () => {
    let rekeyed = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/encryption-keys/rekey') && method === 'POST') {
        rekeyed = true;
        return jsonResponse({
          activeKey: { ...activeKey, keyId: 'k-3', version: 3 },
          retiredKey: { ...activeKey, state: 'retired' },
        });
      }
      return jsonResponse(
        rekeyed
          ? {
              keys: [{ ...activeKey, keyId: 'k-3', version: 3 }, activeKey],
              recoveryConfigured: true,
            }
          : keys,
      );
    });

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    expect(await screen.findByTestId('encryption-key-row-k-3')).toBeInTheDocument();
    expect(
      calls.some(
        (c) =>
          c.method === 'POST' &&
          c.url === 'https://api.test/api/tenants/acme/encryption-keys/rekey',
      ),
    ).toBe(true);
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('does not rekey when the confirmation is cancelled', async () => {
    const calls = installFetch(() => jsonResponse(keys));

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));
    fireEvent.click(await screen.findByTestId('confirm-cancel'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('shows the explanation of a rekey that lost a race and keeps the dialog open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/encryption-keys/rekey') && method === 'POST') {
        return jsonResponse(
          {
            title: 'Precondition Failed',
            status: 412,
            detail:
              'The resource was changed by another request after this request read it. Read it again and retry.',
          },
          412,
        );
      }
      return jsonResponse(keys);
    });

    renderTab({ kind: 'server', serverId: 'root' }, 'server');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'changed by another request',
    );
  });
});

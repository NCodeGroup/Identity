import { describe, expect, it, beforeEach, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore, managementApi } from '@ncode/store';
import type { SecretOwner, SecretResource } from '@ncode/api';
import type { BffRuntimeConfig } from '@ncode/config';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const symmetricSecret: SecretResource = {
  secretId: 's-sym',
  keyId: 's-sym',
  concurrencyToken: 'sc1',
  use: null,
  algorithm: null,
  createdWhen: '2026-01-01T00:00:00Z',
  activatesWhen: '2026-01-01T00:00:00Z',
  retiresWhen: '2099-01-01T00:00:00Z',
  expiresWhen: '2099-01-01T00:00:00Z',
  secretType: 'symmetric',
  keySizeBits: 256,
  isVerifier: true,
  remoteKeyProvider: null,
  managedBy: null,
};

type OwnerCase = {
  readonly key: string;
  readonly owner: SecretOwner;
  readonly rotates: boolean;
  readonly bindsRemoteKeys: boolean;
};

const cases: readonly OwnerCase[] = [
  {
    key: 'tenant',
    owner: { kind: 'tenant', tenantId: 'acme' },
    rotates: false,
    bindsRemoteKeys: true,
  },
  {
    key: 'client',
    owner: { kind: 'client', tenantId: 'acme', clientId: 'web' },
    rotates: true,
    bindsRemoteKeys: false,
  },
  {
    key: 'resource-server',
    owner: { kind: 'resource-server', tenantId: 'acme', resourceServerId: 'rs-1' },
    rotates: false,
    bindsRemoteKeys: false,
  },
];

describe('OwnerSecretsTab offers the actions each owner supports', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.stubGlobal('fetch', vi.fn());
  });

  async function renderTab(key: string, owner: SecretOwner) {
    const store = createAppStore({ config: { runtime } });
    await store.dispatch(
      managementApi.util.upsertQueryData('getCapabilities', undefined, {
        secretTypes: [
          { secretType: 'symmetric', isAsymmetric: false, supportsPublicKeyExport: false },
        ],
        remoteKeyProviders: [
          {
            name: 'aws-kms',
            createsKeys: true,
            algorithms: [
              {
                algorithm: 'ES256',
                use: 'sig',
                secretType: 'ecc',
                keySizeBits: 256,
                curve: 'P-256',
              },
            ],
          },
        ],
      }),
    );
    await store.dispatch(
      managementApi.util.upsertQueryData('listSecrets', owner, {
        concurrencyToken: 'c1',
        secrets: [symmetricSecret],
      }),
    );

    render(
      <Provider store={store}>
        <OwnerSecretsTab owner={owner} ownerLabel={key} />
      </Provider>,
    );

    await screen.findByTestId('secret-delete-s-sym');
  }

  it.each(cases)('$key owner offers rotation: $rotates', async ({ key, owner, rotates }) => {
    await renderTab(key, owner);
    expect(screen.queryByTestId('secret-rotate-s-sym') !== null).toBe(rotates);
  });

  it.each(cases)(
    '$key owner offers binding a KMS key: $bindsRemoteKeys',
    async ({ key, owner, bindsRemoteKeys }) => {
      await renderTab(key, owner);
      expect(screen.queryByTestId('bind-remote-secret') !== null).toBe(bindsRemoteKeys);
    },
  );

  describe('a tenant holds keys in a key management service', () => {
    const tenant: SecretOwner = { kind: 'tenant', tenantId: 'acme' };

    type Call = { readonly url: string; readonly method: string; readonly body: string };

    /** Answers every request with the tenant's secrets, and records each request that was made. */
    function installFetch(): Call[] {
      const calls: Call[] = [];
      vi.stubGlobal(
        'fetch',
        vi.fn(async (input: RequestInfo | URL) => {
          const request = input as Request;
          calls.push({
            url: request.url,
            method: request.method,
            body: await request.clone().text(),
          });
          const body =
            request.method === 'GET'
              ? { concurrencyToken: 'c2', secrets: [symmetricSecret] }
              : symmetricSecret;
          return new Response(JSON.stringify(body), {
            status: request.method === 'POST' ? 201 : 200,
            headers: { 'content-type': 'application/json' },
          });
        }),
      );
      return calls;
    }

    it('creates a remote key through the tenant remote secrets path', async () => {
      const calls = installFetch();
      await renderTab('tenant', tenant);

      fireEvent.click(screen.getByTestId('create-secret'));
      fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: 'aws-kms' } });
      fireEvent.change(screen.getByTestId('secret-remote-algorithm'), {
        target: { value: 'ES256|ecc|256|P-256' },
      });
      fireEvent.click(screen.getByTestId('secret-form-submit'));

      await waitFor(() =>
        expect(screen.queryByTestId('secret-form-submit')).not.toBeInTheDocument(),
      );
      const post = calls.find((call) => call.method === 'POST');
      expect(post?.url).toBe('https://api.test/api/tenants/acme/secrets/remote');
      expect(JSON.parse(post?.body ?? '{}')).toMatchObject({
        remoteKeyProvider: 'aws-kms',
        secretType: 'ecc',
        keySizeBits: 256,
        curve: 'P-256',
        algorithm: 'ES256',
      });
    });

    it('binds a key through the tenant remote import path', async () => {
      const calls = installFetch();
      await renderTab('tenant', tenant);

      fireEvent.click(screen.getByTestId('bind-remote-secret'));
      fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'key-1' } });
      fireEvent.change(screen.getByTestId('remote-bind-algorithm'), {
        target: { value: 'ES256|ecc|256|P-256' },
      });
      fireEvent.click(screen.getByTestId('remote-bind-submit'));

      await waitFor(() =>
        expect(screen.queryByTestId('remote-bind-submit')).not.toBeInTheDocument(),
      );
      const post = calls.find((call) => call.method === 'POST');
      expect(post?.url).toBe('https://api.test/api/tenants/acme/secrets/remote/import');
      expect(JSON.parse(post?.body ?? '{}')).toMatchObject({
        remoteKeyProvider: 'aws-kms',
        remoteKeyId: 'key-1',
        secretType: 'ecc',
        keySizeBits: 256,
        curve: 'P-256',
        algorithm: 'ES256',
      });
    });
  });
});

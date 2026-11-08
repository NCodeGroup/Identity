import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
};

const cases: readonly OwnerCase[] = [
  { key: 'tenant', owner: { kind: 'tenant', tenantId: 'acme' }, rotates: false },
  {
    key: 'client',
    owner: { kind: 'client', tenantId: 'acme', clientId: 'web' },
    rotates: true,
  },
  {
    key: 'resource-server',
    owner: { kind: 'resource-server', tenantId: 'acme', resourceServerId: 'rs-1' },
    rotates: false,
  },
];

describe('OwnerSecretsTab offers rotation only for a client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.stubGlobal('fetch', vi.fn());
  });

  it.each(cases)('$key owner offers rotation: $rotates', async ({ key, owner, rotates }) => {
    const store = createAppStore({ config: { runtime } });
    await store.dispatch(
      managementApi.util.upsertQueryData('getCapabilities', undefined, {
        secretTypes: [
          { secretType: 'symmetric', isAsymmetric: false, supportsPublicKeyExport: false },
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
    expect(screen.queryByTestId('secret-rotate-s-sym') !== null).toBe(rotates);
  });
});

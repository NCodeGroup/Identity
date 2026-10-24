import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore, managementApi } from '@ncode/store';
import type { ManagementOwner, SecretResource } from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const symmetricSecret: SecretResource = {
  secretId: 's-sym',
  keyId: 's-sym',
  concurrencyToken: 'sc1',
  use: null,
  algorithm: null,
  createdWhen: '2026-01-01T00:00:00Z',
  expiresWhen: '2099-01-01T00:00:00Z',
  secretType: 'symmetric',
  keySizeBits: 256,
};

type OwnerCase = {
  readonly key: string;
  readonly owner: ManagementOwner;
  readonly rotates: boolean;
};

const cases: readonly OwnerCase[] = [
  { key: 'server', owner: { kind: 'server', serverId: 'root' }, rotates: false },
  { key: 'tenant', owner: { kind: 'tenant', tenantId: 'acme' }, rotates: false },
  {
    key: 'client',
    owner: { kind: 'client', scope: { kind: 'tenant', tenantId: 'acme' }, clientId: 'web' },
    rotates: true,
  },
  {
    key: 'resource-server',
    owner: {
      kind: 'resource-server',
      scope: { kind: 'tenant', tenantId: 'acme' },
      resourceServerId: 'rs-1',
    },
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

import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type {
  EncryptionKeyOwner,
  EncryptionKeyResource,
  EncryptionKeysResource,
  EncryptionRootKeyResource,
} from '@ncode/api';
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

type Call = { readonly url: string; readonly method: string; readonly body: string };

function installFetch(handler: (url: string, method: string) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    calls.push({
      url: request.url,
      method: request.method,
      body: await request.clone().text(),
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

const activeKey: EncryptionKeyResource = {
  keyId: 'k-2',
  version: 2,
  state: 'active',
  createdWhen: '2026-10-01T09:00:00Z',
  retiredWhen: null,
  rootKeyProvider: 'aws-kms',
  rootKeyId: 'arn:aws:kms:key',
  recoveryKeyId: 'recovery-1',
  rootKeyReference: null,
  rootKeyAccessIdentity: null,
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
  rootKey: null,
  rootKeyProviders: [],
  rootKeyAccessBinding: null,
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

  it('lists the keys of the platform from its encryption-keys path', async () => {
    const calls = installFetch(() => jsonResponse(keys));

    renderTab({ kind: 'platform' }, 'platform');

    expect(await screen.findByTestId('encryption-key-row-k-2')).toBeInTheDocument();
    expect(calls[0]?.url).toBe('https://api.test/api/platform/encryption-keys');
  });

  it('warns when new keys are not wrapped under a recovery key', async () => {
    installFetch(() =>
      jsonResponse({
        ...keys,
        keys: [activeKey],
        recoveryConfigured: false,
      }),
    );

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');

    expect(await screen.findByTestId('encryption-keys-no-recovery')).toHaveTextContent(
      'this tenant protects',
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
              ...keys,
              keys: [{ ...activeKey, keyId: 'k-3', version: 3 }, activeKey],
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

  it('tells a tenant that its values are rewrapped in the background', async () => {
    installFetch(() => jsonResponse(keys));

    renderTab({ kind: 'tenant', tenantId: 'acme' }, 'tenant');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(
      'Retire the active encryption key of this tenant and start the next version? The values the old key protects are rewrapped under the new one in the background, and the old key is destroyed once nothing uses it.',
    );
  });

  it('tells the platform what its next version protects and when the old key is destroyed', async () => {
    installFetch(() => jsonResponse(keys));

    renderTab({ kind: 'platform' }, 'platform');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(
      'The next version protects the Data Protection keys and host data created afterwards.',
    );
    expect(dialog).toHaveTextContent(
      'Existing Data Protection keys keep naming the old version until they expire, and the old key is destroyed once nothing names it.',
    );
    expect(dialog).not.toHaveTextContent('rewrapped');
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

    renderTab({ kind: 'platform' }, 'platform');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'changed by another request',
    );
  });

  it('shows how long to wait when a rekey comes too soon', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/encryption-keys/rekey') && method === 'POST') {
        return jsonResponse(
          {
            title: 'Conflict',
            status: 409,
            detail: 'The last rotation of this master key has not reached every replica yet.',
            retryAfterSeconds: 240,
          },
          409,
        );
      }
      return jsonResponse(keys);
    });

    renderTab({ kind: 'platform' }, 'platform');
    await screen.findByTestId('encryption-key-row-k-2');

    fireEvent.click(screen.getByTestId('encryption-keys-rekey'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent('Try again in 4 minutes.');
  });
});

describe('OwnerEncryptionKeysTab root key', () => {
  const tenant: EncryptionKeyOwner = { kind: 'tenant', tenantId: 'acme' };
  const rootKeyUrl = 'https://api.test/api/tenants/acme/encryption-keys/root-key';
  const providers = ['aws-kms', 'google-cloud-kms'];

  const rootKey: EncryptionRootKeyResource = {
    provider: 'aws-kms',
    keyReference: 'arn:aws:kms:key/abc',
    accessIdentity: null,
    state: 'active',
    createdWhen: '2026-10-01T09:00:00Z',
    stateChangedWhen: '2026-10-01T09:00:00Z',
    validatedWhen: '2026-10-01T10:00:00Z',
    failedWhen: null,
    failure: null,
  };

  const withoutRootKey: EncryptionKeysResource = { ...keys, rootKeyProviders: providers };
  const withRootKey: EncryptionKeysResource = {
    ...keys,
    rootKey,
    rootKeyProviders: providers,
  };

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('says no provider is available and offers no action when a tenant has none', async () => {
    installFetch(() => jsonResponse({ ...keys, rootKeyProviders: [] }));

    renderTab(tenant, 'tenant');

    expect(await screen.findByTestId('root-key-none')).toHaveTextContent(
      'No root key provider is available to this tenant.',
    );
    expect(screen.queryByTestId('root-key-set')).not.toBeInTheDocument();
  });

  it('shows no root key section for the platform', async () => {
    installFetch(() => jsonResponse({ ...keys, rootKeyProviders: [] }));

    renderTab({ kind: 'platform' }, 'platform');

    expect(await screen.findByTestId('encryption-key-row-k-2')).toBeInTheDocument();
    expect(screen.queryByTestId('root-key-card')).not.toBeInTheDocument();
  });

  it('shows the root key, replaces the recovery warning and labels each key by its root', async () => {
    installFetch(() =>
      jsonResponse({
        keys: [{ ...activeKey, rootKeyReference: rootKey.keyReference }, retiredKey],
        recoveryConfigured: false,
        rootKey,
        rootKeyProviders: providers,
        rootKeyAccessBinding: null,
      }),
    );

    renderTab(tenant, 'tenant');

    expect(await screen.findByTestId('root-key-provider')).toHaveTextContent('aws-kms');
    expect(screen.getByTestId('root-key-reference')).toHaveTextContent('arn:aws:kms:key/abc');
    expect(screen.getByTestId('root-key-state')).toHaveTextContent('active');
    expect(screen.getByTestId('encryption-keys-root-key-only')).toHaveTextContent(
      "This tenant's keys are wrapped only by its own root key. Revoking or destroying that key makes its data unreadable, and no operator can recover it.",
    );
    expect(screen.queryByTestId('encryption-keys-no-recovery')).not.toBeInTheDocument();
    expect(screen.getByTestId('encryption-key-root-key-k-2')).toHaveTextContent(
      'arn:aws:kms:key/abc',
    );
    expect(screen.getByTestId('encryption-key-root-key-k-1')).toHaveTextContent('Deployment');
  });

  it('shows an error with the failure when the root key is unavailable', async () => {
    installFetch(() =>
      jsonResponse({
        ...withRootKey,
        rootKey: { ...rootKey, state: 'unavailable', failure: 'The key was revoked.' },
      }),
    );

    renderTab(tenant, 'tenant');

    expect(await screen.findByTestId('root-key-unavailable')).toHaveTextContent(
      'The key was revoked.',
    );
  });

  it('sets a root key from the dialog and reloads the keys', async () => {
    let current: EncryptionKeysResource = withoutRootKey;
    const calls = installFetch((url, method) => {
      if (url === rootKeyUrl && method === 'PUT') {
        current = withRootKey;
        return jsonResponse({
          rootKey,
          activeKey: null,
          retiredKey: null,
          rewrappedKeys: 2,
          failedKeys: 0,
        });
      }
      return jsonResponse(current);
    });

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));
    fireEvent.change(screen.getByTestId('root-key-form-reference'), {
      target: { value: ' arn:aws:kms:key/abc ' },
    });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(await screen.findByTestId('root-key-provider')).toHaveTextContent('aws-kms');
    await waitFor(() =>
      expect(screen.queryByTestId('root-key-form-submit')).not.toBeInTheDocument(),
    );
    const put = calls.find((c) => c.method === 'PUT');
    expect(put?.url).toBe(rootKeyUrl);
    expect(JSON.parse(put?.body ?? '{}')).toEqual({
      provider: 'aws-kms',
      keyReference: 'arn:aws:kms:key/abc',
      accessIdentity: null,
    });
    expect(screen.getByTestId('encryption-keys-root-key-only')).toBeInTheDocument();
  });

  it('names the access binding in the dialog and sets the trimmed access identity', async () => {
    const calls = installFetch((url, method) =>
      url === rootKeyUrl && method === 'PUT'
        ? jsonResponse({
            rootKey,
            activeKey: null,
            retiredKey: null,
            rewrappedKeys: 0,
            failedKeys: 0,
          })
        : jsonResponse({ ...withoutRootKey, rootKeyAccessBinding: 'tenant:acme' }),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));
    expect(
      screen.getByText(
        "Leave blank to reach the key with the deployment's identity. An AWS role's trust policy requires the external ID tenant:acme.",
      ),
    ).toBeInTheDocument();
    fireEvent.change(screen.getByTestId('root-key-form-reference'), {
      target: { value: 'arn:aws:kms:key/abc' },
    });
    fireEvent.change(screen.getByTestId('root-key-form-access-identity'), {
      target: { value: ' arn:aws:iam::111122223333:role/tenant ' },
    });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true));
    expect(JSON.parse(calls.find((c) => c.method === 'PUT')?.body ?? '{}')).toEqual({
      provider: 'aws-kms',
      keyReference: 'arn:aws:kms:key/abc',
      accessIdentity: 'arn:aws:iam::111122223333:role/tenant',
    });
  });

  it('opens the dialog with the stored key reference and access identity and submits them unchanged', async () => {
    const stored: EncryptionRootKeyResource = {
      ...rootKey,
      accessIdentity: 'arn:aws:iam::111122223333:role/tenant',
    };
    const calls = installFetch((url, method) =>
      url === rootKeyUrl && method === 'PUT'
        ? jsonResponse({
            rootKey: stored,
            activeKey: null,
            retiredKey: null,
            rewrappedKeys: 0,
            failedKeys: 0,
          })
        : jsonResponse({ ...withRootKey, rootKey: stored }),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));

    expect(screen.getByTestId('root-key-form-reference')).toHaveValue('arn:aws:kms:key/abc');
    expect(screen.getByTestId('root-key-form-access-identity')).toHaveValue(
      'arn:aws:iam::111122223333:role/tenant',
    );
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true));
    expect(JSON.parse(calls.find((c) => c.method === 'PUT')?.body ?? '{}')).toEqual({
      provider: 'aws-kms',
      keyReference: 'arn:aws:kms:key/abc',
      accessIdentity: 'arn:aws:iam::111122223333:role/tenant',
    });
  });

  it('shows the server explanation of a root key that fails its probe and keeps the dialog open', async () => {
    installFetch((url, method) =>
      url === rootKeyUrl && method === 'PUT'
        ? jsonResponse(
            {
              title: 'Unprocessable Entity',
              status: 422,
              detail: "The key could not wrap and unwrap a probe through 'aws-kms': access denied",
            },
            422,
          )
        : jsonResponse(withoutRootKey),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));
    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(await screen.findByTestId('root-key-form-error')).toHaveTextContent('access denied');
    expect(screen.getByTestId('root-key-form-submit')).toBeInTheDocument();
  });

  it('shows how long to wait when a root key change comes too soon', async () => {
    installFetch((url, method) =>
      url === rootKeyUrl && method === 'PUT'
        ? jsonResponse(
            {
              title: 'Conflict',
              status: 409,
              detail: 'The last rotation of this master key has not reached every replica yet.',
              retryAfterSeconds: 45,
            },
            409,
          )
        : jsonResponse(withoutRootKey),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));
    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(await screen.findByTestId('root-key-form-error')).toHaveTextContent(
      'Try again in 45 seconds.',
    );
  });

  it('shows the validation messages of a rejected field in the dialog', async () => {
    installFetch((url, method) =>
      url === rootKeyUrl && method === 'PUT'
        ? jsonResponse(
            {
              title: 'One or more validation errors occurred.',
              status: 400,
              errors: {
                provider: ['The provider is not allowed.'],
                keyReference: ['The key reference is malformed.'],
              },
            },
            400,
          )
        : jsonResponse(withoutRootKey),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));
    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(await screen.findByTestId('root-key-form-error')).toHaveTextContent(
      'The provider is not allowed. The key reference is malformed.',
    );
  });

  it('falls back to the problem title when validation errors carry no messages', async () => {
    installFetch((url, method) =>
      url === rootKeyUrl && method === 'PUT'
        ? jsonResponse({ title: 'Bad Request', status: 400, errors: { provider: 'x' } }, 400)
        : jsonResponse(withoutRootKey),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));
    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(await screen.findByTestId('root-key-form-error')).toHaveTextContent('Bad Request');
  });

  it('seeds the dialog with the provider of the current root key', async () => {
    installFetch(() =>
      jsonResponse({
        ...withRootKey,
        rootKey: { ...rootKey, provider: 'google-cloud-kms' },
      }),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-set'));

    expect(screen.getByTestId('root-key-form-provider')).toHaveValue('google-cloud-kms');
  });

  it('validates the root key and reloads the keys', async () => {
    let current: EncryptionKeysResource = withRootKey;
    const calls = installFetch((url, method) => {
      if (url === `${rootKeyUrl}/validate` && method === 'POST') {
        current = {
          ...withRootKey,
          rootKey: { ...rootKey, state: 'unavailable', failure: 'The key was revoked.' },
        };
        return jsonResponse(current.rootKey);
      }
      return jsonResponse(current);
    });

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-validate'));

    expect(await screen.findByTestId('root-key-unavailable')).toHaveTextContent(
      'The key was revoked.',
    );
    expect(calls.some((c) => c.method === 'POST' && c.url === `${rootKeyUrl}/validate`)).toBe(true);
  });

  it('reports a failed validation', async () => {
    installFetch((_url, method) =>
      method === 'POST'
        ? jsonResponse({ title: 'Not Found', status: 404 }, 404)
        : jsonResponse(withRootKey),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-validate'));

    expect(await screen.findByTestId('root-key-validate-error')).toHaveTextContent('Not Found');
  });

  it('removes the root key after confirmation and reloads the keys', async () => {
    let current: EncryptionKeysResource = withRootKey;
    const calls = installFetch((url, method) => {
      if (url === rootKeyUrl && method === 'DELETE') {
        current = withoutRootKey;
        return new Response(null, { status: 204 });
      }
      return jsonResponse(current);
    });

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-remove'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    expect(await screen.findByTestId('root-key-none')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.some((c) => c.method === 'DELETE' && c.url === rootKeyUrl)).toBe(true);
  });

  it('shows the root key as removing while keys remain under it', async () => {
    let current: EncryptionKeysResource = withRootKey;
    installFetch((url, method) => {
      if (url === rootKeyUrl && method === 'DELETE') {
        const removing = { ...rootKey, state: 'removing' };
        current = { ...withRootKey, rootKey: removing };
        return jsonResponse(removing, 202);
      }
      return jsonResponse(current);
    });

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-remove'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    await waitFor(() => expect(screen.getByTestId('root-key-state')).toHaveTextContent('removing'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('omits the root-key-only alert and shows the recovery warning while the root key is removing', async () => {
    installFetch(() =>
      jsonResponse({
        keys: [activeKey, retiredKey],
        recoveryConfigured: false,
        rootKey: { ...rootKey, state: 'removing' },
        rootKeyProviders: providers,
      }),
    );

    renderTab(tenant, 'tenant');

    expect(await screen.findByTestId('encryption-keys-no-recovery')).toBeInTheDocument();
    expect(screen.getByTestId('root-key-state')).toHaveTextContent('removing');
    expect(screen.queryByTestId('encryption-keys-root-key-only')).not.toBeInTheDocument();
  });

  it('explains in the remove dialog how the keys are rewrapped and retried', async () => {
    installFetch(() => jsonResponse(withRootKey));

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-remove'));

    expect(await screen.findByRole('dialog')).toHaveTextContent(
      "The keys are rewrapped under the deployment's root key. Any that cannot be unwrapped yet are retried in the background, and the root key is removed once none remains under it.",
    );
  });

  it('does not remove the root key when the confirmation is cancelled', async () => {
    const calls = installFetch(() => jsonResponse(withRootKey));

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-remove'));
    fireEvent.click(await screen.findByTestId('confirm-cancel'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('shows the explanation of a failed removal and keeps the dialog open', async () => {
    installFetch((_url, method) =>
      method === 'DELETE'
        ? jsonResponse(
            { title: 'Conflict', status: 409, detail: 'The removal is in progress.' },
            409,
          )
        : jsonResponse(withRootKey),
    );

    renderTab(tenant, 'tenant');
    fireEvent.click(await screen.findByTestId('root-key-remove'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The removal is in progress.',
    );
  });
});

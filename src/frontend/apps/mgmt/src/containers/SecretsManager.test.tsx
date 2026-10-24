import { describe, expect, it, beforeEach, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore, managementApi } from '@ncode/store';
import type {
  CreatedSecretResource,
  ManagementCapabilitiesResource,
  PublicKeyResource,
  SecretResource,
} from '@ncode/api';
import type { RuntimeConfig } from '@ncode/config';
import { SecretsManager, type SecretsManagerProps } from './SecretsManager.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const rsaSecret: SecretResource = {
  secretId: 's-rsa',
  keyId: 'rsa-kid',
  concurrencyToken: 'sc1',
  use: 'sig',
  algorithm: 'RS256',
  createdWhen: '2026-01-01T00:00:00Z',
  expiresWhen: '2099-01-01T00:00:00Z',
  secretType: 'rsa',
  keySizeBits: 2048,
};

const symmetricSecret: SecretResource = {
  ...rsaSecret,
  secretId: 's-sym',
  keyId: 's-sym',
  use: null,
  algorithm: null,
  secretType: 'symmetric',
  keySizeBits: 256,
};

const created: CreatedSecretResource = {
  ...symmetricSecret,
  secretId: 's-new',
  secretMaterial: 'one-time-material',
};

const publicKey: PublicKeyResource = {
  secretId: 's-rsa',
  secretType: 'rsa',
  jwk: { kty: 'RSA', n: 'bW9k', e: 'AQAB' },
  pem: '-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----',
};

const exportRsaOnly: ManagementCapabilitiesResource = {
  secretTypes: [
    { secretType: 'symmetric', isAsymmetric: false, supportsPublicKeyExport: false },
    { secretType: 'rsa', isAsymmetric: true, supportsPublicKeyExport: true },
  ],
};

type Deferred<T> = {
  readonly promise: Promise<T>;
  readonly resolve: (value: T) => void;
};

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((onResolve) => {
    resolve = onResolve;
  });
  return { promise, resolve };
}

/**
 * Renders the manager against a store whose capabilities cache is already populated, so the discovery query is
 * served from the cache and never reaches the network. `null` leaves the capabilities undiscovered.
 */
async function renderManager(
  props: Partial<SecretsManagerProps> = {},
  capabilities: ManagementCapabilitiesResource | null = exportRsaOnly,
) {
  const store = createAppStore({ config: { runtime } });
  if (capabilities) {
    await store.dispatch(
      managementApi.util.upsertQueryData('getCapabilities', undefined, capabilities),
    );
  }
  const handlers = {
    onCreate: vi.fn<SecretsManagerProps['onCreate']>().mockResolvedValue(created),
    onDelete: vi.fn<SecretsManagerProps['onDelete']>().mockResolvedValue(undefined),
    onExportPublicKey: vi
      .fn<SecretsManagerProps['onExportPublicKey']>()
      .mockResolvedValue(publicKey),
  };
  render(
    <Provider store={store}>
      <SecretsManager
        ownerLabel="client"
        secrets={[rsaSecret, symmetricSecret]}
        isLoading={false}
        loadError={false}
        isCreating={false}
        isDeleting={false}
        {...handlers}
        {...props}
      />
    </Provider>,
  );
  return handlers;
}

describe('SecretsManager', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.restoreAllMocks();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  describe('list states', () => {
    it('shows a loading indicator while the secrets load', async () => {
      await renderManager({ secrets: [], isLoading: true });

      expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    });

    it('reports a failure to load the secrets', async () => {
      await renderManager({ secrets: [], loadError: true });

      expect(screen.getByTestId('data-error')).toHaveTextContent('Failed to load secrets.');
    });

    it('names the owner in the empty state', async () => {
      await renderManager({ secrets: [], ownerLabel: 'resource server' });

      expect(screen.getByTestId('data-empty')).toHaveTextContent(
        'This resource server has no secrets yet.',
      );
    });

    it('offers public-key export only for the secret types the server says support it', async () => {
      await renderManager();

      expect(screen.getByTestId('secret-export-s-rsa')).toBeInTheDocument();
      expect(screen.queryByTestId('secret-export-s-sym')).not.toBeInTheDocument();
      expect(screen.getByTestId('secret-delete-s-sym')).toBeInTheDocument();
      // Discovery is served from the populated cache.
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('offers no public-key export until the capabilities are discovered', async () => {
      // Discovery stays in flight for the whole test.
      fetchMock.mockReturnValue(new Promise<Response>(() => {}));

      await renderManager({}, null);

      expect(screen.getByTestId('secret-delete-s-rsa')).toBeInTheDocument();
      expect(screen.queryByTestId('secret-export-s-rsa')).not.toBeInTheDocument();
      await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    });
  });

  describe('create', () => {
    it('creates a symmetric secret with the default one-year expiry and reveals it once', async () => {
      const { onCreate } = await renderManager();

      fireEvent.click(screen.getByTestId('create-secret'));
      const expires = (screen.getByTestId('secret-expires') as HTMLInputElement).value;
      expect(new Date(expires).getFullYear()).toBe(new Date().getFullYear() + 1);
      fireEvent.click(screen.getByTestId('secret-form-submit'));

      expect(await screen.findByTestId('reveal-secret-material')).toHaveTextContent(
        'one-time-material',
      );
      expect(screen.getByTestId('reveal-secret-id')).toHaveTextContent('s-new');
      expect(onCreate).toHaveBeenCalledWith({
        secretType: 'symmetric',
        keySizeBits: 256,
        curve: null,
        use: null,
        algorithm: null,
        expiresWhen: new Date(expires).toISOString(),
      });

      fireEvent.click(screen.getByTestId('reveal-secret-done'));
      await waitFor(() =>
        expect(screen.queryByTestId('reveal-secret-material')).not.toBeInTheDocument(),
      );
    });

    it('creates an ECC secret on a discovered curve and algorithm', async () => {
      const { onCreate } = await renderManager(
        {},
        {
          ...exportRsaOnly,
          eccCurves: [
            { curveName: 'P-256', curveSizeBits: 256 },
            { curveName: 'P-384', curveSizeBits: 384 },
          ],
          algorithms: [
            {
              code: 'ES384',
              use: 'sig',
              secretTypes: ['ecc'],
              keyBitSizes: [{ minBits: 384, maxBits: 384, skipBits: 0 }],
            },
          ],
        },
      );

      fireEvent.click(screen.getByTestId('create-secret'));
      fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'ecc' } });
      fireEvent.change(screen.getByTestId('secret-curve'), { target: { value: 'P-384' } });
      fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ES384' } });
      fireEvent.click(screen.getByTestId('secret-form-submit'));

      await screen.findByTestId('reveal-secret-material');
      expect(onCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          secretType: 'ecc',
          keySizeBits: 384,
          curve: 'P-384',
          use: 'sig',
          algorithm: 'ES384',
        }),
      );
    });

    it('sends no curve for an ECC secret sized by key size and keeps hand-entered use and algorithm', async () => {
      const { onCreate } = await renderManager();

      fireEvent.click(screen.getByTestId('create-secret'));
      fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'ecc' } });
      fireEvent.change(screen.getByTestId('secret-use'), { target: { value: 'sig' } });
      fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ES256' } });
      fireEvent.click(screen.getByTestId('secret-form-submit'));

      await screen.findByTestId('reveal-secret-material');
      expect(onCreate).toHaveBeenCalledWith(
        expect.objectContaining({ secretType: 'ecc', curve: null, use: 'sig', algorithm: 'ES256' }),
      );
    });

    it('surfaces a rejected create with the server problem detail and keeps the form open', async () => {
      const { onCreate } = await renderManager();
      onCreate.mockRejectedValue({ status: 400, data: { detail: 'Key size is not supported.' } });

      fireEvent.click(screen.getByTestId('create-secret'));
      fireEvent.click(screen.getByTestId('secret-form-submit'));

      expect(await screen.findByTestId('secret-form-error')).toHaveTextContent(
        'Key size is not supported.',
      );
      expect(screen.getByTestId('secret-form-submit')).toBeInTheDocument();
      expect(screen.queryByTestId('reveal-secret-material')).not.toBeInTheDocument();
    });

    it('discards the create form on cancel', async () => {
      const { onCreate } = await renderManager();

      fireEvent.click(screen.getByTestId('create-secret'));
      fireEvent.click(screen.getByTestId('secret-form-cancel'));

      await waitFor(() =>
        expect(screen.queryByTestId('secret-form-submit')).not.toBeInTheDocument(),
      );
      expect(onCreate).not.toHaveBeenCalled();
    });
  });

  describe('delete', () => {
    it('deletes a secret after confirmation', async () => {
      const { onDelete } = await renderManager();

      fireEvent.click(screen.getByTestId('secret-delete-s-sym'));
      expect(screen.getByRole('dialog', { name: 'Delete secret' })).toHaveTextContent(
        'Delete secret “s-sym”? Anything using it will stop authenticating.',
      );
      fireEvent.click(screen.getByTestId('confirm-accept'));

      await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
      expect(onDelete).toHaveBeenCalledWith('s-sym');
    });

    it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
      const { onDelete } = await renderManager();
      onDelete.mockRejectedValue({ status: 409, data: { detail: 'The secret is in use.' } });

      fireEvent.click(screen.getByTestId('secret-delete-s-rsa'));
      fireEvent.click(screen.getByTestId('confirm-accept'));

      expect(await screen.findByTestId('confirm-error')).toHaveTextContent('The secret is in use.');
      expect(screen.getByTestId('confirm-accept')).toBeEnabled();
    });

    it('ignores a confirm fired while the delete dialog is closing', async () => {
      const { onDelete } = await renderManager();

      fireEvent.click(screen.getByTestId('secret-delete-s-rsa'));
      fireEvent.click(screen.getByTestId('confirm-cancel'));
      // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
      fireEvent.click(screen.getByTestId('confirm-accept'));

      await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
      expect(onDelete).not.toHaveBeenCalled();
    });
  });

  describe('rotate', () => {
    it('offers rotation only for the shared secret types the server discovers', async () => {
      await renderManager({ onRotate: vi.fn<NonNullable<SecretsManagerProps['onRotate']>>() });

      expect(screen.getByTestId('secret-rotate-s-sym')).toBeInTheDocument();
      expect(screen.queryByTestId('secret-rotate-s-rsa')).not.toBeInTheDocument();
    });

    it('offers no rotation when the owner cannot rotate its secrets', async () => {
      await renderManager();

      expect(screen.queryByTestId('secret-rotate-s-sym')).not.toBeInTheDocument();
    });

    it('rotates a secret after confirmation and reveals the replacement once', async () => {
      const onRotate = vi
        .fn<NonNullable<SecretsManagerProps['onRotate']>>()
        .mockResolvedValue(created);
      await renderManager({ onRotate });

      fireEvent.click(screen.getByTestId('secret-rotate-s-sym'));
      expect(screen.getByRole('dialog', { name: 'Rotate secret' })).toHaveTextContent(
        'Replace secret “s-sym” with a new one that expires in a year?',
      );
      fireEvent.click(screen.getByTestId('confirm-accept'));

      expect(await screen.findByTestId('reveal-secret-material')).toHaveTextContent(
        'one-time-material',
      );
      expect(screen.getByTestId('reveal-secret-id')).toHaveTextContent('s-new');
      expect(onRotate).toHaveBeenCalledTimes(1);
      const [secretId, body] = onRotate.mock.calls[0]!;
      expect(secretId).toBe('s-sym');
      expect(new Date(body.expiresWhen).getFullYear()).toBe(new Date().getFullYear() + 1);
    });

    it('discards the rotate dialog on cancel without rotating', async () => {
      const onRotate = vi.fn<NonNullable<SecretsManagerProps['onRotate']>>();
      await renderManager({ onRotate });

      fireEvent.click(screen.getByTestId('secret-rotate-s-sym'));
      fireEvent.click(screen.getByTestId('confirm-cancel'));

      await waitFor(() =>
        expect(screen.queryByRole('dialog', { name: 'Rotate secret' })).not.toBeInTheDocument(),
      );
      expect(onRotate).not.toHaveBeenCalled();
    });

    it('keeps the rotate dialog open with the server problem detail when the rotation is rejected', async () => {
      const onRotate = vi
        .fn<NonNullable<SecretsManagerProps['onRotate']>>()
        .mockRejectedValue({ status: 400, data: { detail: 'The secret cannot be rotated.' } });
      await renderManager({ onRotate });

      fireEvent.click(screen.getByTestId('secret-rotate-s-sym'));
      fireEvent.click(screen.getByTestId('confirm-accept'));

      expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
        'The secret cannot be rotated.',
      );
      expect(screen.queryByTestId('reveal-secret-material')).not.toBeInTheDocument();
    });
  });

  describe('public-key export', () => {
    it('shows progress until the public key arrives, then both representations', async () => {
      const pending = deferred<PublicKeyResource>();
      const { onExportPublicKey } = await renderManager();
      onExportPublicKey.mockReturnValue(pending.promise);

      fireEvent.click(screen.getByTestId('secret-export-s-rsa'));

      expect(screen.getByTestId('export-public-key-id')).toHaveTextContent('s-rsa');
      expect(screen.getByTestId('export-public-key-loading')).toBeInTheDocument();
      expect(onExportPublicKey).toHaveBeenCalledWith('s-rsa');

      pending.resolve(publicKey);

      expect(await screen.findByTestId('export-public-key-pem-value')).toHaveTextContent(
        'BEGIN PUBLIC KEY',
      );
      expect(screen.getByTestId('export-public-key-jwk-value')).toHaveTextContent('"kty": "RSA"');
      expect(screen.queryByTestId('export-public-key-loading')).not.toBeInTheDocument();

      fireEvent.click(screen.getByTestId('export-public-key-done'));
      await waitFor(() =>
        expect(screen.queryByTestId('export-public-key-done')).not.toBeInTheDocument(),
      );
    });

    it('surfaces an export failure with the server problem detail', async () => {
      const { onExportPublicKey } = await renderManager();
      onExportPublicKey.mockRejectedValue({
        status: 404,
        data: { detail: 'The secret has no public key.' },
      });

      fireEvent.click(screen.getByTestId('secret-export-s-rsa'));

      expect(await screen.findByTestId('export-public-key-error')).toHaveTextContent(
        'The secret has no public key.',
      );
      expect(screen.queryByTestId('export-public-key-loading')).not.toBeInTheDocument();
    });
  });
});

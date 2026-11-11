import type { Meta, StoryObj } from '@storybook/react-vite';
import { SecretTable } from './SecretTable.js';

const meta: Meta<typeof SecretTable> = {
  title: 'Management/SecretTable',
  component: SecretTable,
  // A fixed reference instant, so the expired/active split renders the same on every visit.
  args: { now: Date.parse('2026-06-01T00:00:00Z') },
};
export default meta;

type Story = StoryObj<typeof SecretTable>;

const base = {
  use: null,
  algorithm: null,
  isVerifier: false,
  createdWhen: '2026-01-01T12:00:00Z',
  activatesWhen: '2026-01-01T12:00:00Z',
  managedBy: null,
  remoteKeyProvider: null,
};

export const Populated: Story = {
  args: {
    secrets: [
      {
        ...base,
        secretId: 's-active',
        keyId: 's-active',
        secretType: 'symmetric',
        keySizeBits: 256,
        // A client secret its method only compares, held as a one-way verifier (ADR-1391).
        isVerifier: true,
        retiresWhen: '2099-01-01T12:00:00Z',
        expiresWhen: '2099-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-expired',
        keyId: 's-expired',
        secretType: 'rsa',
        keySizeBits: 2048,
        retiresWhen: '2020-01-01T12:00:00Z',
        expiresWhen: '2020-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-upcoming',
        keyId: 's-upcoming',
        secretType: 'rsa',
        keySizeBits: 2048,
        // A successor the server provisioned, published ahead of the hand-over (ADR-1404).
        activatesWhen: '2099-01-01T12:00:00Z',
        retiresWhen: '2099-04-01T12:00:00Z',
        expiresWhen: '2099-04-08T12:00:00Z',
        managedBy: 'tenant_key_rollover',
      },
      {
        ...base,
        secretId: 's-remote',
        keyId: 's-remote',
        secretType: 'rsa',
        keySizeBits: 2048,
        // A signing key held in a key management service, which signs with it (ADR-1471).
        retiresWhen: '2099-01-01T12:00:00Z',
        expiresWhen: '2099-01-01T12:00:00Z',
        remoteKeyProvider: 'aws-kms',
      },
    ],
  },
};

export const Empty: Story = { args: { secrets: [] } };

export const WithActions: Story = {
  args: {
    secrets: [
      {
        ...base,
        secretId: 's-active',
        keyId: 's-active',
        secretType: 'symmetric',
        keySizeBits: 256,
        retiresWhen: '2099-01-01T12:00:00Z',
        expiresWhen: '2099-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-rsa',
        keyId: 's-rsa',
        secretType: 'rsa',
        keySizeBits: 2048,
        retiresWhen: '2099-01-01T12:00:00Z',
        expiresWhen: '2099-01-01T12:00:00Z',
      },
    ],
    onDelete: () => undefined,
    onExportPublicKey: () => undefined,
    exportableSecretTypes: ['rsa', 'ecc'],
  },
};

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
  createdWhen: '2026-01-01T12:00:00Z',
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
        expiresWhen: '2099-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-expired',
        keyId: 's-expired',
        secretType: 'rsa',
        keySizeBits: 2048,
        expiresWhen: '2020-01-01T12:00:00Z',
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
        expiresWhen: '2099-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-rsa',
        keyId: 's-rsa',
        secretType: 'rsa',
        keySizeBits: 2048,
        expiresWhen: '2099-01-01T12:00:00Z',
      },
    ],
    onDelete: () => undefined,
    onExportPublicKey: () => undefined,
    exportableSecretTypes: ['rsa', 'ecc'],
  },
};

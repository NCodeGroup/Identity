import type { Meta, StoryObj } from '@storybook/react';
import { SecretTable } from './SecretTable.js';

const meta: Meta<typeof SecretTable> = {
  title: 'Management/SecretTable',
  component: SecretTable,
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
        secretType: 'symmetric',
        keySizeBits: 256,
        expiresWhen: '2099-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-expired',
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
        secretType: 'symmetric',
        keySizeBits: 256,
        expiresWhen: '2099-01-01T12:00:00Z',
      },
      {
        ...base,
        secretId: 's-rsa',
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

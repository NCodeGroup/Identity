import type { Meta, StoryObj } from '@storybook/react';
import { ExportPublicKeyDialog } from './ExportPublicKeyDialog.js';

const meta: Meta<typeof ExportPublicKeyDialog> = {
  title: 'Management/ExportPublicKeyDialog',
  component: ExportPublicKeyDialog,
};
export default meta;

type Story = StoryObj<typeof ExportPublicKeyDialog>;

export const Loaded: Story = {
  args: {
    open: true,
    secretId: 's-1',
    isLoading: false,
    error: undefined,
    publicKey: {
      secretId: 's-1',
      secretType: 'rsa',
      jwk: { kty: 'RSA', use: 'sig', kid: 's-1', n: 'bW9kdWx1cw', e: 'AQAB' },
      pem: '-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A\n-----END PUBLIC KEY-----',
    },
    onClose: () => undefined,
  },
};

export const Loading: Story = {
  args: {
    open: true,
    secretId: 's-1',
    isLoading: true,
    error: undefined,
    publicKey: undefined,
    onClose: () => undefined,
  },
};

export const Error: Story = {
  args: {
    open: true,
    secretId: 's-1',
    isLoading: false,
    error: 'The secret has no public key to export.',
    publicKey: undefined,
    onClose: () => undefined,
  },
};

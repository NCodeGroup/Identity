import type { Meta, StoryObj } from '@storybook/react-vite';
import { RemoteSecretBindDialog, emptyRemoteSecretBindValues } from './RemoteSecretBindDialog.js';

const meta: Meta<typeof RemoteSecretBindDialog> = {
  title: 'Management/RemoteSecretBindDialog',
  component: RemoteSecretBindDialog,
};
export default meta;

type Story = StoryObj<typeof RemoteSecretBindDialog>;

const remoteKeyProviders = [
  {
    name: 'aws-kms',
    algorithms: [
      { algorithm: 'ES256', secretType: 'ecc', keySizeBits: 256, curve: 'P-256' },
      { algorithm: 'RS256', secretType: 'rsa', keySizeBits: 2048, curve: null },
    ],
  },
  {
    name: 'gcp-kms',
    algorithms: [{ algorithm: 'PS256', secretType: 'rsa', keySizeBits: 3072 }],
  },
];

export const Bind: Story = {
  args: {
    open: true,
    initialValues: { ...emptyRemoteSecretBindValues, expiresWhen: '2027-01-01T00:00' },
    remoteKeyProviders,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const WithError: Story = {
  args: {
    open: true,
    initialValues: { ...emptyRemoteSecretBindValues, expiresWhen: '2027-01-01T00:00' },
    remoteKeyProviders,
    error:
      "The key 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab' of the remote key provider 'aws-kms' cannot be bound as a 'ES256' key: AWS KMS key 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab' is not a signing key.",
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const Submitting: Story = {
  args: {
    open: true,
    isSubmitting: true,
    initialValues: {
      ...emptyRemoteSecretBindValues,
      remoteKeyProvider: 'aws-kms',
      remoteKeyId: 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab',
      secretType: 'ecc',
      keySizeBits: 256,
      curve: 'P-256',
      algorithm: 'ES256',
      expiresWhen: '2027-01-01T00:00',
    },
    remoteKeyProviders,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

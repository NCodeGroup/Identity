import type { Meta, StoryObj } from '@storybook/react-vite';
import {
  EncryptionRootKeyFormDialog,
  emptyEncryptionRootKeyFormValues,
} from './EncryptionRootKeyFormDialog.js';

const meta: Meta<typeof EncryptionRootKeyFormDialog> = {
  title: 'Management/EncryptionRootKeyFormDialog',
  component: EncryptionRootKeyFormDialog,
  args: {
    open: true,
    initialValues: emptyEncryptionRootKeyFormValues,
    providers: ['aws-kms', 'google-cloud-kms', 'azure-key-vault'],
    accessBinding: 'tenant:acme',
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof EncryptionRootKeyFormDialog>;

export const Default: Story = {};

export const WithAccessIdentity: Story = {
  args: {
    initialValues: {
      provider: 'aws-kms',
      keyReference: 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab',
      accessIdentity: 'arn:aws:iam::111122223333:role/tenant-kms',
    },
  },
};

export const NoAccessBinding: Story = {
  args: { accessBinding: null },
};

export const Submitting: Story = {
  args: {
    initialValues: {
      provider: 'aws-kms',
      keyReference: 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab',
      accessIdentity: null,
    },
    isSubmitting: true,
  },
};

export const WithError: Story = {
  args: {
    initialValues: {
      provider: 'aws-kms',
      keyReference: 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab',
      accessIdentity: null,
    },
    error:
      'The key could not wrap and unwrap a probe: the key is disabled or its grant was revoked.',
  },
};

export const SingleProvider: Story = {
  args: {
    providers: ['aws-kms'],
  },
};

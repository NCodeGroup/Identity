import type { Meta, StoryObj } from '@storybook/react-vite';
import { EncryptionRootKeyCard } from './EncryptionRootKeyCard.js';

const meta: Meta<typeof EncryptionRootKeyCard> = {
  title: 'Management/EncryptionRootKeyCard',
  component: EncryptionRootKeyCard,
  args: {
    canSet: true,
    onSet: () => undefined,
    onValidate: () => undefined,
    onRemove: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof EncryptionRootKeyCard>;

const activeRootKey = {
  provider: 'aws-kms',
  keyReference: 'arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab',
  state: 'active',
  createdWhen: '2026-10-01T09:00:00Z',
  validatedWhen: '2026-10-01T10:00:00Z',
};

export const Active: Story = {
  args: { rootKey: activeRootKey },
};

export const WithAccessIdentity: Story = {
  args: {
    rootKey: { ...activeRootKey, accessIdentity: 'arn:aws:iam::111122223333:role/tenant-kms' },
  },
};

export const Unavailable: Story = {
  args: {
    rootKey: {
      ...activeRootKey,
      state: 'unavailable',
      failure: 'The key is disabled or its grant was revoked.',
    },
  },
};

export const Removing: Story = {
  args: { rootKey: { ...activeRootKey, state: 'removing' } },
};

export const NoRootKey: Story = {
  args: { rootKey: null },
};

export const NoProviderAvailable: Story = {
  args: { rootKey: null, canSet: false },
};

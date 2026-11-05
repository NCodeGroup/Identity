import type { Meta, StoryObj } from '@storybook/react-vite';
import { EncryptionKeyTable } from './EncryptionKeyTable.js';

const meta: Meta<typeof EncryptionKeyTable> = {
  title: 'Management/EncryptionKeyTable',
  component: EncryptionKeyTable,
};
export default meta;

type Story = StoryObj<typeof EncryptionKeyTable>;

export const ActiveAndRetired: Story = {
  args: {
    keys: [
      {
        keyId: '5b3c2b8e-8a0d-4f0e-9a43-2f6f1a1d7c11',
        version: 2,
        state: 'active',
        createdWhen: '2026-10-01T09:00:00Z',
        retiredWhen: null,
        rootKeyProvider: 'aws-kms',
        recoveryKeyId: 'recovery-key-1',
      },
      {
        keyId: '0f4d7c1e-3d6a-45a3-8d3f-5d0a8c0b9e22',
        version: 1,
        state: 'retired',
        createdWhen: '2025-10-01T09:00:00Z',
        retiredWhen: '2026-10-01T09:00:00Z',
        rootKeyProvider: 'aws-kms',
        recoveryKeyId: null,
      },
    ],
  },
};

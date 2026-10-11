import type { Meta, StoryObj } from '@storybook/react';
import { GrantTable } from './GrantTable.js';

const meta: Meta<typeof GrantTable> = {
  title: 'Management/GrantTable',
  component: GrantTable,
};
export default meta;

type Story = StoryObj<typeof GrantTable>;

const base = {
  grantType: 'authorization_code',
  createdWhen: '2026-01-01T12:00:00Z',
  expiresWhen: '2099-01-01T12:00:00Z',
  revokedWhen: null,
  consumedWhen: null,
};

export const Populated: Story = {
  args: {
    grants: [
      { ...base, grantId: 'g-active', clientId: 'web-spa', subjectId: 'user-1' },
      {
        ...base,
        grantId: 'g-revoked',
        clientId: 'web-spa',
        subjectId: 'user-2',
        revokedWhen: '2026-02-01T12:00:00Z',
      },
      {
        ...base,
        grantId: 'g-expired',
        grantType: 'refresh_token',
        clientId: 'worker',
        subjectId: null,
        expiresWhen: '2020-01-01T12:00:00Z',
      },
    ],
  },
};

export const Empty: Story = { args: { grants: [] } };

export const WithActions: Story = {
  args: {
    grants: [{ ...base, grantId: 'g-active', clientId: 'web-spa', subjectId: 'user-1' }],
    onRevoke: () => undefined,
  },
};

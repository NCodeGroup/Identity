import type { Meta, StoryObj } from '@storybook/react-vite';
import { TenantTable } from './TenantTable.js';

const meta: Meta<typeof TenantTable> = {
  title: 'Management/TenantTable',
  component: TenantTable,
};
export default meta;

type Story = StoryObj<typeof TenantTable>;

export const Populated: Story = {
  args: {
    tenants: [
      { tenantId: 'root', displayName: 'Root', issuer: 'https://auth.example', isDisabled: false },
      {
        tenantId: 'acme',
        displayName: 'Acme Inc',
        issuer: 'https://acme.auth.example',
        isDisabled: false,
      },
      { tenantId: 'old', displayName: 'Legacy', issuer: null, isDisabled: true },
    ],
  },
};

export const Empty: Story = { args: { tenants: [] } };

export const WithActions: Story = {
  args: {
    tenants: [
      { tenantId: 'root', displayName: 'Root', issuer: 'https://auth.example', isDisabled: false },
      { tenantId: 'old', displayName: 'Legacy', issuer: null, isDisabled: true },
    ],
    onEdit: () => undefined,
    onDelete: () => undefined,
  },
};

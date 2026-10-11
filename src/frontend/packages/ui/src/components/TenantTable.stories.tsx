import type { Meta, StoryObj } from '@storybook/react';
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
      { tenantId: 'root', displayName: 'Root', domainName: 'id.example', isDisabled: false },
      { tenantId: 'acme', displayName: 'Acme Inc', domainName: 'acme.example', isDisabled: false },
      { tenantId: 'old', displayName: 'Legacy', domainName: null, isDisabled: true },
    ],
  },
};

export const Empty: Story = { args: { tenants: [] } };

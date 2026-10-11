import type { Meta, StoryObj } from '@storybook/react';
import { ScopeTable } from './ScopeTable.js';

const meta: Meta<typeof ScopeTable> = {
  title: 'Management/ScopeTable',
  component: ScopeTable,
};
export default meta;

type Story = StoryObj<typeof ScopeTable>;

const scopes = [
  { value: 'read:orders', description: 'Read orders', isSystem: false },
  { value: 'write:orders', description: null, isSystem: false },
  { value: 'openid', description: 'OpenID Connect', isSystem: true },
];

export const Populated: Story = { args: { scopes } };

export const Empty: Story = { args: { scopes: [] } };

export const WithActions: Story = {
  args: {
    scopes,
    onEdit: () => undefined,
    onDelete: () => undefined,
  },
};

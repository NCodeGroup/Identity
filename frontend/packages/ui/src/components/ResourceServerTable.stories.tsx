import type { Meta, StoryObj } from '@storybook/react-vite';
import { ResourceServerTable } from './ResourceServerTable.js';

const meta: Meta<typeof ResourceServerTable> = {
  title: 'Management/ResourceServerTable',
  component: ResourceServerTable,
};
export default meta;

type Story = StoryObj<typeof ResourceServerTable>;

const resourceServers = [
  {
    resourceServerId: 'rs-orders',
    name: 'Orders API',
    identifier: 'https://orders.example.com',
    isDisabled: false,
    isSystem: false,
    scopeCount: 3,
  },
  {
    resourceServerId: 'rs-management',
    name: 'Management API',
    identifier: 'urn:ncode:management',
    isDisabled: false,
    isSystem: true,
    scopeCount: 1,
  },
];

export const Populated: Story = { args: { resourceServers } };

export const Empty: Story = { args: { resourceServers: [] } };

export const WithActions: Story = {
  args: {
    resourceServers,
    onSelect: () => undefined,
    onEdit: () => undefined,
    onDelete: () => undefined,
  },
};

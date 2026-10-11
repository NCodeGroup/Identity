import type { Meta, StoryObj } from '@storybook/react';
import { ClientTable } from './ClientTable.js';

const meta: Meta<typeof ClientTable> = {
  title: 'Management/ClientTable',
  component: ClientTable,
};
export default meta;

type Story = StoryObj<typeof ClientTable>;

export const Populated: Story = {
  args: {
    clients: [
      { clientId: 'web-spa', isDisabled: false },
      { clientId: 'service-worker', isDisabled: true },
    ],
  },
};

export const Empty: Story = { args: { clients: [] } };

export const WithActions: Story = {
  args: {
    clients: [
      { clientId: 'web-spa', isDisabled: false },
      { clientId: 'service-worker', isDisabled: true },
    ],
    onEdit: () => undefined,
    onDelete: () => undefined,
  },
};

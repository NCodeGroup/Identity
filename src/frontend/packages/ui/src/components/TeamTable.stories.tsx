import type { Meta, StoryObj } from '@storybook/react-vite';
import { TeamTable } from './TeamTable.js';

const meta: Meta<typeof TeamTable> = {
  title: 'Management/TeamTable',
  component: TeamTable,
};
export default meta;

type Story = StoryObj<typeof TeamTable>;

const teams = [
  { teamId: 'support', displayName: 'Support', isDisabled: false },
  { teamId: 'billing', displayName: 'Billing', isDisabled: true },
];

export const Populated: Story = { args: { teams } };

export const Empty: Story = { args: { teams: [] } };

export const WithActions: Story = {
  args: {
    teams,
    onEdit: () => undefined,
    onDelete: () => undefined,
  },
};

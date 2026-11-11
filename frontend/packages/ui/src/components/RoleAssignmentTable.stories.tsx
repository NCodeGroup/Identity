import type { Meta, StoryObj } from '@storybook/react-vite';
import { RoleAssignmentTable } from './RoleAssignmentTable.js';

const meta: Meta<typeof RoleAssignmentTable> = {
  title: 'Management/RoleAssignmentTable',
  component: RoleAssignmentTable,
};
export default meta;

type Story = StoryObj<typeof RoleAssignmentTable>;

const assignments = [
  { assignmentId: 'a-1', principalId: 'alice' },
  { assignmentId: 'a-2', principalId: 'bob' },
];

export const Default: Story = {
  args: {
    label: 'Owners',
    testIdPrefix: 'owners',
    assignments,
    onRevoke: () => undefined,
  },
};

export const ReadOnly: Story = {
  args: { label: 'Owners', testIdPrefix: 'owners', assignments },
};

export const Empty: Story = {
  args: { label: 'Owners', testIdPrefix: 'owners', assignments: [] },
};

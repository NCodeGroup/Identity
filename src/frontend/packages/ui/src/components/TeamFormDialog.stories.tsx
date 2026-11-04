import type { Meta, StoryObj } from '@storybook/react-vite';
import { TeamFormDialog, emptyTeamFormValues } from './TeamFormDialog.js';

const meta: Meta<typeof TeamFormDialog> = {
  title: 'Management/TeamFormDialog',
  component: TeamFormDialog,
  args: {
    open: true,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof TeamFormDialog>;

export const Create: Story = {
  args: {
    title: 'Create team',
    submitLabel: 'Create',
    initialValues: emptyTeamFormValues,
  },
};

export const Edit: Story = {
  args: {
    title: 'Edit team',
    submitLabel: 'Save',
    initialValues: { teamId: 'support', displayName: 'Support', isDisabled: false },
    isTeamIdLocked: true,
  },
};

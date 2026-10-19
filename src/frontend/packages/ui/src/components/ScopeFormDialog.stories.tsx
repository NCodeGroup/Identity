import type { Meta, StoryObj } from '@storybook/react-vite';
import { ScopeFormDialog, emptyScopeFormValues } from './ScopeFormDialog.js';

const meta: Meta<typeof ScopeFormDialog> = {
  title: 'Management/ScopeFormDialog',
  component: ScopeFormDialog,
  args: {
    open: true,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof ScopeFormDialog>;

export const Create: Story = {
  args: { title: 'Add scope', submitLabel: 'Add', initialValues: emptyScopeFormValues },
};

export const Edit: Story = {
  args: {
    title: 'Edit scope',
    submitLabel: 'Save',
    valueEditable: false,
    initialValues: { value: 'read:orders', description: 'Read orders' },
  },
};

export const WithError: Story = {
  args: {
    title: 'Add scope',
    submitLabel: 'Add',
    initialValues: emptyScopeFormValues,
    error: 'A scope with that value already exists.',
  },
};

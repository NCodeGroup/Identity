import type { Meta, StoryObj } from '@storybook/react-vite';
import { ClientFormDialog, emptyClientFormValues } from './ClientFormDialog.js';

const meta: Meta<typeof ClientFormDialog> = {
  title: 'Management/ClientFormDialog',
  component: ClientFormDialog,
  args: {
    open: true,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof ClientFormDialog>;

export const Create: Story = {
  args: { title: 'Create client', submitLabel: 'Create', initialValues: emptyClientFormValues },
};

export const EditDisabled: Story = {
  args: { title: 'Edit client', submitLabel: 'Save', initialValues: { isDisabled: true } },
};

export const Submitting: Story = {
  args: {
    title: 'Create client',
    submitLabel: 'Create',
    initialValues: emptyClientFormValues,
    isSubmitting: true,
  },
};

export const WithError: Story = {
  args: {
    title: 'Create client',
    submitLabel: 'Create',
    initialValues: emptyClientFormValues,
    error: 'The client could not be created.',
  },
};

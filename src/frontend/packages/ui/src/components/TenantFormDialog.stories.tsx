import type { Meta, StoryObj } from '@storybook/react';
import { TenantFormDialog, emptyTenantFormValues } from './TenantFormDialog.js';

const meta: Meta<typeof TenantFormDialog> = {
  title: 'Management/TenantFormDialog',
  component: TenantFormDialog,
  args: {
    open: true,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof TenantFormDialog>;

export const Create: Story = {
  args: {
    title: 'Create tenant',
    submitLabel: 'Create',
    initialValues: emptyTenantFormValues,
  },
};

export const Edit: Story = {
  args: {
    title: 'Edit tenant',
    submitLabel: 'Save',
    initialValues: { displayName: 'Acme Inc', domainName: 'acme.example', isDisabled: false },
  },
};

export const Submitting: Story = {
  args: {
    title: 'Edit tenant',
    submitLabel: 'Save',
    initialValues: { displayName: 'Acme Inc', domainName: 'acme.example', isDisabled: false },
    isSubmitting: true,
  },
};

export const WithError: Story = {
  args: {
    title: 'Create tenant',
    submitLabel: 'Create',
    initialValues: emptyTenantFormValues,
    error: 'A tenant with that domain already exists.',
  },
};

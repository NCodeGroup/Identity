import type { Meta, StoryObj } from '@storybook/react-vite';
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

const acme = {
  tenantId: 'acme',
  displayName: 'Acme Inc',
  issuer: 'https://acme.auth.example',
  isDisabled: false,
};

export const Edit: Story = {
  args: {
    title: 'Edit tenant',
    submitLabel: 'Save',
    initialValues: acme,
    isTenantIdLocked: true,
  },
};

export const Submitting: Story = {
  args: {
    title: 'Edit tenant',
    submitLabel: 'Save',
    initialValues: acme,
    isTenantIdLocked: true,
    isSubmitting: true,
  },
};

export const WithError: Story = {
  args: {
    title: 'Create tenant',
    submitLabel: 'Create',
    initialValues: emptyTenantFormValues,
    error: 'A tenant with the specified identifier already exists.',
  },
};

import type { Meta, StoryObj } from '@storybook/react';
import { SecretFormDialog, emptySecretFormValues } from './SecretFormDialog.js';

const meta: Meta<typeof SecretFormDialog> = {
  title: 'Management/SecretFormDialog',
  component: SecretFormDialog,
};
export default meta;

type Story = StoryObj<typeof SecretFormDialog>;

export const Create: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' },
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

export const WithError: Story = {
  args: {
    open: true,
    initialValues: { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' },
    error: 'The secret could not be generated from the supplied parameters.',
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};

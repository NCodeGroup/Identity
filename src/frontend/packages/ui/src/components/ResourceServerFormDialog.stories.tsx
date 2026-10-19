import type { Meta, StoryObj } from '@storybook/react-vite';
import {
  ResourceServerFormDialog,
  emptyResourceServerFormValues,
} from './ResourceServerFormDialog.js';

const meta: Meta<typeof ResourceServerFormDialog> = {
  title: 'Management/ResourceServerFormDialog',
  component: ResourceServerFormDialog,
  args: {
    open: true,
    onSubmit: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof ResourceServerFormDialog>;

export const Create: Story = {
  args: {
    title: 'Create resource server',
    submitLabel: 'Create',
    initialValues: emptyResourceServerFormValues,
  },
};

export const Edit: Story = {
  args: {
    title: 'Edit resource server',
    submitLabel: 'Save',
    identifierEditable: false,
    initialValues: {
      identifier: 'https://orders.example.com',
      name: 'Orders API',
      isDisabled: false,
    },
  },
};

export const WithError: Story = {
  args: {
    title: 'Create resource server',
    submitLabel: 'Create',
    initialValues: emptyResourceServerFormValues,
    error: 'A resource server with that identifier already exists.',
  },
};

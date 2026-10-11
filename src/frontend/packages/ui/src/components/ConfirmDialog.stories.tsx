import type { Meta, StoryObj } from '@storybook/react';
import { ConfirmDialog } from './ConfirmDialog.js';

const meta: Meta<typeof ConfirmDialog> = {
  title: 'Management/ConfirmDialog',
  component: ConfirmDialog,
  args: {
    open: true,
    title: 'Delete tenant',
    message: 'Delete “Acme Inc”? This cannot be undone.',
    onConfirm: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof ConfirmDialog>;

export const Default: Story = {};
export const Busy: Story = { args: { isBusy: true } };
export const WithError: Story = {
  args: { error: 'The tenant still has clients and cannot be deleted.' },
};

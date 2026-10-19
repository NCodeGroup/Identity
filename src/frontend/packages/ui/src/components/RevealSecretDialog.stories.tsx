import type { Meta, StoryObj } from '@storybook/react-vite';
import { RevealSecretDialog } from './RevealSecretDialog.js';

const meta: Meta<typeof RevealSecretDialog> = {
  title: 'Management/RevealSecretDialog',
  component: RevealSecretDialog,
};
export default meta;

type Story = StoryObj<typeof RevealSecretDialog>;

export const Symmetric: Story = {
  args: {
    open: true,
    secretId: 's-1',
    secretMaterial: 'ZmFrZS1zZWNyZXQtbWF0ZXJpYWw',
    onClose: () => undefined,
  },
};

export const Asymmetric: Story = {
  args: {
    open: true,
    secretId: 's-2',
    secretMaterial: null,
    onClose: () => undefined,
  },
};

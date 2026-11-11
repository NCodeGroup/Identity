import type { Meta, StoryObj } from '@storybook/react-vite';
import { BootstrapScreen } from './BootstrapScreen.js';

const meta: Meta<typeof BootstrapScreen> = {
  title: 'Shell/BootstrapScreen',
  component: BootstrapScreen,
};
export default meta;

type Story = StoryObj<typeof BootstrapScreen>;

export const Loading: Story = {};
export const Failed: Story = {
  args: { error: "Failed to fetch 'https://id.example/config/bootstrap.json' (HTTP 503)." },
};

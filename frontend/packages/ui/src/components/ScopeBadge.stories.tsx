import type { Meta, StoryObj } from '@storybook/react-vite';
import { ScopeBadge } from './ScopeBadge.js';

const meta: Meta<typeof ScopeBadge> = {
  title: 'Boundary/ScopeBadge',
  component: ScopeBadge,
  args: { kind: 'tenant' },
};
export default meta;

type Story = StoryObj<typeof ScopeBadge>;

export const Tenant: Story = { args: { kind: 'tenant' } };
export const Team: Story = { args: { kind: 'team' } };
export const Platform: Story = { args: { kind: 'platform' } };
export const CustomLabel: Story = { args: { kind: 'tenant', label: 'acme' } };

import type { Meta, StoryObj } from '@storybook/react';
import { ScopeBadge } from './ScopeBadge.js';

const meta: Meta<typeof ScopeBadge> = {
  title: 'Boundary/ScopeBadge',
  component: ScopeBadge,
  args: { kind: 'tenant' },
};
export default meta;

type Story = StoryObj<typeof ScopeBadge>;

export const Tenant: Story = { args: { kind: 'tenant' } };
export const Global: Story = { args: { kind: 'global' } };
export const CustomLabel: Story = { args: { kind: 'tenant', label: 'acme' } };

import type { Meta, StoryObj } from '@storybook/react-vite';
import { TenantUsageTable } from './TenantUsageTable.js';

const meta: Meta<typeof TenantUsageTable> = {
  title: 'Management/TenantUsageTable',
  component: TenantUsageTable,
};
export default meta;

type Story = StoryObj<typeof TenantUsageTable>;

export const Populated: Story = {
  args: {
    entries: [
      { day: '2026-10-20', meter: 'requests', count: 12840 },
      { day: '2026-10-20', meter: 'token_requests', count: 1310 },
      { day: '2026-10-21', meter: 'requests', count: 9021 },
      { day: '2026-10-21', meter: 'token_requests', count: 877 },
    ],
  },
};

export const SparseMeters: Story = {
  args: {
    entries: [
      { day: '2026-10-20', meter: 'requests', count: 40 },
      { day: '2026-10-21', meter: 'token_requests', count: 3 },
    ],
  },
};

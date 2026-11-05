import type { Meta, StoryObj } from '@storybook/react-vite';
import { TeamUsageTable } from './TeamUsageTable.js';

const meta: Meta<typeof TeamUsageTable> = {
  title: 'Management/TeamUsageTable',
  component: TeamUsageTable,
};
export default meta;

type Story = StoryObj<typeof TeamUsageTable>;

export const Populated: Story = {
  args: {
    entries: [
      { tenantId: 'acme', day: '2026-10-20', meter: 'requests', count: 12840 },
      { tenantId: 'acme', day: '2026-10-20', meter: 'token_requests', count: 1310 },
      { tenantId: 'globex', day: '2026-10-20', meter: 'requests', count: 4021 },
      { tenantId: 'acme', day: '2026-10-21', meter: 'requests', count: 9021 },
      { tenantId: 'globex', day: '2026-10-21', meter: 'token_requests', count: 877 },
    ],
  },
};

export const SparseMeters: Story = {
  args: {
    entries: [
      { tenantId: 'acme', day: '2026-10-20', meter: 'requests', count: 40 },
      { tenantId: 'globex', day: '2026-10-21', meter: 'token_requests', count: 3 },
    ],
  },
};

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
      { teamId: 'acme', tenantId: 'acme', day: '2026-10-20', meter: 'requests', count: 12840 },
      { teamId: 'acme', tenantId: 'acme', day: '2026-10-20', meter: 'token_requests', count: 1310 },
      { teamId: 'globex', tenantId: 'globex', day: '2026-10-20', meter: 'requests', count: 4021 },
      { teamId: 'acme', tenantId: 'acme', day: '2026-10-21', meter: 'requests', count: 9021 },
      {
        teamId: 'globex',
        tenantId: 'globex',
        day: '2026-10-21',
        meter: 'token_requests',
        count: 877,
      },
    ],
  },
};

export const SparseMeters: Story = {
  args: {
    entries: [
      { teamId: 'acme', tenantId: 'acme', day: '2026-10-20', meter: 'requests', count: 40 },
      {
        teamId: 'globex',
        tenantId: 'globex',
        day: '2026-10-21',
        meter: 'token_requests',
        count: 3,
      },
    ],
  },
};

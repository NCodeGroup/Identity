import type { Meta, StoryObj } from '@storybook/react-vite';
import {
  TenantPromotionDialog,
  type TenantPromotionDialogReport,
} from './TenantPromotionDialog.js';

const meta: Meta<typeof TenantPromotionDialog> = {
  title: 'Management/TenantPromotionDialog',
  component: TenantPromotionDialog,
  args: {
    open: true,
    tenantIds: ['acme-dev', 'acme-prod'],
    isBusy: false,
    onPreview: () => undefined,
    onPromote: () => undefined,
    onCancel: () => undefined,
  },
};
export default meta;

type Story = StoryObj<typeof TenantPromotionDialog>;

const previewed: TenantPromotionDialogReport = {
  sourceTenantId: 'acme-dev',
  targetTenantId: 'acme-prod',
  isApplied: false,
  changes: [
    { resourceType: 'resource_server', resourceId: 'orders-api', action: 'created' },
    { resourceType: 'client', resourceId: 'web-app', action: 'updated' },
  ],
  notes: [
    {
      resourceType: 'client',
      resourceId: 'web-app',
      code: 'credential_required',
      detail: 'The client needs a secret created in the target tenant.',
    },
    {
      resourceType: 'client',
      resourceId: 'web-app',
      code: 'environment_setting_unset',
      detail: 'The setting is bound to the environment and has no value in the target tenant.',
      settingName: 'redirect_uris',
    },
    {
      resourceType: 'client',
      resourceId: 'console',
      code: 'host_managed',
      detail: 'The host manages this client.',
    },
  ],
  errors: [],
};

export const Empty: Story = {};

export const Previewed: Story = {
  args: { report: previewed },
};

export const WithErrors: Story = {
  args: {
    report: {
      sourceTenantId: 'acme-dev',
      targetTenantId: 'acme-prod',
      isApplied: false,
      changes: [],
      notes: [],
      errors: [
        {
          resourceType: 'client',
          resourceId: 'console',
          detail: 'The client is managed by the host and cannot be changed.',
        },
        {
          resourceType: 'resource_server',
          resourceId: 'orders-api',
          detail: 'The resource server has an unreadable settings document.',
        },
      ],
    },
  },
};

export const WithError: Story = {
  args: {
    error: "The caller may not change the limit 'max_clients' of the tenant 'acme-prod'.",
  },
};

export const Busy: Story = {
  args: { report: previewed, isBusy: true },
};

export const Applied: Story = {
  args: { report: { ...previewed, isApplied: true } },
};

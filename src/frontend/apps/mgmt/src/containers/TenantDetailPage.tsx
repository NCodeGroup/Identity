import { useState } from 'react';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import {
  useCreateTenantSecretMutation,
  useDeleteTenantSecretMutation,
  useGetTenantEffectiveSettingsQuery,
  useGetTenantSettingsQuery,
  useListTenantSecretsQuery,
  useUpdateTenantSettingsMutation,
} from '@ncode/store';
import { SecretsManager } from './SecretsManager.js';
import { SettingsManager } from './SettingsManager.js';

export type TenantDetailPageProps = {
  readonly tenantId: string;
};

type TenantSurface = 'secrets' | 'settings';

/**
 * Container: the current tenant's own secrets and settings surface (as opposed to its clients'). Mirrors the
 * client Secrets/Settings pattern on the tenant-level nested endpoints, wiring the shared
 * {@link SecretsManager}/{@link SettingsManager}. Owns only tab state; the tenant id comes from the active
 * scope, so the boundary stays structural (a tenant administrator can never address another tenant).
 */
export function TenantDetailPage({ tenantId }: TenantDetailPageProps) {
  const [surface, setSurface] = useState<TenantSurface>('secrets');

  return (
    <>
      <Stack mb={2}>
        <Typography variant="h5" component="h2">
          Tenant
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {tenantId}
        </Typography>
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: TenantSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="secrets" label="Secrets" data-testid="tenant-detail-tab-secrets" />
        <Tab value="settings" label="Settings" data-testid="tenant-detail-tab-settings" />
      </Tabs>
      {surface === 'secrets' ? (
        <TenantSecretsTab tenantId={tenantId} />
      ) : (
        <TenantSettingsTab tenantId={tenantId} />
      )}
    </>
  );
}

function TenantSecretsTab({ tenantId }: TenantDetailPageProps) {
  const { data, isLoading, error } = useListTenantSecretsQuery({ tenantId });
  const [createSecret, { isLoading: isCreating }] = useCreateTenantSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteTenantSecretMutation();

  return (
    <SecretsManager
      ownerLabel="tenant"
      secrets={data?.secrets ?? []}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isCreating={isCreating}
      isDeleting={isDeleting}
      onCreate={(body) => createSecret({ tenantId, body }).unwrap()}
      onDelete={(secretId) => deleteSecret({ tenantId, secretId }).unwrap()}
    />
  );
}

function TenantSettingsTab({ tenantId }: TenantDetailPageProps) {
  const { data: settings, isLoading, error } = useGetTenantSettingsQuery({ tenantId });
  const { data: effective } = useGetTenantEffectiveSettingsQuery({ tenantId });
  const [updateSettings, { isLoading: isSaving }] = useUpdateTenantSettingsMutation();

  return (
    <SettingsManager
      settings={settings}
      effective={effective}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isSaving={isSaving}
      onSave={({ concurrencyToken, original, desired }) =>
        updateSettings({ tenantId, concurrencyToken, original, desired }).unwrap()
      }
    />
  );
}

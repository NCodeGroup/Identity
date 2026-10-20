import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ClientResource, ManagementScope } from '@ncode/api';
import {
  useCreateClientSecretMutation,
  useDeleteClientSecretMutation,
  useGetClientEffectiveSettingsQuery,
  useGetClientSettingsQuery,
  useLazyGetClientSecretPublicKeyQuery,
  useListClientSecretsQuery,
  useUpdateClientSettingsMutation,
} from '@ncode/store';
import { SecretsManager } from './SecretsManager.js';
import { SettingsManager } from './SettingsManager.js';

export type ClientDetailPageProps = {
  readonly scope: ManagementScope;
  readonly client: ClientResource;
  readonly onBack: () => void;
};

type ClientSurface = 'secrets' | 'settings';

/**
 * Container: drills into a single client to manage its secrets and settings. The parent
 * {@link import('./ClientsPage.js').ClientsPage} supplies the client and owns the drill-in selection.
 * Secrets honor the one-time reveal (ADR-0050) and settings are a top-level JSON Patch diff; the shared
 * {@link SecretsManager}/{@link SettingsManager} render both, wired to the client's nested endpoints.
 * Owns only tab state.
 */
export function ClientDetailPage({ scope, client, onBack }: ClientDetailPageProps) {
  const [surface, setSurface] = useState<ClientSurface>('secrets');

  return (
    <>
      <Button
        size="small"
        startIcon={<ArrowBackIcon />}
        onClick={onBack}
        data-testid="client-detail-back"
        sx={{ mb: 1 }}
      >
        Clients
      </Button>
      <Stack sx={{ mb: 2 }}>
        <Typography variant="h5" component="h2">
          {client.clientId}
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          Client · {client.isDisabled ? 'Disabled' : 'Active'}
          {client.resourceServerId ? ` · Acts for resource server ${client.resourceServerId}` : ''}
        </Typography>
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: ClientSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="secrets" label="Secrets" data-testid="client-tab-secrets" />
        <Tab value="settings" label="Settings" data-testid="client-tab-settings" />
      </Tabs>
      {surface === 'secrets' ? (
        <ClientSecretsTab scope={scope} clientId={client.clientId} />
      ) : (
        <ClientSettingsTab scope={scope} clientId={client.clientId} />
      )}
    </>
  );
}

type ClientTabProps = {
  readonly scope: ManagementScope;
  readonly clientId: string;
};

function ClientSecretsTab({ scope, clientId }: ClientTabProps) {
  const { data, isLoading, error } = useListClientSecretsQuery({ scope, clientId });
  const [createSecret, { isLoading: isCreating }] = useCreateClientSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteClientSecretMutation();
  const [getPublicKey] = useLazyGetClientSecretPublicKeyQuery();

  return (
    <SecretsManager
      ownerLabel="client"
      secrets={data?.secrets ?? []}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isCreating={isCreating}
      isDeleting={isDeleting}
      onCreate={(body) => createSecret({ scope, clientId, body }).unwrap()}
      onDelete={(secretId) => deleteSecret({ scope, clientId, secretId }).unwrap()}
      onExportPublicKey={(secretId) => getPublicKey({ scope, clientId, secretId }).unwrap()}
    />
  );
}

function ClientSettingsTab({ scope, clientId }: ClientTabProps) {
  const { data: settings, isLoading, error } = useGetClientSettingsQuery({ scope, clientId });
  const { data: effective } = useGetClientEffectiveSettingsQuery({ scope, clientId });
  const [updateSettings, { isLoading: isSaving }] = useUpdateClientSettingsMutation();

  return (
    <SettingsManager
      settings={settings}
      effective={effective}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isSaving={isSaving}
      onSave={({ concurrencyToken, original, desired }) =>
        updateSettings({ scope, clientId, concurrencyToken, original, desired }).unwrap()
      }
    />
  );
}

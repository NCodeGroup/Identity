import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ServerResource } from '@ncode/api';
import {
  useCreateServerSecretMutation,
  useDeleteServerSecretMutation,
  useGetServerEffectiveSettingsQuery,
  useGetServerSettingsQuery,
  useLazyGetServerSecretPublicKeyQuery,
  useListServerSecretsQuery,
  useUpdateServerSettingsMutation,
} from '@ncode/store';
import { SecretsManager } from './SecretsManager.js';
import { SettingsManager } from './SettingsManager.js';

export type ServerDetailPageProps = {
  readonly server: ServerResource;
  readonly onBack: () => void;
};

type ServerSurface = 'secrets' | 'settings';

/**
 * Container: drills into a single (global-scope) server to manage its secrets and settings. Mirrors the
 * client Secrets/Settings pattern on the server-level endpoints, wiring the shared
 * {@link SecretsManager}/{@link SettingsManager}. Owns only tab state; the parent
 * {@link import('./ServersPage.js').ServersPage} supplies the server and owns the drill-in selection.
 */
export function ServerDetailPage({ server, onBack }: ServerDetailPageProps) {
  const [surface, setSurface] = useState<ServerSurface>('secrets');

  return (
    <>
      <Button
        size="small"
        startIcon={<ArrowBackIcon />}
        onClick={onBack}
        data-testid="server-detail-back"
        sx={{ mb: 1 }}
      >
        Servers
      </Button>
      <Stack sx={{ mb: 2 }}>
        <Typography variant="h5" component="h2">
          {server.displayName}
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          Server · {server.serverId}
        </Typography>
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: ServerSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="secrets" label="Secrets" data-testid="server-tab-secrets" />
        <Tab value="settings" label="Settings" data-testid="server-tab-settings" />
      </Tabs>
      {surface === 'secrets' ? (
        <ServerSecretsTab serverId={server.serverId} />
      ) : (
        <ServerSettingsTab serverId={server.serverId} />
      )}
    </>
  );
}

type ServerTabProps = {
  readonly serverId: string;
};

function ServerSecretsTab({ serverId }: ServerTabProps) {
  const { data, isLoading, error } = useListServerSecretsQuery({ serverId });
  const [createSecret, { isLoading: isCreating }] = useCreateServerSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteServerSecretMutation();
  const [getPublicKey] = useLazyGetServerSecretPublicKeyQuery();

  return (
    <SecretsManager
      ownerLabel="server"
      secrets={data?.secrets ?? []}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isCreating={isCreating}
      isDeleting={isDeleting}
      onCreate={(body) => createSecret({ serverId, body }).unwrap()}
      onDelete={(secretId) => deleteSecret({ serverId, secretId }).unwrap()}
      onExportPublicKey={(secretId) => getPublicKey({ serverId, secretId }).unwrap()}
    />
  );
}

function ServerSettingsTab({ serverId }: ServerTabProps) {
  const { data: settings, isLoading, error } = useGetServerSettingsQuery({ serverId });
  const { data: effective } = useGetServerEffectiveSettingsQuery({ serverId });
  const [updateSettings, { isLoading: isSaving }] = useUpdateServerSettingsMutation();

  return (
    <SettingsManager
      settings={settings}
      effective={effective}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isSaving={isSaving}
      onSave={({ concurrencyToken, original, desired }) =>
        updateSettings({ serverId, concurrencyToken, original, desired }).unwrap()
      }
    />
  );
}

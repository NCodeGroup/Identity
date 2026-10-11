import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ManagementScope, ResourceServerResource } from '@ncode/api';
import {
  useCreateResourceServerSecretMutation,
  useDeleteResourceServerSecretMutation,
  useGetResourceServerEffectiveSettingsQuery,
  useGetResourceServerSettingsQuery,
  useListResourceServerSecretsQuery,
  useUpdateResourceServerSettingsMutation,
} from '@ncode/store';
import { ResourceServerScopesPage } from './ResourceServerScopesPage.js';
import { SecretsManager } from './SecretsManager.js';
import { SettingsManager } from './SettingsManager.js';

export type ResourceServerDetailPageProps = {
  readonly scope: ManagementScope;
  readonly resourceServer: ResourceServerResource;
  readonly onBack: () => void;
};

type ResourceServerSurface = 'scopes' | 'secrets' | 'settings';

/**
 * Container: drills into a single resource server (API) to manage its scopes, secrets, and settings. The parent
 * {@link import('./ResourceServersPage.js').ResourceServersPage} supplies the resource server and owns the drill-in
 * selection. Scopes reuse the embedded {@link ResourceServerScopesPage}; secrets/settings reuse the shared
 * {@link SecretsManager}/{@link SettingsManager} (one-time reveal per ADR-0050 and a top-level JSON Patch diff),
 * wired to the resource server's nested endpoints. Owns only tab state.
 */
export function ResourceServerDetailPage({
  scope,
  resourceServer,
  onBack,
}: ResourceServerDetailPageProps) {
  const [surface, setSurface] = useState<ResourceServerSurface>('scopes');

  return (
    <>
      <Button
        size="small"
        startIcon={<ArrowBackIcon />}
        onClick={onBack}
        data-testid="resource-server-detail-back"
        sx={{ mb: 1 }}
      >
        Resource servers
      </Button>
      <Stack mb={2}>
        <Typography variant="h5" component="h2">
          {resourceServer.name}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Resource server · {resourceServer.identifier}
        </Typography>
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: ResourceServerSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="scopes" label="Scopes" data-testid="resource-server-tab-scopes" />
        <Tab value="secrets" label="Secrets" data-testid="resource-server-tab-secrets" />
        <Tab value="settings" label="Settings" data-testid="resource-server-tab-settings" />
      </Tabs>
      {surface === 'scopes' ? (
        <ResourceServerScopesPage scope={scope} resourceServer={resourceServer} embedded />
      ) : surface === 'secrets' ? (
        <ResourceServerSecretsTab
          scope={scope}
          resourceServerId={resourceServer.resourceServerId}
        />
      ) : (
        <ResourceServerSettingsTab
          scope={scope}
          resourceServerId={resourceServer.resourceServerId}
        />
      )}
    </>
  );
}

type ResourceServerTabProps = {
  readonly scope: ManagementScope;
  readonly resourceServerId: string;
};

function ResourceServerSecretsTab({ scope, resourceServerId }: ResourceServerTabProps) {
  const { data, isLoading, error } = useListResourceServerSecretsQuery({ scope, resourceServerId });
  const [createSecret, { isLoading: isCreating }] = useCreateResourceServerSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteResourceServerSecretMutation();

  return (
    <SecretsManager
      ownerLabel="resource server"
      secrets={data?.secrets ?? []}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isCreating={isCreating}
      isDeleting={isDeleting}
      onCreate={(body) => createSecret({ scope, resourceServerId, body }).unwrap()}
      onDelete={(secretId) => deleteSecret({ scope, resourceServerId, secretId }).unwrap()}
    />
  );
}

function ResourceServerSettingsTab({ scope, resourceServerId }: ResourceServerTabProps) {
  const {
    data: settings,
    isLoading,
    error,
  } = useGetResourceServerSettingsQuery({ scope, resourceServerId });
  const { data: effective } = useGetResourceServerEffectiveSettingsQuery({
    scope,
    resourceServerId,
  });
  const [updateSettings, { isLoading: isSaving }] = useUpdateResourceServerSettingsMutation();

  return (
    <SettingsManager
      settings={settings}
      effective={effective}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isSaving={isSaving}
      onSave={({ concurrencyToken, original, desired }) =>
        updateSettings({ scope, resourceServerId, concurrencyToken, original, desired }).unwrap()
      }
    />
  );
}

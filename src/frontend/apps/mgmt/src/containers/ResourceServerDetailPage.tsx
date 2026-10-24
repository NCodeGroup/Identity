import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ManagementOwner, ManagementScope, ResourceServerResource } from '@ncode/api';
import { ClientsPage } from './ClientsPage.js';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';
import { ResourceServerScopesPage } from './ResourceServerScopesPage.js';

export type ResourceServerDetailPageProps = {
  readonly scope: ManagementScope;
  readonly resourceServer: ResourceServerResource;
  readonly onBack: () => void;
};

type ResourceServerSurface = 'scopes' | 'clients' | 'secrets' | 'settings';

/**
 * Container: drills into a single resource server (API) to manage its scopes, the clients that act for
 * it, its secrets, and its settings. The parent
 * {@link import('./ResourceServersPage.js').ResourceServersPage} supplies the resource server and owns
 * the drill-in selection. Scopes reuse the embedded {@link ResourceServerScopesPage} and clients the
 * embedded {@link ClientsPage}; secrets/settings reuse the owner-generic {@link OwnerSecretsTab}/
 * {@link OwnerSettingsTab} (one-time reveal per ADR-0050 and a top-level JSON Patch diff). A system
 * resource server is read by the server itself, so it holds no keys and no client acts for it: those
 * tabs are not offered. Owns only tab state.
 */
export function ResourceServerDetailPage({
  scope,
  resourceServer,
  onBack,
}: ResourceServerDetailPageProps) {
  const [surface, setSurface] = useState<ResourceServerSurface>('scopes');
  const isSystem = resourceServer.isSystem;
  const owner: ManagementOwner = {
    kind: 'resource-server',
    scope,
    resourceServerId: resourceServer.resourceServerId,
  };

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
      <Stack sx={{ mb: 2 }}>
        <Typography variant="h5" component="h2">
          {resourceServer.name}
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          Resource server · {resourceServer.identifier}
        </Typography>
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: ResourceServerSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="scopes" label="Scopes" data-testid="resource-server-tab-scopes" />
        {!isSystem && (
          <Tab value="clients" label="Clients" data-testid="resource-server-tab-clients" />
        )}
        {!isSystem && (
          <Tab value="secrets" label="Secrets" data-testid="resource-server-tab-secrets" />
        )}
        <Tab value="settings" label="Settings" data-testid="resource-server-tab-settings" />
      </Tabs>
      {surface === 'scopes' ? (
        <ResourceServerScopesPage scope={scope} resourceServer={resourceServer} embedded />
      ) : surface === 'clients' ? (
        <ClientsPage scope={scope} resourceServerId={resourceServer.resourceServerId} embedded />
      ) : surface === 'secrets' ? (
        <OwnerSecretsTab owner={owner} ownerLabel="resource server" />
      ) : (
        <OwnerSettingsTab owner={owner} />
      )}
    </>
  );
}

import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ClientResource, SecretOwner } from '@ncode/api';
import { OwnerAccessTab } from './OwnerAccessTab.js';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';

export type ClientDetailPageProps = {
  readonly client: ClientResource;
  readonly onBack: () => void;
};

type ClientSurface = 'secrets' | 'settings' | 'access';

/** Names the node that owns a machine client, such as `Machine client of team acme`. */
function machineClientLabel(client: ClientResource): string {
  return client.owningNodeType === 'platform'
    ? 'Machine client of the platform'
    : `Machine client of ${client.owningNodeType} ${client.owningNodeId}`;
}

/**
 * Container: drills into a single client to manage its secrets, settings and owners. The parent
 * {@link import('./ClientsPage.js').ClientsPage} supplies the client and owns the drill-in selection.
 * The container addresses the client through the client's own tenant, so a machine client opened from the
 * platform or a team reaches the control-plane tenant's routes (ADR-1257, ADR-1466).
 * Secrets honor the one-time reveal (ADR-1050) and settings are a top-level JSON Patch diff; the owner-generic
 * {@link OwnerSecretsTab}/{@link OwnerSettingsTab} render both for the client, and {@link OwnerAccessTab} lists
 * and edits the client's owners. Owns only tab state.
 */
export function ClientDetailPage({ client, onBack }: ClientDetailPageProps) {
  const [surface, setSurface] = useState<ClientSurface>('secrets');
  const owner: SecretOwner = {
    kind: 'client',
    tenantId: client.tenantId,
    clientId: client.clientId,
  };

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
          {client.owningNodeType !== null ? ` · ${machineClientLabel(client)}` : ''}
        </Typography>
        {client.owningNodeType !== null ? (
          <Typography
            variant="body2"
            sx={{ color: 'text.secondary' }}
            data-testid="client-principal"
          >
            Principal {client.principalId}
          </Typography>
        ) : null}
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: ClientSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="secrets" label="Secrets" data-testid="client-tab-secrets" />
        <Tab value="settings" label="Settings" data-testid="client-tab-settings" />
        <Tab value="access" label="Access" data-testid="client-tab-access" />
      </Tabs>
      {surface === 'secrets' ? (
        <OwnerSecretsTab owner={owner} ownerLabel="client" />
      ) : surface === 'access' ? (
        <OwnerAccessTab owner={owner} />
      ) : (
        <OwnerSettingsTab owner={owner} />
      )}
    </>
  );
}

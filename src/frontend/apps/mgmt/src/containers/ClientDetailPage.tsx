import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ClientResource, ManagementOwner, ManagementScope } from '@ncode/api';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';

export type ClientDetailPageProps = {
  readonly scope: ManagementScope;
  readonly client: ClientResource;
  readonly onBack: () => void;
};

type ClientSurface = 'secrets' | 'settings';

/**
 * Container: drills into a single client to manage its secrets and settings. The parent
 * {@link import('./ClientsPage.js').ClientsPage} supplies the client and owns the drill-in selection.
 * Secrets honor the one-time reveal (ADR-0050) and settings are a top-level JSON Patch diff; the owner-generic
 * {@link OwnerSecretsTab}/{@link OwnerSettingsTab} render both for the client. Owns only tab state.
 */
export function ClientDetailPage({ scope, client, onBack }: ClientDetailPageProps) {
  const [surface, setSurface] = useState<ClientSurface>('secrets');
  const owner: ManagementOwner = { kind: 'client', scope, clientId: client.clientId };

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
        <OwnerSecretsTab owner={owner} ownerLabel="client" />
      ) : (
        <OwnerSettingsTab owner={owner} />
      )}
    </>
  );
}

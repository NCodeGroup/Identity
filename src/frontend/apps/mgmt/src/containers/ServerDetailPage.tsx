import { useState } from 'react';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import type { ManagementOwner, ServerResource } from '@ncode/api';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';

export type ServerDetailPageProps = {
  readonly server: ServerResource;
  readonly onBack: () => void;
};

type ServerSurface = 'secrets' | 'settings';

/**
 * Container: drills into a single (global-scope) server to manage its secrets and settings, wiring the
 * owner-generic {@link OwnerSecretsTab}/{@link OwnerSettingsTab}. Owns only tab state; the parent
 * {@link import('./ServersPage.js').ServersPage} supplies the server and owns the drill-in selection.
 */
export function ServerDetailPage({ server, onBack }: ServerDetailPageProps) {
  const [surface, setSurface] = useState<ServerSurface>('secrets');
  const owner: ManagementOwner = { kind: 'server', serverId: server.serverId };

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
        <OwnerSecretsTab owner={owner} ownerLabel="server" />
      ) : (
        <OwnerSettingsTab owner={owner} />
      )}
    </>
  );
}

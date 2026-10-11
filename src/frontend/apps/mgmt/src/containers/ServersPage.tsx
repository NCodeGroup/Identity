import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { DataStateBoundary, ServerTable } from '@ncode/ui';
import { useListServersQuery } from '@ncode/store';
import { ServerDetailPage } from './ServerDetailPage.js';

/**
 * Container: the global-scope server browser. Servers are system-level settings/secret owners; this lists
 * them and drills into {@link ServerDetailPage} to manage a server's secrets and settings. There is no
 * create/edit/delete here — the server inventory is provisioned out of band. Owns only drill-in selection.
 */
export function ServersPage() {
  const { data, isLoading, error } = useListServersQuery();
  const servers = data?.items ?? [];

  const [selectedId, setSelectedId] = useState<string>();
  const selected = servers.find((s) => s.serverId === selectedId);
  if (selected) {
    return <ServerDetailPage server={selected} onBack={() => setSelectedId(undefined)} />;
  }

  return (
    <>
      <Typography variant="h5" component="h2" mb={1}>
        Servers
      </Typography>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load servers.' : undefined}
        isEmpty={servers.length === 0}
        emptyMessage="No servers are provisioned."
      >
        <ServerTable
          servers={servers.map((s) => ({ serverId: s.serverId, displayName: s.displayName }))}
          onSelect={setSelectedId}
        />
      </DataStateBoundary>
    </>
  );
}

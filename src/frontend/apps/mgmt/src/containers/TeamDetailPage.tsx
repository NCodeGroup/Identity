import { useState } from 'react';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { apiErrorMessage, type ApiError, useGetTeamUsageQuery } from '@ncode/store';
import { DataStateBoundary, TeamUsageTable } from '@ncode/ui';
import type { TeamSurface } from '../routes.js';
import { MachineClientsPage } from './MachineClientsPage.js';
import { OwnerAccessTab } from './OwnerAccessTab.js';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';
import { TeamsPage } from './TeamsPage.js';
import { TenantsPage } from './TenantsPage.js';

export type TeamDetailPageProps = {
  readonly teamId: string;
  /** The team surface the address bar names. */
  readonly surface: TeamSurface;
  /** Opens a tenant of the team. */
  readonly onOpenTenant: (tenantId: string) => void;
  /** Opens a child team of the team. */
  readonly onOpenTeam: (teamId: string) => void;
};

type TeamTabProps = {
  readonly teamId: string;
};

/**
 * Container: one team's tenants, child teams, machine clients, settings, access, or usage surface, as the `surface`
 * prop names. The address bar selects the surface (ADR-1255), so this page holds no tab state. Wires the team's
 * tenant list to {@link TenantsPage}, its child team list to {@link TeamsPage}, the team's machine clients to
 * {@link MachineClientsPage}, the owner-generic {@link OwnerSettingsTab} to the team, lists and manages the team's
 * administrators and owners through {@link OwnerAccessTab}, and shows the usage ledger of the team's tenants and of
 * its descendant teams' tenants (ADR-1199, ADR-1426) through {@link TeamUsageTable}.
 */
export function TeamDetailPage({ teamId, surface, onOpenTenant, onOpenTeam }: TeamDetailPageProps) {
  return (
    <>
      <Stack sx={{ mb: 2 }}>
        <Typography variant="h5" component="h2">
          Team
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {teamId}
        </Typography>
      </Stack>
      {surface === 'tenants' && <TenantsPage teamId={teamId} onOpenTenant={onOpenTenant} />}
      {surface === 'teams' && <TeamsPage parentTeamId={teamId} onOpenTeam={onOpenTeam} />}
      {surface === 'clients' && <MachineClientsPage owner={{ kind: 'team', teamId }} />}
      {surface === 'settings' && <OwnerSettingsTab owner={{ kind: 'team', teamId }} />}
      {surface === 'access' && <OwnerAccessTab owner={{ kind: 'team', teamId }} />}
      {surface === 'usage' && <TeamUsageTab teamId={teamId} />}
    </>
  );
}

/**
 * The team's usage ledger for a range of UTC days. An empty bound is left to the server, which defaults the range to
 * the last 30 days and refuses one longer than 366 days; the effective range is echoed back and shown.
 */
function TeamUsageTab({ teamId }: TeamTabProps) {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data, isFetching, error } = useGetTeamUsageQuery({
    teamId,
    from: from || undefined,
    to: to || undefined,
  });

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
        <TextField
          type="date"
          size="small"
          label="From (UTC)"
          value={from}
          onChange={(event) => setFrom(event.target.value)}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { 'data-testid': 'usage-from' } }}
        />
        <TextField
          type="date"
          size="small"
          label="To (UTC)"
          value={to}
          onChange={(event) => setTo(event.target.value)}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { 'data-testid': 'usage-to' } }}
        />
        {data && (
          <Typography variant="body2" sx={{ color: 'text.secondary' }} data-testid="usage-range">
            {data.from} to {data.to}
          </Typography>
        )}
      </Stack>
      <DataStateBoundary
        isLoading={isFetching && !data}
        error={error ? apiErrorMessage(error as ApiError) : undefined}
        isEmpty={(data?.entries.length ?? 0) === 0}
        emptyMessage="No usage recorded in this range."
      >
        <TeamUsageTable entries={data?.entries ?? []} />
      </DataStateBoundary>
    </Stack>
  );
}

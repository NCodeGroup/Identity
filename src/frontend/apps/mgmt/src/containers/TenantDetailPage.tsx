import { useState } from 'react';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { ManagementOwner } from '@ncode/api';
import { apiErrorMessage, type ApiError, useGetTenantUsageQuery } from '@ncode/store';
import { DataStateBoundary, TenantUsageTable } from '@ncode/ui';
import type { TenantOwnSurface } from '../routes.js';
import { OwnerSecretsTab } from './OwnerSecretsTab.js';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';

export type TenantDetailPageProps = {
  readonly tenantId: string;
  /** The tenant surface the address bar names. */
  readonly surface: TenantOwnSurface;
};

type TenantTabProps = {
  readonly tenantId: string;
};

/**
 * Container: the current tenant's own secrets, settings, or usage surface (as opposed to its clients'), as the
 * `surface` prop names. The address bar selects the surface (ADR-0255), so this page holds no tab state. Wires the
 * owner-generic {@link OwnerSecretsTab}/{@link OwnerSettingsTab} to the tenant, and shows the tenant's usage ledger
 * (ADR-0199) through {@link TenantUsageTable}. The tenant id comes from the active scope, so the boundary stays
 * structural (a tenant administrator can never address another tenant).
 */
export function TenantDetailPage({ tenantId, surface }: TenantDetailPageProps) {
  const owner: ManagementOwner = { kind: 'tenant', tenantId };

  return (
    <>
      <Stack sx={{ mb: 2 }}>
        <Typography variant="h5" component="h2">
          Tenant
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {tenantId}
        </Typography>
      </Stack>
      {surface === 'secrets' && <OwnerSecretsTab owner={owner} ownerLabel="tenant" />}
      {surface === 'settings' && <OwnerSettingsTab owner={owner} />}
      {surface === 'usage' && <TenantUsageTab tenantId={tenantId} />}
    </>
  );
}

/**
 * The tenant's usage ledger for a range of UTC days. An empty bound is left to the server, which defaults the range to
 * the last 30 days and refuses one longer than 366 days; the effective range is echoed back and shown.
 */
function TenantUsageTab({ tenantId }: TenantTabProps) {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data, isFetching, error } = useGetTenantUsageQuery({
    tenantId,
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
        <TenantUsageTable entries={data?.entries ?? []} />
      </DataStateBoundary>
    </Stack>
  );
}

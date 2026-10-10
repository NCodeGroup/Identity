import Typography from '@mui/material/Typography';
import { DataStateBoundary, TenantTable } from '@ncode/ui';
import { useListTenantsQuery } from '@ncode/store';

export type TenantsPageProps = {
  readonly onOpenTenant: (tenantId: string) => void;
};

/**
 * Container: the global-scope tenant browser. Wires the RTK Query tenant list to the stateless
 * {@link TenantTable}. Selecting a tenant crosses into that tenant's security boundary.
 */
export function TenantsPage({ onOpenTenant }: TenantsPageProps) {
  const { data, isLoading, error } = useListTenantsQuery();
  const tenants = data?.items ?? [];

  return (
    <>
      <Typography variant="h5" component="h2" gutterBottom>
        Tenants
      </Typography>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load tenants.' : undefined}
        isEmpty={tenants.length === 0}
        emptyMessage="No tenants exist yet."
      >
        <TenantTable tenants={tenants} onSelect={onOpenTenant} />
      </DataStateBoundary>
    </>
  );
}

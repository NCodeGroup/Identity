import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import type { TenantRow } from './TenantTable.js';

/** The filter value that shows every tenant. An environment type is never empty, so it cannot collide. */
export const allTenantEnvironments = '';

/** The distinct environment types of the supplied tenants, sorted for display. */
export function tenantEnvironmentTypes(tenants: readonly TenantRow[]): string[] {
  const types = new Set<string>();
  for (const tenant of tenants) {
    if (tenant.environment) {
      types.add(tenant.environment.type);
    }
  }
  return [...types].sort((a, b) => a.localeCompare(b));
}

/** Whether a tenant is shown under the environment type filter (`allTenantEnvironments` shows every tenant). */
export function matchesTenantEnvironment(tenant: TenantRow, environmentType: string): boolean {
  return environmentType === allTenantEnvironments || tenant.environment?.type === environmentType;
}

export type TenantEnvironmentFilterProps = {
  /** The environment types to offer, typically {@link tenantEnvironmentTypes} of the listed tenants. */
  readonly types: readonly string[];
  /** The selected type, or {@link allTenantEnvironments}. */
  readonly value: string;
  readonly onChange: (environmentType: string) => void;
};

/**
 * Stateless environment filter for the tenant list. The vocabulary of environment types is open, so it offers the
 * types the listed tenants have rather than a fixed set.
 */
export function TenantEnvironmentFilter({ types, value, onChange }: TenantEnvironmentFilterProps) {
  const selected = types.includes(value) ? value : allTenantEnvironments;
  return (
    <TextField
      select
      size="small"
      label="Environment"
      value={selected}
      onChange={(event) => onChange(event.target.value)}
      sx={{ minWidth: 200 }}
      slotProps={{ htmlInput: { 'data-testid': 'tenant-environment-filter' } }}
    >
      <MenuItem value={allTenantEnvironments}>All environments</MenuItem>
      {types.map((type) => (
        <MenuItem key={type} value={type}>
          {type}
        </MenuItem>
      ))}
    </TextField>
  );
}

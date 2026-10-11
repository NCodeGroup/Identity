import Chip from '@mui/material/Chip';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';

/** The minimal tenant shape this presentational table needs (decoupled from the API contract). */
export type TenantRow = {
  readonly tenantId: string;
  readonly displayName: string;
  readonly domainName: string | null;
  readonly isDisabled: boolean;
};

export type TenantTableProps = {
  readonly tenants: readonly TenantRow[];
  readonly onSelect?: (tenantId: string) => void;
};

/**
 * Stateless tenant table. Receives rows and a selection callback as props; holds no state and does
 * no fetching, so it renders directly in Storybook and under RTL.
 */
export function TenantTable({ tenants, onSelect }: TenantTableProps) {
  return (
    <Table size="small" aria-label="Tenants">
      <TableHead>
        <TableRow>
          <TableCell>Display name</TableCell>
          <TableCell>Tenant ID</TableCell>
          <TableCell>Domain</TableCell>
          <TableCell>Status</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {tenants.map((tenant) => (
          <TableRow
            key={tenant.tenantId}
            hover={Boolean(onSelect)}
            {...(onSelect
              ? { onClick: () => onSelect(tenant.tenantId), sx: { cursor: 'pointer' } }
              : {})}
            data-testid={`tenant-row-${tenant.tenantId}`}
          >
            <TableCell>{tenant.displayName}</TableCell>
            <TableCell>{tenant.tenantId}</TableCell>
            <TableCell>{tenant.domainName ?? '—'}</TableCell>
            <TableCell>
              <Chip
                size="small"
                color={tenant.isDisabled ? 'default' : 'success'}
                label={tenant.isDisabled ? 'Disabled' : 'Active'}
              />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

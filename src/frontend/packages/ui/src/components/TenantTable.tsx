import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';

/** The minimal tenant shape this presentational table needs (decoupled from the API contract). */
export type TenantRow = {
  readonly tenantId: string;
  readonly displayName: string;
  readonly issuer: string | null;
  readonly isDisabled: boolean;
};

export type TenantTableProps = {
  readonly tenants: readonly TenantRow[];
  readonly onSelect?: (tenantId: string) => void;
  readonly onEdit?: (tenantId: string) => void;
  readonly onDelete?: (tenantId: string) => void;
};

/**
 * Stateless tenant table. Receives rows and callbacks as props; holds no state and does no fetching,
 * so it renders directly in Storybook and under RTL. The row-level edit/delete controls appear only
 * when their callbacks are supplied, and stop propagation so they never trigger row selection.
 */
export function TenantTable({ tenants, onSelect, onEdit, onDelete }: TenantTableProps) {
  const showActions = Boolean(onEdit || onDelete);
  return (
    <Table size="small" aria-label="Tenants">
      <TableHead>
        <TableRow>
          <TableCell>Display name</TableCell>
          <TableCell>Tenant ID</TableCell>
          <TableCell>Issuer</TableCell>
          <TableCell>Status</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
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
            <TableCell>{tenant.issuer ?? '—'}</TableCell>
            <TableCell>
              <Chip
                size="small"
                color={tenant.isDisabled ? 'default' : 'success'}
                label={tenant.isDisabled ? 'Disabled' : 'Active'}
              />
            </TableCell>
            {showActions && (
              <TableCell align="right">
                <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                  {onEdit && (
                    <Tooltip title="Edit tenant">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${tenant.displayName}`}
                        data-testid={`tenant-edit-${tenant.tenantId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onEdit(tenant.tenantId);
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && (
                    <Tooltip title="Delete tenant">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${tenant.displayName}`}
                        data-testid={`tenant-delete-${tenant.tenantId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onDelete(tenant.tenantId);
                        }}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </Stack>
              </TableCell>
            )}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

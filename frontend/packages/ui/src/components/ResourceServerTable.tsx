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

/** The minimal resource-server shape this presentational table needs (decoupled from the API contract). */
export type ResourceServerRow = {
  readonly resourceServerId: string;
  readonly name: string;
  readonly identifier: string;
  readonly isDisabled: boolean;
  readonly isSystem: boolean;
  readonly scopeCount: number;
};

export type ResourceServerTableProps = {
  readonly resourceServers: readonly ResourceServerRow[];
  readonly onSelect?: (resourceServerId: string) => void;
  readonly onEdit?: (resourceServerId: string) => void;
  readonly onDelete?: (resourceServerId: string) => void;
};

/**
 * Stateless resource-server (API) table. Receives rows and callbacks as props; holds no state and
 * does no fetching, so it renders directly in Storybook and under RTL. Selecting a row drills into
 * that resource server's scopes. The row-level edit/delete controls appear only when their callbacks
 * are supplied (and stop propagation so they never trigger selection); a system-owned resource server
 * exposes no delete control because it cannot be removed.
 */
export function ResourceServerTable({
  resourceServers,
  onSelect,
  onEdit,
  onDelete,
}: ResourceServerTableProps) {
  const showActions = Boolean(onEdit || onDelete);
  return (
    <Table size="small" aria-label="Resource servers">
      <TableHead>
        <TableRow>
          <TableCell>Name</TableCell>
          <TableCell>Identifier</TableCell>
          <TableCell align="right">Scopes</TableCell>
          <TableCell>Status</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {resourceServers.map((resourceServer) => (
          <TableRow
            key={resourceServer.resourceServerId}
            hover={Boolean(onSelect)}
            {...(onSelect
              ? {
                  onClick: () => onSelect(resourceServer.resourceServerId),
                  sx: { cursor: 'pointer' },
                }
              : {})}
            data-testid={`resource-server-row-${resourceServer.resourceServerId}`}
          >
            <TableCell>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <span>{resourceServer.name}</span>
                {resourceServer.isSystem && <Chip size="small" variant="outlined" label="System" />}
              </Stack>
            </TableCell>
            <TableCell>{resourceServer.identifier}</TableCell>
            <TableCell align="right">{resourceServer.scopeCount}</TableCell>
            <TableCell>
              <Chip
                size="small"
                color={resourceServer.isDisabled ? 'default' : 'success'}
                label={resourceServer.isDisabled ? 'Disabled' : 'Active'}
              />
            </TableCell>
            {showActions && (
              <TableCell align="right">
                <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                  {onEdit && (
                    <Tooltip title="Edit resource server">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${resourceServer.name}`}
                        data-testid={`resource-server-edit-${resourceServer.resourceServerId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onEdit(resourceServer.resourceServerId);
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && !resourceServer.isSystem && (
                    <Tooltip title="Delete resource server">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${resourceServer.name}`}
                        data-testid={`resource-server-delete-${resourceServer.resourceServerId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onDelete(resourceServer.resourceServerId);
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

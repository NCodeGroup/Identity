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

/** The minimal client shape this presentational table needs (decoupled from the API contract). */
export type ClientRow = {
  readonly clientId: string;
  readonly isDisabled: boolean;
};

export type ClientTableProps = {
  readonly clients: readonly ClientRow[];
  readonly onSelect?: (clientId: string) => void;
  readonly onEdit?: (clientId: string) => void;
  readonly onDelete?: (clientId: string) => void;
};

/**
 * Stateless client table. A client has no display name, so it is identified by its id. Receives rows
 * and callbacks as props; holds no state and does no fetching, so it renders directly in Storybook
 * and under RTL. Selecting a row drills into that client's secrets and settings. The edit/delete
 * controls appear only when their callbacks are supplied (and stop propagation so they never trigger
 * selection).
 */
export function ClientTable({ clients, onSelect, onEdit, onDelete }: ClientTableProps) {
  const showActions = Boolean(onEdit || onDelete);
  return (
    <Table size="small" aria-label="Clients">
      <TableHead>
        <TableRow>
          <TableCell>Client ID</TableCell>
          <TableCell>Status</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {clients.map((client) => (
          <TableRow
            key={client.clientId}
            hover={Boolean(onSelect)}
            {...(onSelect
              ? { onClick: () => onSelect(client.clientId), sx: { cursor: 'pointer' } }
              : {})}
            data-testid={`client-row-${client.clientId}`}
          >
            <TableCell>{client.clientId}</TableCell>
            <TableCell>
              <Chip
                size="small"
                color={client.isDisabled ? 'default' : 'success'}
                label={client.isDisabled ? 'Disabled' : 'Active'}
              />
            </TableCell>
            {showActions && (
              <TableCell align="right">
                <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                  {onEdit && (
                    <Tooltip title="Edit client">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${client.clientId}`}
                        data-testid={`client-edit-${client.clientId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onEdit(client.clientId);
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && (
                    <Tooltip title="Delete client">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${client.clientId}`}
                        data-testid={`client-delete-${client.clientId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onDelete(client.clientId);
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

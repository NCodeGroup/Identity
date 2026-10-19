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

/** The minimal server shape this presentational table needs (decoupled from the API contract). */
export type ServerRow = {
  readonly serverId: string;
  readonly displayName: string;
};

export type ServerTableProps = {
  readonly servers: readonly ServerRow[];
  readonly onSelect?: (serverId: string) => void;
  readonly onEdit?: (serverId: string) => void;
  readonly onDelete?: (serverId: string) => void;
};

/**
 * Stateless server table. Receives rows and callbacks as props; holds no state and does no fetching,
 * so it renders directly in Storybook and under RTL. Selecting a row drills into the server's secrets
 * and settings; the row-level edit/delete controls appear only when their callbacks are supplied, and
 * stop propagation so they never trigger row selection.
 */
export function ServerTable({ servers, onSelect, onEdit, onDelete }: ServerTableProps) {
  const showActions = Boolean(onEdit || onDelete);
  return (
    <Table size="small" aria-label="Servers">
      <TableHead>
        <TableRow>
          <TableCell>Display name</TableCell>
          <TableCell>Server ID</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {servers.map((server) => (
          <TableRow
            key={server.serverId}
            hover={Boolean(onSelect)}
            {...(onSelect
              ? { onClick: () => onSelect(server.serverId), sx: { cursor: 'pointer' } }
              : {})}
            data-testid={`server-row-${server.serverId}`}
          >
            <TableCell>{server.displayName}</TableCell>
            <TableCell>{server.serverId}</TableCell>
            {showActions && (
              <TableCell align="right">
                <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                  {onEdit && (
                    <Tooltip title="Edit server">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${server.displayName}`}
                        data-testid={`server-edit-${server.serverId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onEdit(server.serverId);
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && (
                    <Tooltip title="Delete server">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${server.displayName}`}
                        data-testid={`server-delete-${server.serverId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onDelete(server.serverId);
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

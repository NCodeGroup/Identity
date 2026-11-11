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

/** The minimal scope shape this presentational table needs (decoupled from the API contract). */
export type ScopeRow = {
  readonly value: string;
  readonly description: string | null;
  readonly isSystem: boolean;
};

export type ScopeTableProps = {
  readonly scopes: readonly ScopeRow[];
  readonly onEdit?: (scopeValue: string) => void;
  readonly onDelete?: (scopeValue: string) => void;
};

/**
 * Stateless scope (permission) table owned by a resource server. Receives rows and callbacks as
 * props; holds no state and does no fetching, so it renders directly in Storybook and under RTL. The
 * edit/delete controls appear only when their callbacks are supplied; a system-owned scope exposes no
 * delete control because it cannot be removed.
 */
export function ScopeTable({ scopes, onEdit, onDelete }: ScopeTableProps) {
  const showActions = Boolean(onEdit || onDelete);
  return (
    <Table size="small" aria-label="Scopes">
      <TableHead>
        <TableRow>
          <TableCell>Value</TableCell>
          <TableCell>Description</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {scopes.map((scope) => (
          <TableRow key={scope.value} data-testid={`scope-row-${scope.value}`}>
            <TableCell>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <span>{scope.value}</span>
                {scope.isSystem && <Chip size="small" variant="outlined" label="System" />}
              </Stack>
            </TableCell>
            <TableCell>{scope.description ?? '—'}</TableCell>
            {showActions && (
              <TableCell align="right">
                <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                  {onEdit && (
                    <Tooltip title="Edit scope">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${scope.value}`}
                        data-testid={`scope-edit-${scope.value}`}
                        onClick={() => onEdit(scope.value)}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && !scope.isSystem && (
                    <Tooltip title="Delete scope">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${scope.value}`}
                        data-testid={`scope-delete-${scope.value}`}
                        onClick={() => onDelete(scope.value)}
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

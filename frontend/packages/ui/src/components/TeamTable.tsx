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

/** The minimal team shape this presentational table needs (decoupled from the API contract). */
export type TeamRow = {
  readonly teamId: string;
  readonly displayName: string;
  readonly isDisabled: boolean;
  readonly parentTeamId?: string | null;
};

export type TeamTableProps = {
  readonly teams: readonly TeamRow[];
  readonly showParent?: boolean;
  readonly onSelect?: (teamId: string) => void;
  readonly onEdit?: (teamId: string) => void;
  readonly onDelete?: (teamId: string) => void;
};

/**
 * Stateless team table. Receives rows and callbacks as props; holds no state and does no fetching,
 * so it renders directly in Storybook and under RTL. Selecting a row drills into the team; the
 * row-level edit/delete controls appear only when their callbacks are supplied, and stop
 * propagation so they never trigger row selection.
 */
export function TeamTable({ teams, showParent, onSelect, onEdit, onDelete }: TeamTableProps) {
  const showActions = Boolean(onEdit || onDelete);
  return (
    <Table size="small" aria-label="Teams">
      <TableHead>
        <TableRow>
          <TableCell>Display name</TableCell>
          <TableCell>Team ID</TableCell>
          {showParent && <TableCell>Parent team</TableCell>}
          <TableCell>Status</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {teams.map((team) => (
          <TableRow
            key={team.teamId}
            hover={Boolean(onSelect)}
            {...(onSelect
              ? { onClick: () => onSelect(team.teamId), sx: { cursor: 'pointer' } }
              : {})}
            data-testid={`team-row-${team.teamId}`}
          >
            <TableCell>{team.displayName}</TableCell>
            <TableCell>{team.teamId}</TableCell>
            {showParent && (
              <TableCell data-testid={`team-parent-${team.teamId}`}>
                {team.parentTeamId ?? '—'}
              </TableCell>
            )}
            <TableCell>
              <Chip
                size="small"
                color={team.isDisabled ? 'default' : 'success'}
                label={team.isDisabled ? 'Disabled' : 'Active'}
              />
            </TableCell>
            {showActions && (
              <TableCell align="right">
                <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                  {onEdit && (
                    <Tooltip title="Edit team">
                      <IconButton
                        size="small"
                        aria-label={`Edit ${team.displayName}`}
                        data-testid={`team-edit-${team.teamId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onEdit(team.teamId);
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && (
                    <Tooltip title="Delete team">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${team.displayName}`}
                        data-testid={`team-delete-${team.teamId}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          onDelete(team.teamId);
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

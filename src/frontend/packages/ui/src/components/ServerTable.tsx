import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';

/** The minimal server shape this presentational table needs (decoupled from the API contract). */
export type ServerRow = {
  readonly serverId: string;
  readonly displayName: string;
};

export type ServerTableProps = {
  readonly servers: readonly ServerRow[];
  readonly onSelect?: (serverId: string) => void;
};

/**
 * Stateless server table. Receives rows and a select callback as props; holds no state and does no
 * fetching, so it renders directly in Storybook and under RTL. Selecting a row drills into the server's
 * secrets and settings — servers have no create/edit/delete here, so there are no row-level actions.
 */
export function ServerTable({ servers, onSelect }: ServerTableProps) {
  return (
    <Table size="small" aria-label="Servers">
      <TableHead>
        <TableRow>
          <TableCell>Display name</TableCell>
          <TableCell>Server ID</TableCell>
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
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

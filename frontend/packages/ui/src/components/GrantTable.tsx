import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import BlockIcon from '@mui/icons-material/Block';

/** The minimal grant shape this presentational table needs (decoupled from the API contract). */
export type GrantRow = {
  readonly grantId: string;
  readonly grantType: string;
  readonly clientId: string | null;
  readonly subjectId: string | null;
  readonly createdWhen: string;
  readonly expiresWhen: string | null;
  readonly revokedWhen: string | null;
  readonly consumedWhen: string | null;
};

export type GrantTableProps = {
  readonly grants: readonly GrantRow[];
  /**
   * The instant (epoch milliseconds) a grant's expiry is judged against. Supplied by the caller rather than read from
   * the clock, so the table stays a pure function of its props.
   */
  readonly now: number;
  readonly onRevoke?: (grantId: string) => void;
};

type GrantStatus = { readonly label: string; readonly color: 'success' | 'default' | 'warning' };

/** Derives the display status of a grant from its lifecycle timestamps (revoked wins over expired/active). */
function grantStatus(grant: GrantRow, now: number): GrantStatus {
  if (grant.revokedWhen) {
    return { label: 'Revoked', color: 'default' };
  }
  if (grant.expiresWhen && Date.parse(grant.expiresWhen) <= now) {
    return { label: 'Expired', color: 'warning' };
  }
  return { label: 'Active', color: 'success' };
}

/** Formats an ISO-8601 instant for display, falling back to the raw value if it is unparseable. */
function formatInstant(value: string): string {
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? value : new Date(parsed).toLocaleString();
}

/**
 * Stateless grants table. Grants are read-only administratively; the only action is revocation of an
 * active grant (revoked/expired rows expose no control). Receives rows and a callback as props; holds
 * no state and does no fetching, so it renders directly in Storybook and under RTL.
 */
export function GrantTable({ grants, now, onRevoke }: GrantTableProps) {
  const showActions = Boolean(onRevoke);
  return (
    <Table size="small" aria-label="Grants">
      <TableHead>
        <TableRow>
          <TableCell>Type</TableCell>
          <TableCell>Client</TableCell>
          <TableCell>Subject</TableCell>
          <TableCell>Created</TableCell>
          <TableCell>Status</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {grants.map((grant) => {
          const status = grantStatus(grant, now);
          const revocable = showActions && status.label === 'Active';
          return (
            <TableRow key={grant.grantId} data-testid={`grant-row-${grant.grantId}`}>
              <TableCell>{grant.grantType}</TableCell>
              <TableCell>{grant.clientId ?? '—'}</TableCell>
              <TableCell>{grant.subjectId ?? '—'}</TableCell>
              <TableCell>{formatInstant(grant.createdWhen)}</TableCell>
              <TableCell>
                <Chip size="small" color={status.color} label={status.label} />
              </TableCell>
              {showActions && (
                <TableCell align="right">
                  <Stack direction="row" spacing={0.5} sx={{ justifyContent: 'flex-end' }}>
                    {revocable && onRevoke && (
                      <Tooltip title="Revoke grant">
                        <IconButton
                          size="small"
                          aria-label={`Revoke ${grant.grantId}`}
                          data-testid={`grant-revoke-${grant.grantId}`}
                          onClick={() => onRevoke(grant.grantId)}
                        >
                          <BlockIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </Stack>
                </TableCell>
              )}
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

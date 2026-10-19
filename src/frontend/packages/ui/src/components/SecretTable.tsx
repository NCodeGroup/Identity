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
import VpnKeyIcon from '@mui/icons-material/VpnKey';

/** The minimal secret shape this presentational table needs (decoupled from the API contract). */
export type SecretRow = {
  readonly secretId: string;
  readonly secretType: string;
  readonly keySizeBits: number;
  readonly use: string | null;
  readonly algorithm: string | null;
  readonly createdWhen: string;
  readonly expiresWhen: string;
};

export type SecretTableProps = {
  readonly secrets: readonly SecretRow[];
  /**
   * The instant (epoch milliseconds) a secret's expiry is judged against. Supplied by the caller rather than read from
   * the clock, so the table stays a pure function of its props.
   */
  readonly now: number;
  readonly onDelete?: (secretId: string) => void;
  /** Exports the public key of a secret; shown only for secret types in {@link exportableSecretTypes}. */
  readonly onExportPublicKey?: (secretId: string) => void;
  /**
   * The secret types that expose an exportable public key, discovered from the server capabilities. The export
   * action is shown for a row only when its {@link SecretRow.secretType} is included here.
   */
  readonly exportableSecretTypes?: readonly string[];
};

/** Formats an ISO-8601 instant for display, falling back to the raw value if it is unparseable. */
function formatInstant(value: string): string {
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? value : new Date(parsed).toLocaleString();
}

/**
 * Stateless secrets table. Secret key material is never shown here (it is revealed once at creation,
 * ADR-0050); this lists only metadata. An expired secret is flagged. Receives rows and a callback as
 * props; holds no state and does no fetching, so it renders directly in Storybook and under RTL.
 */
export function SecretTable({
  secrets,
  now,
  onDelete,
  onExportPublicKey,
  exportableSecretTypes,
}: SecretTableProps) {
  const exportable = new Set(exportableSecretTypes ?? []);
  const showActions = Boolean(onDelete) || Boolean(onExportPublicKey);
  return (
    <Table size="small" aria-label="Secrets">
      <TableHead>
        <TableRow>
          <TableCell>Secret ID</TableCell>
          <TableCell>Type</TableCell>
          <TableCell>Key size</TableCell>
          <TableCell>Expires</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {secrets.map((secret) => {
          const expired = Date.parse(secret.expiresWhen) <= now;
          return (
            <TableRow key={secret.secretId} data-testid={`secret-row-${secret.secretId}`}>
              <TableCell>{secret.secretId}</TableCell>
              <TableCell>{secret.secretType}</TableCell>
              <TableCell>{secret.keySizeBits}</TableCell>
              <TableCell>
                <Stack direction="row" spacing={1} alignItems="center">
                  <span>{formatInstant(secret.expiresWhen)}</span>
                  {expired && <Chip size="small" color="warning" label="Expired" />}
                </Stack>
              </TableCell>
              {showActions && (
                <TableCell align="right">
                  {onExportPublicKey && exportable.has(secret.secretType) && (
                    <Tooltip title="Export public key">
                      <IconButton
                        size="small"
                        aria-label={`Export public key for ${secret.secretId}`}
                        data-testid={`secret-export-${secret.secretId}`}
                        onClick={() => onExportPublicKey(secret.secretId)}
                      >
                        <VpnKeyIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  {onDelete && (
                    <Tooltip title="Delete secret">
                      <IconButton
                        size="small"
                        aria-label={`Delete ${secret.secretId}`}
                        data-testid={`secret-delete-${secret.secretId}`}
                        onClick={() => onDelete(secret.secretId)}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              )}
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

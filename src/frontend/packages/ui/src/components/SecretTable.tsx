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

/** Secret types that carry an exportable public key (asymmetric); symmetric secrets have none. */
const asymmetricSecretTypes = new Set(['rsa', 'ecc', 'certificate']);

/** Whether the given secret type is asymmetric and therefore exposes a public key. */
export function isAsymmetricSecretType(secretType: string): boolean {
  return asymmetricSecretTypes.has(secretType.toLowerCase());
}

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
  readonly onDelete?: (secretId: string) => void;
  /** Exports the public key of an asymmetric secret; shown only for asymmetric secret types. */
  readonly onExportPublicKey?: (secretId: string) => void;
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
export function SecretTable({ secrets, onDelete, onExportPublicKey }: SecretTableProps) {
  const now = Date.now();
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
                  {onExportPublicKey && isAsymmetricSecretType(secret.secretType) && (
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

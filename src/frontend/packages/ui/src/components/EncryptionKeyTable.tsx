import Chip from '@mui/material/Chip';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';

/** The minimal master key shape this presentational table needs (decoupled from the API contract). */
export type EncryptionKeyRow = {
  readonly keyId: string;
  readonly version: number;
  /** `active` for the key that protects new values; any other state reads as retired. */
  readonly state: string;
  readonly createdWhen: string;
  readonly retiredWhen: string | null;
  readonly rootKeyProvider: string;
  /** The recovery key that also wraps the key, or `null` when no recovery copy exists. */
  readonly recoveryKeyId: string | null;
};

export type EncryptionKeyTableProps = {
  readonly keys: readonly EncryptionKeyRow[];
};

/**
 * Stateless table of an owner's master keys (ADR-1407): version, state, when each was created and retired, the root key
 * provider that wraps it, and whether a recovery copy exists. It shows no key material, because the API returns none.
 * Holds no state and does no fetching.
 */
export function EncryptionKeyTable({ keys }: EncryptionKeyTableProps) {
  return (
    <Table size="small" aria-label="Encryption keys">
      <TableHead>
        <TableRow>
          <TableCell>Version</TableCell>
          <TableCell>State</TableCell>
          <TableCell>Created</TableCell>
          <TableCell>Retired</TableCell>
          <TableCell>Root key provider</TableCell>
          <TableCell>Recovery copy</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {keys.map((key) => {
          const isActive = key.state === 'active';
          return (
            <TableRow key={key.keyId} data-testid={`encryption-key-row-${key.keyId}`}>
              <TableCell>{key.version}</TableCell>
              <TableCell>
                <Chip
                  size="small"
                  label={isActive ? 'active' : 'retired'}
                  color={isActive ? 'success' : 'default'}
                  data-testid={`encryption-key-state-${key.keyId}`}
                />
              </TableCell>
              <TableCell>{new Date(key.createdWhen).toLocaleString()}</TableCell>
              <TableCell>
                {key.retiredWhen ? new Date(key.retiredWhen).toLocaleString() : '—'}
              </TableCell>
              <TableCell>{key.rootKeyProvider}</TableCell>
              <TableCell>{key.recoveryKeyId ? 'Yes' : 'No'}</TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

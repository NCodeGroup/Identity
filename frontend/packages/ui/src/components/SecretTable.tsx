import Chip from '@mui/material/Chip';
import AutorenewIcon from '@mui/icons-material/Autorenew';
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
  /** The key identifier (`kid`) the key is selected by; equal to {@link secretId} when the key has no own `kid`. */
  readonly keyId: string;
  readonly secretType: string;
  readonly keySizeBits: number;
  /**
   * Whether the server holds only a one-way verifier of the shared secret, which authenticates a method that compares
   * the secret and never one that uses it as a key, such as `client_secret_jwt`.
   */
  readonly isVerifier: boolean;
  readonly use: string | null;
  readonly algorithm: string | null;
  readonly createdWhen: string;
  /** When the secret starts to sign or encrypt; a key published ahead of its use carries a future instant. */
  readonly activatesWhen: string;
  /** When the secret stops signing or encrypting while it still verifies or decrypts until {@link expiresWhen}. */
  readonly retiresWhen: string;
  readonly expiresWhen: string;
  /**
   * The component that manages the secret's lifecycle on its own, such as the tenant key rollover for a key the
   * server provisioned, or `null` when the secret keeps the lifecycle its creator gave it.
   */
  readonly managedBy: string | null;
  /**
   * The remote key provider whose key management service holds the key's private half, such as `aws-kms`, or `null`
   * for a key the server holds.
   */
  readonly remoteKeyProvider: string | null;
};

/** Where a secret stands at an instant, derived from its four instants (`SecretLifecycleStates`). */
export type SecretLifecycleState = 'upcoming' | 'active' | 'retired' | 'expired';

/** Names the state a secret is in at `now` (epoch milliseconds), as the server judges it from the same instants. */
export function getSecretLifecycleState(secret: SecretRow, now: number): SecretLifecycleState {
  if (Date.parse(secret.expiresWhen) <= now) {
    return 'expired';
  }
  if (Date.parse(secret.retiresWhen) <= now) {
    return 'retired';
  }
  if (Date.parse(secret.activatesWhen) > now) {
    return 'upcoming';
  }
  return 'active';
}

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
  /** Rotates a secret in one call; shown only for secret types in {@link rotatableSecretTypes}. */
  readonly onRotate?: (secretId: string) => void;
  /**
   * The secret types that can be rotated, discovered from the server capabilities. The rotate action is shown for a
   * row only when its {@link SecretRow.secretType} is included here.
   */
  readonly rotatableSecretTypes?: readonly string[];
};

/** Formats an ISO-8601 instant for display, falling back to the raw value if it is unparseable. */
function formatInstant(value: string): string {
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? value : new Date(parsed).toLocaleString();
}

const stateLabels: Record<Exclude<SecretLifecycleState, 'active'>, string> = {
  upcoming: 'Upcoming',
  retired: 'Retired',
  expired: 'Expired',
};

// See ADR-1050 (secret material is revealed once), ADR-1391 (a compared secret is stored as a verifier), ADR-1403 (a
// secret's active window lies inside its validity window) and ADR-1404 (the server manages its provisioned keys).
/**
 * Stateless secrets table. Secret key material is never shown here (it is revealed once at creation); this lists only
 * metadata: when each secret activates, retires and expires, and where it stands at `now`. A secret that is not yet
 * active, retired or expired is flagged, and so are a shared secret held only as a one-way verifier, which a method
 * that uses the secret as a key cannot use, and a key whose lifecycle the server manages; a key held in a key
 * management service carries a chip naming its remote key provider. Receives rows and a callback as props; holds no
 * state and does no fetching, so it renders directly in Storybook and under RTL.
 */
export function SecretTable({
  secrets,
  now,
  onDelete,
  onExportPublicKey,
  exportableSecretTypes,
  onRotate,
  rotatableSecretTypes,
}: SecretTableProps) {
  const exportable = new Set(exportableSecretTypes ?? []);
  const rotatable = new Set(rotatableSecretTypes ?? []);
  const showActions = Boolean(onDelete) || Boolean(onExportPublicKey) || Boolean(onRotate);
  return (
    <Table size="small" aria-label="Secrets">
      <TableHead>
        <TableRow>
          <TableCell>Secret ID</TableCell>
          <TableCell>Key ID</TableCell>
          <TableCell>Type</TableCell>
          <TableCell>Key size</TableCell>
          <TableCell>Activates</TableCell>
          <TableCell>Retires</TableCell>
          <TableCell>Expires</TableCell>
          {showActions && <TableCell align="right">Actions</TableCell>}
        </TableRow>
      </TableHead>
      <TableBody>
        {secrets.map((secret) => {
          const state = getSecretLifecycleState(secret, now);
          return (
            <TableRow key={secret.secretId} data-testid={`secret-row-${secret.secretId}`}>
              <TableCell>{secret.secretId}</TableCell>
              <TableCell data-testid={`secret-kid-${secret.secretId}`}>{secret.keyId}</TableCell>
              <TableCell>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <span>{secret.secretType}</span>
                  {secret.isVerifier && (
                    <Tooltip title="Stored as a one-way verifier: it authenticates a method that compares the secret, never one that uses it as a key, such as client_secret_jwt">
                      <Chip
                        size="small"
                        label="Verifier"
                        data-testid={`secret-verifier-${secret.secretId}`}
                      />
                    </Tooltip>
                  )}
                  {secret.managedBy !== null && (
                    <Tooltip title="The server manages this key's lifecycle: it publishes the successor ahead of the retirement, retires the key when its algorithm is no longer used and removes it once it has expired">
                      <Chip
                        size="small"
                        label="Managed"
                        data-testid={`secret-managed-${secret.secretId}`}
                      />
                    </Tooltip>
                  )}
                  {secret.remoteKeyProvider !== null && (
                    <Tooltip title="Held in a key management service: the private key never leaves it, and the service signs with it">
                      <Chip
                        size="small"
                        label={secret.remoteKeyProvider}
                        data-testid={`secret-remote-${secret.secretId}`}
                      />
                    </Tooltip>
                  )}
                </Stack>
              </TableCell>
              <TableCell>{secret.keySizeBits}</TableCell>
              <TableCell>{formatInstant(secret.activatesWhen)}</TableCell>
              <TableCell>{formatInstant(secret.retiresWhen)}</TableCell>
              <TableCell>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <span>{formatInstant(secret.expiresWhen)}</span>
                  {state !== 'active' && (
                    <Chip
                      size="small"
                      color={state === 'expired' ? 'warning' : 'default'}
                      label={stateLabels[state]}
                      data-testid={`secret-state-${secret.secretId}`}
                    />
                  )}
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
                  {onRotate && rotatable.has(secret.secretType) && (
                    <Tooltip title="Rotate secret">
                      <IconButton
                        size="small"
                        aria-label={`Rotate ${secret.secretId}`}
                        data-testid={`secret-rotate-${secret.secretId}`}
                        onClick={() => onRotate(secret.secretId)}
                      >
                        <AutorenewIcon fontSize="small" />
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

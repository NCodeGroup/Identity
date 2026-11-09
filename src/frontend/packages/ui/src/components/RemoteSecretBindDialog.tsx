import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import {
  offeredRemoteKeyProviders,
  remoteKeyAlgorithmLabel,
  remoteKeyAlgorithmValue,
  type RemoteKeyProviderOption,
} from './remoteKeyAlgorithms.js';

/** The bind form's own field shape. `expiresWhen` is a `datetime-local` string (local time). */
export type RemoteSecretBindValues = {
  /** The remote key provider whose key management service holds the key. */
  readonly remoteKeyProvider: string;
  /** The identifier of the key in the key management service. */
  readonly remoteKeyId: string;
  /** The secret type of the key, taken from the chosen algorithm row. */
  readonly secretType: string;
  /** The key size of the key, taken from the chosen algorithm row. */
  readonly keySizeBits: number;
  /** The curve of the key, taken from the chosen algorithm row; `''` for none. */
  readonly curve: string;
  /** The JOSE signature or key-management algorithm of the key, taken from the chosen algorithm row. */
  readonly algorithm: string;
  readonly expiresWhen: string;
};

export type RemoteSecretBindDialogProps = {
  readonly open: boolean;
  readonly initialValues: RemoteSecretBindValues;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  /**
   * The providers offered, with the algorithms each holds keys for, discovered from server capabilities; only the
   * providers that list an algorithm are offered.
   */
  readonly remoteKeyProviders: readonly RemoteKeyProviderOption[];
  readonly onSubmit: (values: RemoteSecretBindValues) => void;
  readonly onCancel: () => void;
};

/** A zeroed starting point for the bind form; containers seed a sensible default expiry. */
export const emptyRemoteSecretBindValues: RemoteSecretBindValues = {
  remoteKeyProvider: '',
  remoteKeyId: '',
  secretType: '',
  keySizeBits: 0,
  curve: '',
  algorithm: '',
  expiresWhen: '',
};

/**
 * Stateless form, rendered in a dialog, that binds a key a key management service already holds. The server reads
 * the key's public half from the key management service and never destroys a bound key, so nothing is revealed. The
 * field state is local because it is intrinsic to the controlled inputs (it reseeds on the rising edge of `open`),
 * so the component still renders in isolation under Storybook/RTL. Persistence and server-error handling live in
 * the container that wires the mutation.
 */
export function RemoteSecretBindDialog({
  open,
  initialValues,
  isSubmitting = false,
  error,
  remoteKeyProviders,
  onSubmit,
  onCancel,
}: RemoteSecretBindDialogProps) {
  const [values, setValues] = useState<RemoteSecretBindValues>(initialValues);
  const wasOpenRef = useRef(open);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues]);

  // The chosen provider is always one the form offers: the seeded provider when it is offered, otherwise the first
  // offered provider, so the select never holds a value it has no option for.
  const providers = offeredRemoteKeyProviders(remoteKeyProviders);
  const provider = providers.find((p) => p.name === values.remoteKeyProvider) ?? providers[0];
  const selectedRow = provider?.algorithms.find(
    (row) =>
      row.algorithm === values.algorithm &&
      row.secretType === values.secretType &&
      row.keySizeBits === values.keySizeBits &&
      (row.curve ?? '') === values.curve,
  );
  const remoteKeyId = values.remoteKeyId.trim();

  const canSubmit =
    provider !== undefined &&
    remoteKeyId.length > 0 &&
    selectedRow !== undefined &&
    values.expiresWhen.trim().length > 0 &&
    !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      remoteKeyProvider: provider.name,
      remoteKeyId,
      secretType: selectedRow.secretType,
      keySizeBits: selectedRow.keySizeBits,
      curve: selectedRow.curve ?? '',
      algorithm: selectedRow.algorithm,
      expiresWhen: values.expiresWhen,
    });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>Bind a key management service key</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="remote-bind-error">
              {error}
            </Alert>
          )}
          <DialogContentText component="div">
            The server reads the key&apos;s public half from the key management service, checks that
            it is a key of the chosen algorithm, and stores it once the key proves it holds the
            private half: a signing key signs a challenge the public half verifies, and an
            encryption key decrypts a challenge encrypted to it. Binding needs update authority on
            the platform, and the server never destroys a bound key.
          </DialogContentText>
          <TextField
            select
            label="Provider"
            value={provider ? provider.name : ''}
            onChange={(event) =>
              setValues((v) => ({
                ...v,
                remoteKeyProvider: event.target.value,
                secretType: '',
                keySizeBits: 0,
                curve: '',
                algorithm: '',
              }))
            }
            required
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'remote-bind-provider' } }}
          >
            {providers.map((candidate) => (
              <MenuItem key={candidate.name} value={candidate.name}>
                {candidate.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Key identifier"
            value={values.remoteKeyId}
            onChange={(event) => setValues((v) => ({ ...v, remoteKeyId: event.target.value }))}
            helperText="An AWS KMS key ARN, an Azure Key Vault versioned key identifier or a Google Cloud KMS crypto key version name."
            required
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'remote-bind-key-id' } }}
          />
          <TextField
            select
            label="Algorithm"
            value={selectedRow ? remoteKeyAlgorithmValue(selectedRow) : ''}
            onChange={(event) => {
              const row = provider?.algorithms.find(
                (candidate) => remoteKeyAlgorithmValue(candidate) === event.target.value,
              );
              if (!row) {
                return;
              }
              setValues((v) => ({
                ...v,
                secretType: row.secretType,
                keySizeBits: row.keySizeBits,
                curve: row.curve ?? '',
                algorithm: row.algorithm,
              }));
            }}
            required
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'remote-bind-algorithm' } }}
          >
            {(provider?.algorithms ?? []).map((row) => (
              <MenuItem key={remoteKeyAlgorithmValue(row)} value={remoteKeyAlgorithmValue(row)}>
                {remoteKeyAlgorithmLabel(row)}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Expires"
            type="datetime-local"
            value={values.expiresWhen}
            onChange={(event) => setValues((v) => ({ ...v, expiresWhen: event.target.value }))}
            required
            fullWidth
            slotProps={{
              inputLabel: { shrink: true },
              htmlInput: { 'data-testid': 'remote-bind-expires' },
            }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="remote-bind-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="remote-bind-submit"
        >
          {isSubmitting ? 'Binding…' : 'Bind'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

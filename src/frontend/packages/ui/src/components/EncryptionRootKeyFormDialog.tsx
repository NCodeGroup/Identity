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

/** The root key form's own field shape. */
export type EncryptionRootKeyFormValues = {
  /**
   * The preferred key wrapper name (e.g. `aws-kms`). The form offers only the providers the tenant may choose, so a
   * provider it does not offer gives way to the first one it does.
   */
  readonly provider: string;
  /** The reference that names the key at its provider, such as a KMS key ARN. */
  readonly keyReference: string;
};

export type EncryptionRootKeyFormDialogProps = {
  readonly open: boolean;
  readonly initialValues: EncryptionRootKeyFormValues;
  /** The key wrapper names the tenant may choose from. None are offered while they load, and the form cannot be submitted. */
  readonly providers: readonly string[];
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: EncryptionRootKeyFormValues) => void;
  readonly onCancel: () => void;
};

/** A zeroed starting point for the root key form. */
export const emptyEncryptionRootKeyFormValues: EncryptionRootKeyFormValues = {
  provider: '',
  keyReference: '',
};

/**
 * Stateless root key form rendered in a dialog (ADR-1414): the provider the tenant chooses from the allowed ones and the
 * key reference. The field state is local because it is intrinsic to the controlled inputs (it reseeds on the rising
 * edge of `open`), so the component still renders in isolation under Storybook/RTL. The server validates the key by
 * wrapping and unwrapping a probe through it, and its error is shown in the dialog; persistence and that error handling
 * live in the container that wires the mutation.
 */
export function EncryptionRootKeyFormDialog({
  open,
  initialValues,
  providers,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: EncryptionRootKeyFormDialogProps) {
  const [values, setValues] = useState<EncryptionRootKeyFormValues>(initialValues);
  const wasOpenRef = useRef(open);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues]);

  // The selected provider is always one the form offers, so the select never holds a value it has no option for.
  const provider = providers.includes(values.provider) ? values.provider : (providers[0] ?? '');
  const keyReference = values.keyReference.trim();
  const canSubmit = provider.length > 0 && keyReference.length > 0 && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({ provider, keyReference });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>Set root key</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="root-key-form-error">
              {error}
            </Alert>
          )}
          <DialogContentText component="div">
            Every encryption key of this tenant is wrapped under this key, with no operator recovery
            copy. Revoking or destroying the key makes the tenant&apos;s data unreadable.
          </DialogContentText>
          <TextField
            select
            label="Provider"
            value={provider}
            onChange={(event) => setValues((v) => ({ ...v, provider: event.target.value }))}
            fullWidth
            error={providers.length === 0}
            helperText={providers.length === 0 ? 'No root key providers are available.' : undefined}
            slotProps={{ htmlInput: { 'data-testid': 'root-key-form-provider' } }}
          >
            {providers.map((name) => (
              <MenuItem key={name} value={name}>
                {name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Key reference"
            value={values.keyReference}
            onChange={(event) => setValues((v) => ({ ...v, keyReference: event.target.value }))}
            placeholder="e.g. arn:aws:kms:us-east-1:111122223333:key/…"
            required
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'root-key-form-reference' } }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="root-key-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="root-key-form-submit"
        >
          {isSubmitting ? 'Setting…' : 'Set root key'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

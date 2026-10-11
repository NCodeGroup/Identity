import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';

/** The mint form's own field shape. `lifetimeSeconds` is text so a blank value means "never expires". */
export type InitialAccessTokenFormValues = {
  readonly description: string;
  readonly lifetimeSeconds: string;
};

export type InitialAccessTokenFormDialogProps = {
  readonly open: boolean;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: InitialAccessTokenFormValues) => void;
  readonly onCancel: () => void;
};

/** A zeroed starting point for the mint form: no description and a non-expiring (blank lifetime) token. */
export const emptyInitialAccessTokenFormValues: InitialAccessTokenFormValues = {
  description: '',
  lifetimeSeconds: '',
};

/** True when the lifetime field is blank (non-expiring) or a positive integer number of seconds. */
function isValidLifetime(lifetimeSeconds: string): boolean {
  const trimmed = lifetimeSeconds.trim();
  if (trimmed.length === 0) {
    return true;
  }
  return /^\d+$/.test(trimmed) && Number(trimmed) > 0;
}

/**
 * Stateless initial-access-token mint form (RFC 7591) rendered in a dialog. The minted token is revealed
 * exactly once in {@link import('./RevealInitialAccessTokenDialog.js').RevealInitialAccessTokenDialog} and
 * never again, so there is no edit path — only create. The field state is local because it is intrinsic to
 * the controlled inputs (it reseeds on the rising edge of `open`), so the component still renders in
 * isolation. Persistence, the one-time reveal, and server-error handling live in the container.
 */
export function InitialAccessTokenFormDialog({
  open,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: InitialAccessTokenFormDialogProps) {
  const [values, setValues] = useState<InitialAccessTokenFormValues>(
    emptyInitialAccessTokenFormValues,
  );
  const wasOpen = useRef(open);

  useEffect(() => {
    if (open && !wasOpen.current) {
      setValues(emptyInitialAccessTokenFormValues);
    }
    wasOpen.current = open;
  }, [open]);

  const lifetimeValid = isValidLifetime(values.lifetimeSeconds);
  const canSubmit = lifetimeValid && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      description: values.description.trim(),
      lifetimeSeconds: values.lifetimeSeconds.trim(),
    });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>Mint initial access token</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="iat-form-error">
              {error}
            </Alert>
          )}
          <DialogContentText component="div">
            An initial access token authorizes its bearer to register one new client at the
            protected dynamic client registration endpoint. The token value is shown only once after
            minting — store it securely, it can never be retrieved again.
          </DialogContentText>
          <TextField
            label="Description (optional)"
            value={values.description}
            onChange={(event) => setValues((v) => ({ ...v, description: event.target.value }))}
            placeholder="e.g. onboarding for Partner X"
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'iat-description' } }}
          />
          <TextField
            label="Lifetime (seconds, optional)"
            type="number"
            value={values.lifetimeSeconds}
            onChange={(event) => setValues((v) => ({ ...v, lifetimeSeconds: event.target.value }))}
            error={!lifetimeValid}
            helperText={
              lifetimeValid
                ? 'Leave blank for a token that never expires.'
                : 'Lifetime must be a positive whole number of seconds.'
            }
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'iat-lifetime', min: 1 } }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="iat-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="iat-form-submit"
        >
          {isSubmitting ? 'Minting…' : 'Mint'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

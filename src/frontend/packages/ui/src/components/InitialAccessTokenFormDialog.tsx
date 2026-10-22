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

/**
 * The mint form's own field shape. `lifetimeSeconds` is text so a blank value means "the 24-hour default";
 * `resourceServerId` is blank when the token is not scoped to a resource server.
 */
export type InitialAccessTokenFormValues = {
  readonly description: string;
  readonly lifetimeSeconds: string;
  readonly resourceServerId: string;
};

/** A resource server the token may be scoped to (decoupled from the API contract). */
export type InitialAccessTokenResourceServerOption = {
  readonly resourceServerId: string;
  readonly name: string;
  readonly identifier: string;
};

export type InitialAccessTokenFormDialogProps = {
  readonly open: boolean;
  /**
   * The resource servers the token may be scoped to (ADR-0192). The picker is offered only when there is at
   * least one; the container passes the tenant's non-system resource servers.
   */
  readonly resourceServers?: readonly InitialAccessTokenResourceServerOption[];
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: InitialAccessTokenFormValues) => void;
  readonly onCancel: () => void;
};

/** A zeroed starting point for the mint form: no description and the default (blank) lifetime. */
export const emptyInitialAccessTokenFormValues: InitialAccessTokenFormValues = {
  description: '',
  lifetimeSeconds: '',
  resourceServerId: '',
};

/** The longest lifetime the server accepts for an initial access token: 30 days, in seconds. */
const MAX_LIFETIME_SECONDS = 2_592_000;

/** True when the lifetime field is blank (the default) or a whole number of seconds from 1 to 30 days. */
function isValidLifetime(lifetimeSeconds: string): boolean {
  const trimmed = lifetimeSeconds.trim();
  if (trimmed.length === 0) {
    return true;
  }
  if (!/^\d+$/.test(trimmed)) {
    return false;
  }
  const seconds = Number(trimmed);
  return seconds >= 1 && seconds <= MAX_LIFETIME_SECONDS;
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
  resourceServers = [],
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: InitialAccessTokenFormDialogProps) {
  const [values, setValues] = useState<InitialAccessTokenFormValues>(
    emptyInitialAccessTokenFormValues,
  );
  const wasOpenRef = useRef(open);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(emptyInitialAccessTokenFormValues);
    }
    wasOpenRef.current = open;
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
      resourceServerId: values.resourceServerId,
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
            minting — store it securely, it can never be retrieved again. It can be used once and
            expires after its lifetime.
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
                ? 'Leave blank for a token that expires in 24 hours.'
                : 'Lifetime must be a whole number of seconds from 1 to 2592000 (30 days).'
            }
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'iat-lifetime', min: 1 } }}
          />
          {resourceServers.length > 0 && (
            <TextField
              select
              label="Resource server (optional)"
              value={values.resourceServerId}
              onChange={(event) =>
                setValues((v) => ({ ...v, resourceServerId: event.target.value }))
              }
              helperText={
                values.resourceServerId
                  ? 'The client it registers is bound to this resource server, must be confidential, and introspects its tokens.'
                  : 'Leave unscoped to register an ordinary client.'
              }
              fullWidth
              slotProps={{ htmlInput: { 'data-testid': 'iat-resource-server' } }}
            >
              <MenuItem value="">None</MenuItem>
              {resourceServers.map((resourceServer) => (
                <MenuItem
                  key={resourceServer.resourceServerId}
                  value={resourceServer.resourceServerId}
                >
                  {resourceServer.name} ({resourceServer.identifier})
                </MenuItem>
              ))}
            </TextField>
          )}
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

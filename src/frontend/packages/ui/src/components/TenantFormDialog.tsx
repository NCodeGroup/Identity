import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';

/** The tenant form's own field shape. `issuer` is a string here (empty means "no issuer"). */
export type TenantFormValues = {
  readonly tenantId: string;
  readonly displayName: string;
  readonly issuer: string;
  readonly isDisabled: boolean;
};

export type TenantFormDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly submitLabel: string;
  readonly initialValues: TenantFormValues;
  /** Whether the tenant ID is fixed, because the tenant already exists and its ID cannot change. */
  readonly isTenantIdLocked?: boolean;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: TenantFormValues) => void;
  readonly onCancel: () => void;
};

export const emptyTenantFormValues: TenantFormValues = {
  tenantId: '',
  displayName: '',
  issuer: '',
  isDisabled: false,
};

/**
 * The tenant ID rule the server enforces, mirrored here only to guide input: 1 to 63 lowercase
 * letters, digits, or hyphens that starts and ends with a letter or digit.
 */
const tenantIdPattern = /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/;

/**
 * Stateless tenant create/edit form rendered in a dialog. The field state is local because it is
 * intrinsic to the controlled inputs (it reseeds from {@link TenantFormDialogProps.initialValues}
 * each time the dialog opens), so the component still renders in isolation under Storybook/RTL. All
 * persistence and server-error handling lives in the container that wires the mutation.
 */
export function TenantFormDialog({
  open,
  title,
  submitLabel,
  initialValues,
  isTenantIdLocked = false,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: TenantFormDialogProps) {
  const [values, setValues] = useState<TenantFormValues>(initialValues);
  const wasOpenRef = useRef(open);

  // Reseed the form only on the rising edge of `open`, so a new `initialValues` identity while the
  // dialog is already open never discards in-progress edits.
  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues]);

  const tenantId = values.tenantId.trim();
  const displayName = values.displayName.trim();
  const isTenantIdValid = isTenantIdLocked || tenantIdPattern.test(tenantId);
  const showTenantIdError = !isTenantIdLocked && tenantId.length > 0 && !isTenantIdValid;
  const canSubmit = isTenantIdValid && displayName.length > 0 && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      tenantId,
      displayName,
      issuer: values.issuer.trim(),
      isDisabled: values.isDisabled,
    });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="tenant-form-error">
              {error}
            </Alert>
          )}
          <TextField
            label="Tenant ID"
            value={values.tenantId}
            onChange={(event) => setValues((v) => ({ ...v, tenantId: event.target.value }))}
            required={!isTenantIdLocked}
            disabled={isTenantIdLocked}
            error={showTenantIdError}
            helperText={
              isTenantIdLocked
                ? 'A tenant ID cannot be changed.'
                : 'Lowercase letters, digits, and hyphens, starting and ending with a letter or digit. It cannot be changed later.'
            }
            placeholder="e.g. acme-dev"
            fullWidth
            autoFocus={!isTenantIdLocked}
            slotProps={{ htmlInput: { 'data-testid': 'tenant-id' } }}
          />
          <TextField
            label="Display name"
            value={values.displayName}
            onChange={(event) => setValues((v) => ({ ...v, displayName: event.target.value }))}
            required
            fullWidth
            autoFocus={isTenantIdLocked}
            slotProps={{ htmlInput: { 'data-testid': 'tenant-display-name' } }}
          />
          <TextField
            label="Issuer"
            value={values.issuer}
            onChange={(event) => setValues((v) => ({ ...v, issuer: event.target.value }))}
            helperText="The https URL the tenant is served under. Leave empty only for local development."
            placeholder="e.g. https://acme.auth.example.com"
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'tenant-issuer' } }}
          />
          <FormControlLabel
            control={
              <Switch
                checked={values.isDisabled}
                onChange={(event) => setValues((v) => ({ ...v, isDisabled: event.target.checked }))}
              />
            }
            label="Disabled"
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="tenant-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="tenant-form-submit"
        >
          {isSubmitting ? 'Saving…' : submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

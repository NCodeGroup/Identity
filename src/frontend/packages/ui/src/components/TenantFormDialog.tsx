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

/** The tenant form's own field shape. `domainName` is a string here (empty means "no domain"). */
export type TenantFormValues = {
  readonly displayName: string;
  readonly domainName: string;
  readonly isDisabled: boolean;
};

export type TenantFormDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly submitLabel: string;
  readonly initialValues: TenantFormValues;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: TenantFormValues) => void;
  readonly onCancel: () => void;
};

export const emptyTenantFormValues: TenantFormValues = {
  displayName: '',
  domainName: '',
  isDisabled: false,
};

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

  const displayName = values.displayName.trim();
  const canSubmit = displayName.length > 0 && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      displayName,
      domainName: values.domainName.trim(),
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
            label="Display name"
            value={values.displayName}
            onChange={(event) => setValues((v) => ({ ...v, displayName: event.target.value }))}
            required
            fullWidth
            autoFocus
            slotProps={{ htmlInput: { 'data-testid': 'tenant-display-name' } }}
          />
          <TextField
            label="Domain name"
            value={values.domainName}
            onChange={(event) => setValues((v) => ({ ...v, domainName: event.target.value }))}
            placeholder="e.g. id.example.com"
            fullWidth
            slotProps={{ htmlInput: { 'data-testid': 'tenant-domain-name' } }}
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

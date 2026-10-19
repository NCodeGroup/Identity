import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';

/** The server form's own field shape. */
export type ServerFormValues = {
  readonly displayName: string;
};

export type ServerFormDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly submitLabel: string;
  readonly initialValues: ServerFormValues;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: ServerFormValues) => void;
  readonly onCancel: () => void;
};

export const emptyServerFormValues: ServerFormValues = {
  displayName: '',
};

/**
 * Stateless server create/edit form rendered in a dialog. The field state is local because it is
 * intrinsic to the controlled inputs (it reseeds from {@link ServerFormDialogProps.initialValues}
 * each time the dialog opens), so the component still renders in isolation under Storybook/RTL. All
 * persistence and server-error handling lives in the container that wires the mutation.
 */
export function ServerFormDialog({
  open,
  title,
  submitLabel,
  initialValues,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: ServerFormDialogProps) {
  const [values, setValues] = useState<ServerFormValues>(initialValues);
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
    onSubmit({ displayName });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="server-form-error">
              {error}
            </Alert>
          )}
          <TextField
            label="Display name"
            value={values.displayName}
            onChange={(event) => setValues({ displayName: event.target.value })}
            required
            fullWidth
            autoFocus
            slotProps={{ htmlInput: { 'data-testid': 'server-display-name' } }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="server-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="server-form-submit"
        >
          {isSubmitting ? 'Saving…' : submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

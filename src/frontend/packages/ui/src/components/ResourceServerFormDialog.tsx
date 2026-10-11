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

/** The resource-server form's own field shape. `identifier` is the audience (immutable after create). */
export type ResourceServerFormValues = {
  readonly identifier: string;
  readonly name: string;
  readonly isDisabled: boolean;
};

export type ResourceServerFormDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly submitLabel: string;
  readonly initialValues: ResourceServerFormValues;
  /** Whether the audience identifier may be edited. False on edit, since the identifier is immutable. */
  readonly identifierEditable?: boolean;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: ResourceServerFormValues) => void;
  readonly onCancel: () => void;
};

export const emptyResourceServerFormValues: ResourceServerFormValues = {
  identifier: '',
  name: '',
  isDisabled: false,
};

/**
 * Stateless resource-server (API) create/edit form rendered in a dialog. The audience identifier is
 * immutable after creation, so it is read-only when {@link ResourceServerFormDialogProps.identifierEditable}
 * is false. The field state is local because it is intrinsic to the controlled inputs (it reseeds on
 * the rising edge of `open`), so the component still renders in isolation under Storybook/RTL. All
 * persistence and server-error handling lives in the container that wires the mutation.
 */
export function ResourceServerFormDialog({
  open,
  title,
  submitLabel,
  initialValues,
  identifierEditable = true,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: ResourceServerFormDialogProps) {
  const [values, setValues] = useState<ResourceServerFormValues>(initialValues);
  const wasOpen = useRef(open);

  useEffect(() => {
    if (open && !wasOpen.current) {
      setValues(initialValues);
    }
    wasOpen.current = open;
  }, [open, initialValues]);

  const identifier = values.identifier.trim();
  const name = values.name.trim();
  const canSubmit =
    name.length > 0 && (!identifierEditable || identifier.length > 0) && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({ identifier, name, isDisabled: values.isDisabled });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="resource-server-form-error">
              {error}
            </Alert>
          )}
          <TextField
            label="Identifier (audience)"
            value={values.identifier}
            onChange={(event) => setValues((v) => ({ ...v, identifier: event.target.value }))}
            placeholder="e.g. https://api.example.com"
            required={identifierEditable}
            disabled={!identifierEditable}
            fullWidth
            {...(identifierEditable ? { autoFocus: true } : {})}
            helperText={
              identifierEditable ? undefined : 'The identifier is immutable after creation.'
            }
            slotProps={{ htmlInput: { 'data-testid': 'resource-server-identifier' } }}
          />
          <TextField
            label="Name"
            value={values.name}
            onChange={(event) => setValues((v) => ({ ...v, name: event.target.value }))}
            required
            fullWidth
            {...(identifierEditable ? {} : { autoFocus: true })}
            slotProps={{ htmlInput: { 'data-testid': 'resource-server-name' } }}
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
        <Button
          onClick={onCancel}
          disabled={isSubmitting}
          data-testid="resource-server-form-cancel"
        >
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="resource-server-form-submit"
        >
          {isSubmitting ? 'Saving…' : submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

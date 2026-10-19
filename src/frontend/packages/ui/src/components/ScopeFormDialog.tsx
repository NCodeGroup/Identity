import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';

/** The scope form's own field shape. `value` is the permission string (immutable after create). */
export type ScopeFormValues = {
  readonly value: string;
  readonly description: string;
};

export type ScopeFormDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly submitLabel: string;
  readonly initialValues: ScopeFormValues;
  /** Whether the scope value may be edited. False on edit, since the scope value is immutable. */
  readonly valueEditable?: boolean;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: ScopeFormValues) => void;
  readonly onCancel: () => void;
};

export const emptyScopeFormValues: ScopeFormValues = { value: '', description: '' };

/**
 * Stateless scope (permission) create/edit form rendered in a dialog. The scope value is immutable
 * after creation, so it is read-only when {@link ScopeFormDialogProps.valueEditable} is false. The
 * field state is local because it is intrinsic to the controlled inputs (it reseeds on the rising
 * edge of `open`), so the component still renders in isolation under Storybook/RTL.
 */
export function ScopeFormDialog({
  open,
  title,
  submitLabel,
  initialValues,
  valueEditable = true,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: ScopeFormDialogProps) {
  const [values, setValues] = useState<ScopeFormValues>(initialValues);
  const wasOpenRef = useRef(open);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues]);

  const value = values.value.trim();
  const canSubmit = (!valueEditable || value.length > 0) && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({ value, description: values.description.trim() });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="scope-form-error">
              {error}
            </Alert>
          )}
          <TextField
            label="Value"
            value={values.value}
            onChange={(event) => setValues((v) => ({ ...v, value: event.target.value }))}
            placeholder="e.g. read:messages"
            required={valueEditable}
            disabled={!valueEditable}
            fullWidth
            {...(valueEditable ? { autoFocus: true } : {})}
            helperText={valueEditable ? undefined : 'The scope value is immutable after creation.'}
            slotProps={{ htmlInput: { 'data-testid': 'scope-value' } }}
          />
          <TextField
            label="Description"
            value={values.description}
            onChange={(event) => setValues((v) => ({ ...v, description: event.target.value }))}
            fullWidth
            {...(valueEditable ? {} : { autoFocus: true })}
            slotProps={{ htmlInput: { 'data-testid': 'scope-description' } }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="scope-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="scope-form-submit"
        >
          {isSubmitting ? 'Saving…' : submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

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
import { isValidTenantId } from './TenantFormDialog.js';

/** The team form's own field shape. */
export type TeamFormValues = {
  readonly teamId: string;
  readonly displayName: string;
  readonly isDisabled: boolean;
};

export type TeamFormDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly submitLabel: string;
  readonly initialValues: TeamFormValues;
  /** Whether the team ID is fixed, because the team already exists and its ID cannot change. */
  readonly isTeamIdLocked?: boolean;
  readonly isSubmitting?: boolean;
  readonly error?: string | undefined;
  readonly onSubmit: (values: TeamFormValues) => void;
  readonly onCancel: () => void;
};

export const emptyTeamFormValues: TeamFormValues = {
  teamId: '',
  displayName: '',
  isDisabled: false,
};

/**
 * Stateless team create/edit form rendered in a dialog. The field state is local because it is
 * intrinsic to the controlled inputs (it reseeds from {@link TeamFormDialogProps.initialValues}
 * each time the dialog opens), so the component still renders in isolation under Storybook/RTL. A
 * team ID takes the tenant ID form. All persistence and server-error handling lives in the
 * container that wires the mutation.
 */
export function TeamFormDialog({
  open,
  title,
  submitLabel,
  initialValues,
  isTeamIdLocked = false,
  isSubmitting = false,
  error,
  onSubmit,
  onCancel,
}: TeamFormDialogProps) {
  const [values, setValues] = useState<TeamFormValues>(initialValues);
  const wasOpenRef = useRef(open);

  // Reseed the form only on the rising edge of `open`, so a new `initialValues` identity while the
  // dialog is already open never discards in-progress edits.
  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setValues(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues]);

  const teamId = values.teamId.trim();
  const displayName = values.displayName.trim();
  const isTeamIdValid = isTeamIdLocked || isValidTenantId(teamId);
  const showTeamIdError = !isTeamIdLocked && teamId.length > 0 && !isTeamIdValid;
  const canSubmit = isTeamIdValid && displayName.length > 0 && !isSubmitting;

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({ teamId, displayName, isDisabled: values.isDisabled });
  };

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" data-testid="team-form-error">
              {error}
            </Alert>
          )}
          <TextField
            label="Team ID"
            value={values.teamId}
            onChange={(event) => setValues((v) => ({ ...v, teamId: event.target.value }))}
            required={!isTeamIdLocked}
            disabled={isTeamIdLocked}
            error={showTeamIdError}
            helperText={
              isTeamIdLocked
                ? 'A team ID cannot be changed.'
                : 'Lowercase letters, digits, and hyphens, starting and ending with a letter or digit. It cannot be changed later.'
            }
            placeholder="e.g. support"
            fullWidth
            autoFocus={!isTeamIdLocked}
            slotProps={{ htmlInput: { 'data-testid': 'team-id' } }}
          />
          <TextField
            label="Display name"
            value={values.displayName}
            onChange={(event) => setValues((v) => ({ ...v, displayName: event.target.value }))}
            required
            fullWidth
            autoFocus={isTeamIdLocked}
            slotProps={{ htmlInput: { 'data-testid': 'team-display-name' } }}
          />
          <FormControlLabel
            control={
              <Switch
                checked={values.isDisabled}
                onChange={(event) => setValues((v) => ({ ...v, isDisabled: event.target.checked }))}
                slotProps={{ input: { 'data-testid': 'team-disabled' } as object }}
              />
            }
            label="Disabled"
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isSubmitting} data-testid="team-form-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
          data-testid="team-form-submit"
        >
          {isSubmitting ? 'Saving…' : submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

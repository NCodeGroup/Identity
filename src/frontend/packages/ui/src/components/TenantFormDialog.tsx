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

/**
 * The tenant form's own field shape. `issuer` is a string here; empty means the tenant takes the issuer
 * the server derives for it.
 */
export type TenantFormValues = {
  readonly tenantId: string;
  readonly displayName: string;
  readonly issuer: string;
  readonly isDisabled: boolean;
};

/** One reason the address being entered cannot be served, as the server reports it. */
export type TenantAddressPreviewFailure = {
  readonly detail: string;
  readonly isConflict: boolean;
};

/**
 * The server's preview of the address being entered (its tenant ID and issuer): the issuer the tenant
 * would take, whether the ID is taken, and every reason the tenant could not be served there.
 */
export type TenantAddressPreview = {
  readonly issuer: string | null;
  readonly isIssuerDerived: boolean;
  readonly exists: boolean;
  readonly failures: readonly TenantAddressPreviewFailure[];
};

/** The address fields as entered, trimmed. */
export type TenantAddressInput = {
  readonly tenantId: string;
  readonly issuer: string;
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
  /** The server's preview of the address currently entered, or `undefined` while none matches it. */
  readonly addressPreview?: TenantAddressPreview | undefined;
  /** Invoked with the address fields as they change while open, so the container can preview them. */
  readonly onAddressChange?: (address: TenantAddressInput) => void;
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

/** Whether the value is a well-formed tenant ID, the precondition for asking the server about it. */
export function isValidTenantId(value: string): boolean {
  return tenantIdPattern.test(value);
}

/**
 * Stateless tenant create/edit form rendered in a dialog. The field state is local because it is
 * intrinsic to the controlled inputs (it reseeds from {@link TenantFormDialogProps.initialValues}
 * each time the dialog opens), so the component still renders in isolation under Storybook/RTL. All
 * persistence and server-error handling lives in the container that wires the mutation, as does the
 * address preview: the dialog reports the address as it is entered and renders the preview it is given
 * — the issuer an empty field resolves to, a taken ID, and every reason the address cannot be served.
 */
export function TenantFormDialog({
  open,
  title,
  submitLabel,
  initialValues,
  isTenantIdLocked = false,
  isSubmitting = false,
  error,
  addressPreview,
  onAddressChange,
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
  const issuer = values.issuer.trim();
  const isTenantIdValid = isTenantIdLocked || isValidTenantId(tenantId);
  const isTenantIdTaken = !isTenantIdLocked && addressPreview?.exists === true;
  const showTenantIdError =
    !isTenantIdLocked && tenantId.length > 0 && (!isTenantIdValid || isTenantIdTaken);
  const addressFailures = addressPreview?.failures ?? [];
  const derivedIssuer =
    addressPreview?.isIssuerDerived === true ? (addressPreview.issuer ?? undefined) : undefined;
  const canSubmit =
    isTenantIdValid &&
    !isTenantIdTaken &&
    addressFailures.length === 0 &&
    displayName.length > 0 &&
    !isSubmitting;

  useEffect(() => {
    if (open) {
      onAddressChange?.({ tenantId, issuer });
    }
  }, [open, tenantId, issuer, onAddressChange]);

  const handleSubmit = () => {
    if (!canSubmit) {
      return;
    }
    onSubmit({
      tenantId,
      displayName,
      issuer,
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
          {addressFailures.length > 0 && (
            <Alert severity="warning" data-testid="tenant-address-failures">
              {addressFailures.map((failure) => (
                <div key={failure.detail}>{failure.detail}</div>
              ))}
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
                : isTenantIdTaken
                  ? 'A tenant with this ID already exists.'
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
            helperText={
              derivedIssuer && issuer.length === 0
                ? `Leave empty to use ${derivedIssuer}.`
                : "The https URL the tenant is served under. Leave empty to use the deployment's default address."
            }
            placeholder={derivedIssuer ?? 'e.g. https://acme.auth.example.com'}
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

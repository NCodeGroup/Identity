import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

/** The minimal root key shape this presentational card needs (decoupled from the API contract). */
export type EncryptionRootKeyRow = {
  /** The name of the key wrapper that reaches the key, such as `aws-kms`. */
  readonly provider: string;
  /** The reference that names the key at its provider, such as a KMS key ARN. */
  readonly keyReference: string;
  /** `active` or `removing`; any other state reads as unavailable. */
  readonly state: string;
  readonly createdWhen: string;
  readonly validatedWhen?: string | null;
  /** Why the last validation failed, or absent when it did not. */
  readonly failure?: string | null;
};

export type EncryptionRootKeyCardProps = {
  /** The root key the tenant brings, or absent when the deployment's root key wraps its keys. */
  readonly rootKey: EncryptionRootKeyRow | null | undefined;
  /** Whether the tenant may choose a root key provider; without one and without a root key there is nothing to set. */
  readonly canSet: boolean;
  readonly isBusy?: boolean;
  readonly onSet: () => void;
  readonly onValidate: () => void;
  readonly onRemove: () => void;
};

const stateLabel = (state: string): 'active' | 'removing' | 'unavailable' =>
  state === 'active' || state === 'removing' ? state : 'unavailable';

const stateColor = { active: 'success', removing: 'warning', unavailable: 'error' } as const;

const formatWhen = (when: string | null | undefined): string =>
  when ? new Date(when).toLocaleString() : '—';

type FieldProps = { readonly label: string; readonly value: string; readonly testId: string };

function Field({ label, value, testId }: FieldProps) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'baseline' }}>
      <Typography variant="body2" color="text.secondary" sx={{ minWidth: 120 }}>
        {label}
      </Typography>
      <Typography variant="body2" sx={{ wordBreak: 'break-all' }} data-testid={testId}>
        {value}
      </Typography>
    </Stack>
  );
}

/**
 * Stateless card for the root key a tenant brings (ADR-1411): the provider, the key reference, its state, when it was
 * created and last validated, and why the last validation failed. It shows no key material, because the API returns
 * none. With no root key it says the deployment's root key wraps the tenant's keys, and offers "Set root key" only when
 * a provider is available. Holds no state and does no fetching.
 */
export function EncryptionRootKeyCard({
  rootKey,
  canSet,
  isBusy = false,
  onSet,
  onValidate,
  onRemove,
}: EncryptionRootKeyCardProps) {
  const state = rootKey ? stateLabel(rootKey.state) : undefined;

  return (
    <Paper variant="outlined" sx={{ p: 2 }} data-testid="root-key-card">
      <Stack spacing={1.5}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="subtitle1" component="h4">
            Root key
          </Typography>
          {canSet && (
            <Button
              variant="outlined"
              size="small"
              onClick={onSet}
              disabled={isBusy}
              data-testid="root-key-set"
            >
              Set root key
            </Button>
          )}
        </Stack>
        {rootKey && state ? (
          <>
            {state === 'unavailable' && (
              <Alert severity="error" data-testid="root-key-unavailable">
                {rootKey.failure
                  ? `The root key is unavailable: ${rootKey.failure}`
                  : 'The root key is unavailable.'}
              </Alert>
            )}
            <Stack spacing={0.5}>
              <Field label="Provider" value={rootKey.provider} testId="root-key-provider" />
              <Field
                label="Key reference"
                value={rootKey.keyReference}
                testId="root-key-reference"
              />
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <Typography variant="body2" color="text.secondary" sx={{ minWidth: 120 }}>
                  State
                </Typography>
                <Chip
                  size="small"
                  label={state}
                  color={stateColor[state]}
                  data-testid="root-key-state"
                />
              </Stack>
              <Field
                label="Created"
                value={formatWhen(rootKey.createdWhen)}
                testId="root-key-created"
              />
              <Field
                label="Last validated"
                value={formatWhen(rootKey.validatedWhen)}
                testId="root-key-validated"
              />
              <Field
                label="Last failure"
                value={rootKey.failure ?? '—'}
                testId="root-key-failure"
              />
            </Stack>
            <Stack direction="row" spacing={1}>
              <Button
                variant="outlined"
                size="small"
                onClick={onValidate}
                disabled={isBusy}
                data-testid="root-key-validate"
              >
                Validate
              </Button>
              <Button
                variant="outlined"
                size="small"
                color="error"
                onClick={onRemove}
                disabled={isBusy}
                data-testid="root-key-remove"
              >
                Remove
              </Button>
            </Stack>
          </>
        ) : (
          <Typography variant="body2" color="text.secondary" data-testid="root-key-none">
            {canSet
              ? "This tenant's keys are wrapped under the deployment's root key."
              : 'No root key provider is available to this tenant.'}
          </Typography>
        )}
      </Stack>
    </Paper>
  );
}

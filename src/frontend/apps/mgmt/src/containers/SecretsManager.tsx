import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  ExportPublicKeyDialog,
  RevealSecretDialog,
  SecretFormDialog,
  SecretTable,
  emptySecretFormValues,
  type PublicKeyMaterial,
  type SecretFormValues,
} from '@ncode/ui';
import type {
  CreateSecretRequest,
  CreatedSecretResource,
  PublicKeyResource,
  SecretResource,
} from '@ncode/api';
import { apiErrorMessage, useGetCapabilitiesQuery, type ApiError } from '@ncode/store';

export type SecretsManagerProps = {
  /** The owner noun used in headings and empty/warning copy (e.g. "client", "tenant", "server"). */
  readonly ownerLabel: string;
  readonly secrets: readonly SecretResource[];
  readonly isLoading: boolean;
  readonly loadError: boolean;
  readonly isCreating: boolean;
  readonly isDeleting: boolean;
  /** Creates a secret; must reject with an {@link ApiError}-shaped error on failure. */
  readonly onCreate: (body: CreateSecretRequest) => Promise<CreatedSecretResource>;
  /** Deletes a secret; must reject with an {@link ApiError}-shaped error on failure. */
  readonly onDelete: (secretId: string) => Promise<void>;
  /** Reads an asymmetric secret's public key; must reject with an {@link ApiError}-shaped error on failure. */
  readonly onExportPublicKey: (secretId: string) => Promise<PublicKeyResource>;
};

type SecretDialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'reveal'; readonly created: CreatedSecretResource }
  | { readonly kind: 'export'; readonly secret: SecretResource }
  | { readonly kind: 'delete'; readonly secret: SecretResource };

const closed: SecretDialogState = { kind: 'none' };

/** Seeds the create-secret form with a default expiry one year out, as a `datetime-local` string. */
function defaultSecretFormValues(): SecretFormValues {
  const expiry = new Date();
  expiry.setFullYear(expiry.getFullYear() + 1);
  // Trim to minute precision for the `datetime-local` input (YYYY-MM-DDTHH:mm).
  const local = new Date(expiry.getTime() - expiry.getTimezoneOffset() * 60_000);
  return { ...emptySecretFormValues, expiresWhen: local.toISOString().slice(0, 16) };
}

/**
 * Container-agnostic secrets surface shared by every secret owner (client, tenant, server). It owns only
 * the tab's dialog/form/error state and the one-time reveal (ADR-0050: the create response's material is
 * shown once in {@link RevealSecretDialog} and never re-fetched); the owner-specific RTK Query data and
 * mutations are injected so the same UI serves each owner's nested path. {@link SecretsManagerProps.onCreate}
 * and {@link SecretsManagerProps.onDelete} reject with an {@link ApiError} that is shaped for display.
 */
export function SecretsManager({
  ownerLabel,
  secrets,
  isLoading,
  loadError,
  isCreating,
  isDeleting,
  onCreate,
  onDelete,
  onExportPublicKey,
}: SecretsManagerProps) {
  // Read the clock once, in a state initializer: reading it during render would make the table's output impure.
  const [now] = useState(() => Date.now());
  const [dialog, setDialog] = useState<SecretDialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();
  const [publicKey, setPublicKey] = useState<PublicKeyMaterial>();
  const [exportError, setExportError] = useState<string>();

  // Which secret types expose an exportable public key is discovered from the server, not hard-coded.
  const { data: capabilities } = useGetCapabilitiesQuery();
  const exportableSecretTypes = (capabilities?.secretTypes ?? [])
    .filter((capability) => capability.supportsPublicKeyExport)
    .map((capability) => capability.secretType);
  const eccCurves = capabilities?.eccCurves ?? [];
  const algorithms = capabilities?.algorithms ?? [];

  const openDelete = (secretId: string) => {
    const secret = secrets.find((s) => s.secretId === secretId);
    if (!secret) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', secret });
  };

  const openExport = async (secretId: string) => {
    const secret = secrets.find((s) => s.secretId === secretId);
    if (!secret) {
      return;
    }
    setExportError(undefined);
    setPublicKey(undefined);
    setDialog({ kind: 'export', secret });
    try {
      setPublicKey(await onExportPublicKey(secretId));
    } catch (exportFailure) {
      setExportError(apiErrorMessage(exportFailure as ApiError));
    }
  };

  const handleCreate = async (values: SecretFormValues) => {
    setFormError(undefined);
    try {
      const created = await onCreate({
        secretType: values.secretType,
        keySizeBits: values.keySizeBits,
        curve: values.secretType === 'ecc' && values.curve.length > 0 ? values.curve : null,
        use: values.use.length > 0 ? values.use : null,
        algorithm: values.algorithm.length > 0 ? values.algorithm : null,
        expiresWhen: new Date(values.expiresWhen).toISOString(),
      });
      setDialog({ kind: 'reveal', created });
    } catch (submitError) {
      setFormError(apiErrorMessage(submitError as ApiError));
    }
  };

  const handleDelete = async () => {
    if (dialog.kind !== 'delete') {
      return;
    }
    setDeleteError(undefined);
    try {
      await onDelete(dialog.secret.secretId);
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Typography variant="h6" component="h3">
          Secrets
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => {
            setFormError(undefined);
            setDialog({ kind: 'create' });
          }}
          data-testid="create-secret"
        >
          Create secret
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={loadError ? 'Failed to load secrets.' : undefined}
        isEmpty={secrets.length === 0}
        emptyMessage={`This ${ownerLabel} has no secrets yet.`}
      >
        <SecretTable
          secrets={[...secrets]}
          now={now}
          onDelete={openDelete}
          onExportPublicKey={openExport}
          exportableSecretTypes={exportableSecretTypes}
        />
      </DataStateBoundary>

      <SecretFormDialog
        open={dialog.kind === 'create'}
        initialValues={defaultSecretFormValues()}
        isSubmitting={isCreating}
        error={formError}
        eccCurves={eccCurves}
        algorithms={algorithms}
        onSubmit={handleCreate}
        onCancel={() => setDialog(closed)}
      />

      <RevealSecretDialog
        open={dialog.kind === 'reveal'}
        secretId={dialog.kind === 'reveal' ? dialog.created.secretId : ''}
        secretMaterial={dialog.kind === 'reveal' ? dialog.created.secretMaterial : null}
        onClose={() => setDialog(closed)}
      />

      <ExportPublicKeyDialog
        open={dialog.kind === 'export'}
        secretId={dialog.kind === 'export' ? dialog.secret.secretId : ''}
        publicKey={publicKey}
        isLoading={dialog.kind === 'export' && !publicKey && !exportError}
        error={exportError}
        onClose={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete secret"
        message={
          dialog.kind === 'delete'
            ? `Delete secret “${dialog.secret.secretId}”? Anything using it will stop authenticating.`
            : ''
        }
        isBusy={isDeleting}
        error={deleteError}
        onConfirm={handleDelete}
        onCancel={() => setDialog(closed)}
      />
    </>
  );
}

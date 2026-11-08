import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import LinkIcon from '@mui/icons-material/Link';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  ExportPublicKeyDialog,
  RemoteSecretBindDialog,
  RevealSecretDialog,
  SecretFormDialog,
  SecretTable,
  emptyRemoteSecretBindValues,
  emptySecretFormValues,
  type PublicKeyMaterial,
  type RemoteSecretBindValues,
  type SecretFormValues,
} from '@ncode/ui';
import type {
  CreateRemoteSecretRequest,
  CreateSecretRequest,
  CreatedSecretResource,
  ImportRemoteSecretRequest,
  PublicKeyResource,
  RotateSecretRequest,
  SecretResource,
} from '@ncode/api';
import { apiErrorMessage, useGetCapabilitiesQuery, type ApiError } from '@ncode/store';

export type SecretsManagerProps = {
  /** The owner noun used in headings and empty/warning copy (e.g. "client", "tenant", "resource server"). */
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
  /**
   * Rotates a shared secret: adds a replacement and keeps the rotated secret until it expires or is deleted
   * (ADR-1320); must reject with an {@link ApiError}-shaped error on failure. The rotate action is shown only when
   * this is supplied.
   */
  readonly onRotate?: (
    secretId: string,
    body: RotateSecretRequest,
  ) => Promise<CreatedSecretResource>;
  readonly isRotating?: boolean;
  /**
   * Creates a tenant's signing key in a remote key provider's key management service, which keeps the private half;
   * must reject with an {@link ApiError}-shaped error on failure. The custody choice is offered only when this is
   * supplied (ADR-1475, ADR-1476).
   */
  readonly onCreateRemote?: (body: CreateRemoteSecretRequest) => Promise<SecretResource>;
  /**
   * Binds a key a remote key provider's key management service already holds; must reject with an
   * {@link ApiError}-shaped error on failure. The bind action is shown only when this is supplied (ADR-1475,
   * ADR-1476).
   */
  readonly onImportRemote?: (body: ImportRemoteSecretRequest) => Promise<SecretResource>;
  readonly isBinding?: boolean;
};

type SecretDialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'bind' }
  | { readonly kind: 'reveal'; readonly created: CreatedSecretResource }
  | { readonly kind: 'export'; readonly secret: SecretResource }
  | { readonly kind: 'delete'; readonly secret: SecretResource }
  | { readonly kind: 'rotate'; readonly secret: SecretResource };

const closed: SecretDialogState = { kind: 'none' };

/** The default expiry of a created or rotated secret: one year out. */
function defaultExpiry(): Date {
  const expiry = new Date();
  expiry.setFullYear(expiry.getFullYear() + 1);
  return expiry;
}

/** How long a rotated secret keeps authenticating, so its holder can switch to the replacement. */
const rotationGraceDays = 7;

/** The expiry given to a rotated secret: the grace period from now. */
function rotationGraceEnd(): Date {
  const end = new Date();
  end.setDate(end.getDate() + rotationGraceDays);
  return end;
}

/** Seeds a dialog's expiry input with the default expiry, as a `datetime-local` string. */
function defaultExpiryLocal(): string {
  const expiry = defaultExpiry();
  // Trim to minute precision for the `datetime-local` input (YYYY-MM-DDTHH:mm).
  const local = new Date(expiry.getTime() - expiry.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

/** Seeds the create-secret form with the default expiry, as a `datetime-local` string. */
function defaultSecretFormValues(): SecretFormValues {
  return { ...emptySecretFormValues, expiresWhen: defaultExpiryLocal() };
}

/** Seeds the bind dialog with the default expiry, as a `datetime-local` string. */
function defaultBindValues(): RemoteSecretBindValues {
  return { ...emptyRemoteSecretBindValues, expiresWhen: defaultExpiryLocal() };
}

/**
 * Container-agnostic secrets surface shared by every secret owner (tenant, client, resource server). It owns only
 * the tab's dialog/form/error state and the one-time reveal (ADR-1050: the create response's material is
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
  onRotate,
  isRotating = false,
  onCreateRemote,
  onImportRemote,
  isBinding = false,
}: SecretsManagerProps) {
  // Read the clock once, in a state initializer: reading it during render would make the table's output impure.
  const [now] = useState(() => Date.now());
  const [dialog, setDialog] = useState<SecretDialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();
  const [rotateError, setRotateError] = useState<string>();
  const [bindError, setBindError] = useState<string>();
  const [publicKey, setPublicKey] = useState<PublicKeyMaterial>();
  const [exportError, setExportError] = useState<string>();

  // Which secret types expose an exportable public key is discovered from the server, not hard-coded.
  const { data: capabilities } = useGetCapabilitiesQuery();
  const exportableSecretTypes = (capabilities?.secretTypes ?? [])
    .filter((capability) => capability.supportsPublicKeyExport)
    .map((capability) => capability.secretType);
  // The secret types, curves and algorithms the create form offers are discovered from the server too.
  const secretTypes = (capabilities?.secretTypes ?? []).map((capability) => capability.secretType);
  const eccCurves = capabilities?.eccCurves ?? [];
  const algorithms = capabilities?.algorithms ?? [];
  // The remote key providers, with the algorithms each holds keys for, are discovered from the server too.
  const remoteKeyProviders = capabilities?.remoteKeyProviders ?? [];
  const canBindRemote =
    onImportRemote !== undefined &&
    remoteKeyProviders.some((provider) => provider.algorithms.length > 0);
  // A shared secret is rotated; a key is replaced by importing a new one (ADR-1320).
  const rotatableSecretTypes = (capabilities?.secretTypes ?? [])
    .filter((capability) => !capability.isAsymmetric)
    .map((capability) => capability.secretType);

  const openDelete = (secretId: string) => {
    const secret = secrets.find((s) => s.secretId === secretId);
    if (!secret) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', secret });
  };

  const openRotate = (secretId: string) => {
    const secret = secrets.find((s) => s.secretId === secretId);
    if (!secret) {
      return;
    }
    setRotateError(undefined);
    setDialog({ kind: 'rotate', secret });
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
      if (values.custody.length > 0 && onCreateRemote) {
        await onCreateRemote({
          remoteKeyProvider: values.custody,
          secretType: values.secretType,
          keySizeBits: values.keySizeBits,
          curve: values.curve.length > 0 ? values.curve : null,
          algorithm: values.algorithm,
          expiresWhen: new Date(values.expiresWhen).toISOString(),
        });
        setDialog(closed);
        return;
      }
      const created = await onCreate({
        secretType: values.secretType,
        keySizeBits: values.keySizeBits,
        // The form names a curve for every secret type with curves (`ecc`, `okp`, …) and none for one without.
        curve: values.curve.length > 0 ? values.curve : null,
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

  const handleRotate = async () => {
    if (dialog.kind !== 'rotate' || !onRotate) {
      return;
    }
    setRotateError(undefined);
    try {
      const created = await onRotate(dialog.secret.secretId, {
        expiresWhen: defaultExpiry().toISOString(),
        rotatedExpiresWhen: rotationGraceEnd().toISOString(),
      });
      setDialog({ kind: 'reveal', created });
    } catch (rotateFailure) {
      setRotateError(apiErrorMessage(rotateFailure as ApiError));
    }
  };

  const handleBind = async (values: RemoteSecretBindValues) => {
    if (!onImportRemote) {
      return;
    }
    setBindError(undefined);
    try {
      await onImportRemote({
        remoteKeyProvider: values.remoteKeyProvider,
        remoteKeyId: values.remoteKeyId,
        secretType: values.secretType,
        keySizeBits: values.keySizeBits,
        curve: values.curve.length > 0 ? values.curve : null,
        algorithm: values.algorithm,
        expiresWhen: new Date(values.expiresWhen).toISOString(),
      });
      setDialog(closed);
    } catch (bindFailure) {
      setBindError(apiErrorMessage(bindFailure as ApiError));
    }
  };

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Typography variant="h6" component="h3">
          Secrets
        </Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          {canBindRemote && (
            <Button
              variant="outlined"
              startIcon={<LinkIcon />}
              onClick={() => {
                setBindError(undefined);
                setDialog({ kind: 'bind' });
              }}
              data-testid="bind-remote-secret"
            >
              Bind KMS key
            </Button>
          )}
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
          {...(onRotate ? { onRotate: openRotate } : {})}
          rotatableSecretTypes={rotatableSecretTypes}
        />
      </DataStateBoundary>

      <SecretFormDialog
        open={dialog.kind === 'create'}
        initialValues={defaultSecretFormValues()}
        isSubmitting={isCreating}
        error={formError}
        secretTypes={secretTypes}
        eccCurves={eccCurves}
        algorithms={algorithms}
        remoteKeyProviders={onCreateRemote ? remoteKeyProviders : []}
        onSubmit={handleCreate}
        onCancel={() => setDialog(closed)}
      />

      <RemoteSecretBindDialog
        open={dialog.kind === 'bind'}
        initialValues={defaultBindValues()}
        isSubmitting={isBinding}
        error={bindError}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={handleBind}
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

      <ConfirmDialog
        open={dialog.kind === 'rotate'}
        title="Rotate secret"
        message={
          dialog.kind === 'rotate'
            ? `Create a replacement for secret “${dialog.secret.secretId}” that expires in a year? The current secret keeps authenticating for up to ${rotationGraceDays} days, or until you delete it, so its holder can switch.`
            : ''
        }
        confirmLabel="Rotate"
        confirmColor="primary"
        isBusy={isRotating}
        error={rotateError}
        onConfirm={handleRotate}
        onCancel={() => setDialog(closed)}
      />
    </>
  );
}

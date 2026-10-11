import { useMemo, useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  RevealSecretDialog,
  SecretFormDialog,
  SecretTable,
  SettingsEditor,
  emptySecretFormValues,
  type SecretFormValues,
} from '@ncode/ui';
import type {
  ClientResource,
  CreatedSecretResource,
  ManagementScope,
  SecretResource,
} from '@ncode/api';
import {
  apiErrorMessage,
  useCreateClientSecretMutation,
  useDeleteClientSecretMutation,
  useGetClientEffectiveSettingsQuery,
  useGetClientSettingsQuery,
  useListClientSecretsQuery,
  useUpdateClientSettingsMutation,
  type ApiError,
} from '@ncode/store';

export type ClientDetailPageProps = {
  readonly scope: ManagementScope;
  readonly client: ClientResource;
  readonly onBack: () => void;
};

type ClientSurface = 'secrets' | 'settings';

/** Seeds the create-secret form with a default expiry one year out, as a `datetime-local` string. */
function defaultSecretFormValues(): SecretFormValues {
  const expiry = new Date();
  expiry.setFullYear(expiry.getFullYear() + 1);
  // Trim to minute precision for the `datetime-local` input (YYYY-MM-DDTHH:mm).
  const local = new Date(expiry.getTime() - expiry.getTimezoneOffset() * 60_000);
  return { ...emptySecretFormValues, expiresWhen: local.toISOString().slice(0, 16) };
}

/**
 * Container: drills into a single client to manage its secrets and settings. The parent
 * {@link import('./ClientsPage.js').ClientsPage} supplies the client and owns the drill-in selection.
 * Secrets honor the one-time reveal (ADR-0050): the create response's material is shown once in
 * {@link RevealSecretDialog} and never re-fetched. Settings are edited as an opaque JSON object whose
 * save is a top-level JSON Patch diff against the persisted object. Owns only tab/dialog/error state.
 */
export function ClientDetailPage({ scope, client, onBack }: ClientDetailPageProps) {
  const [surface, setSurface] = useState<ClientSurface>('secrets');

  return (
    <>
      <Button
        size="small"
        startIcon={<ArrowBackIcon />}
        onClick={onBack}
        data-testid="client-detail-back"
        sx={{ mb: 1 }}
      >
        Clients
      </Button>
      <Stack mb={2}>
        <Typography variant="h5" component="h2">
          {client.clientId}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Client · {client.isDisabled ? 'Disabled' : 'Active'}
        </Typography>
      </Stack>
      <Tabs
        value={surface}
        onChange={(_event, value: ClientSurface) => setSurface(value)}
        sx={{ mb: 2 }}
      >
        <Tab value="secrets" label="Secrets" data-testid="client-tab-secrets" />
        <Tab value="settings" label="Settings" data-testid="client-tab-settings" />
      </Tabs>
      {surface === 'secrets' ? (
        <ClientSecretsTab scope={scope} clientId={client.clientId} />
      ) : (
        <ClientSettingsTab scope={scope} clientId={client.clientId} />
      )}
    </>
  );
}

type ClientTabProps = {
  readonly scope: ManagementScope;
  readonly clientId: string;
};

type SecretDialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'reveal'; readonly created: CreatedSecretResource }
  | { readonly kind: 'delete'; readonly secret: SecretResource };

const secretClosed: SecretDialogState = { kind: 'none' };

function ClientSecretsTab({ scope, clientId }: ClientTabProps) {
  const { data, isLoading, error } = useListClientSecretsQuery({ scope, clientId });
  const secrets = data?.secrets ?? [];

  const [createSecret, { isLoading: isCreating }] = useCreateClientSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteClientSecretMutation();

  const [dialog, setDialog] = useState<SecretDialogState>(secretClosed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const openDelete = (secretId: string) => {
    const secret = secrets.find((s) => s.secretId === secretId);
    if (!secret) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', secret });
  };

  const handleCreate = async (values: SecretFormValues) => {
    setFormError(undefined);
    try {
      const created = await createSecret({
        scope,
        clientId,
        body: {
          secretType: values.secretType,
          keySizeBits: values.keySizeBits,
          use: values.use.length > 0 ? values.use : null,
          algorithm: values.algorithm.length > 0 ? values.algorithm : null,
          expiresWhen: new Date(values.expiresWhen).toISOString(),
        },
      }).unwrap();
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
      await deleteSecret({ scope, clientId, secretId: dialog.secret.secretId }).unwrap();
      setDialog(secretClosed);
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
        error={error ? 'Failed to load secrets.' : undefined}
        isEmpty={secrets.length === 0}
        emptyMessage="This client has no secrets yet."
      >
        <SecretTable secrets={secrets} onDelete={openDelete} />
      </DataStateBoundary>

      <SecretFormDialog
        open={dialog.kind === 'create'}
        initialValues={defaultSecretFormValues()}
        isSubmitting={isCreating}
        error={formError}
        onSubmit={handleCreate}
        onCancel={() => setDialog(secretClosed)}
      />

      <RevealSecretDialog
        open={dialog.kind === 'reveal'}
        secretId={dialog.kind === 'reveal' ? dialog.created.secretId : ''}
        secretMaterial={dialog.kind === 'reveal' ? dialog.created.secretMaterial : null}
        onClose={() => setDialog(secretClosed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete secret"
        message={
          dialog.kind === 'delete'
            ? `Delete secret “${dialog.secret.secretId}”? Clients using it will stop authenticating.`
            : ''
        }
        isBusy={isDeleting}
        error={deleteError}
        onConfirm={handleDelete}
        onCancel={() => setDialog(secretClosed)}
      />
    </>
  );
}

function ClientSettingsTab({ scope, clientId }: ClientTabProps) {
  const { data: settings, isLoading, error } = useGetClientSettingsQuery({ scope, clientId });
  const { data: effective } = useGetClientEffectiveSettingsQuery({ scope, clientId });
  const [updateSettings, { isLoading: isSaving }] = useUpdateClientSettingsMutation();

  const [saveError, setSaveError] = useState<string>();

  const initialJson = useMemo(
    () => JSON.stringify(settings?.settings ?? {}, null, 2),
    [settings?.settings],
  );
  const effectiveJson = useMemo(
    () => JSON.stringify(effective?.settings ?? {}, null, 2),
    [effective?.settings],
  );

  const handleSave = async (desired: Record<string, unknown>) => {
    if (!settings) {
      return;
    }
    setSaveError(undefined);
    try {
      await updateSettings({
        scope,
        clientId,
        concurrencyToken: settings.concurrencyToken,
        original: settings.settings,
        desired,
      }).unwrap();
    } catch (submitError) {
      setSaveError(apiErrorMessage(submitError as ApiError));
    }
  };

  return (
    <DataStateBoundary isLoading={isLoading} error={error ? 'Failed to load settings.' : undefined}>
      <SettingsEditor
        initialJson={initialJson}
        effectiveJson={effectiveJson}
        isSaving={isSaving}
        error={saveError}
        onSave={handleSave}
      />
    </DataStateBoundary>
  );
}

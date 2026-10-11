import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ClientFormDialog,
  ClientTable,
  ConfirmDialog,
  DataStateBoundary,
  emptyClientFormValues,
  type ClientFormValues,
} from '@ncode/ui';
import type { ClientResource, ManagementScope } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateClientMutation,
  useDeleteClientMutation,
  useListClientsQuery,
  useUpdateClientMutation,
  type ApiError,
} from '@ncode/store';
import { ClientDetailPage } from './ClientDetailPage.js';

export type ClientsPageProps = {
  readonly scope: ManagementScope;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly client: ClientResource }
  | { readonly kind: 'delete'; readonly client: ClientResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: browses and administers the clients within a given management scope. The scope is passed
 * down from the stateful shell, so a tenant administrator view is always bound to its own tenant (the
 * API path is nested under the tenant — the boundary is structural, not a filter). Wires the RTK Query
 * client list and mutations to the stateless {@link ClientTable}, {@link ClientFormDialog}, and
 * {@link ConfirmDialog}; owns only dialog/error state.
 */
export function ClientsPage({ scope }: ClientsPageProps) {
  const { data, isLoading, error } = useListClientsQuery({ scope });
  const clients = data?.items ?? [];

  const [createClient, { isLoading: isCreating }] = useCreateClientMutation();
  const [updateClient, { isLoading: isUpdating }] = useUpdateClientMutation();
  const [deleteClient, { isLoading: isDeleting }] = useDeleteClientMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const [selectedId, setSelectedId] = useState<string>();
  const selected = clients.find((c) => c.clientId === selectedId);
  if (selected) {
    return (
      <ClientDetailPage scope={scope} client={selected} onBack={() => setSelectedId(undefined)} />
    );
  }

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (clientId: string) => {
    const client = clients.find((c) => c.clientId === clientId);
    if (!client) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', client });
  };
  const openDelete = (clientId: string) => {
    const client = clients.find((c) => c.clientId === clientId);
    if (!client) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', client });
  };

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: ClientFormValues =
    dialog.kind === 'edit' ? { isDisabled: dialog.client.isDisabled } : emptyClientFormValues;

  const handleSubmit = async (values: ClientFormValues) => {
    setFormError(undefined);
    try {
      if (dialog.kind === 'create') {
        await createClient({
          scope,
          body: { isDisabled: values.isDisabled, settings: {} },
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateClient({
          scope,
          clientId: dialog.client.clientId,
          concurrencyToken: dialog.client.concurrencyToken,
          fields: { isDisabled: values.isDisabled },
        }).unwrap();
      }
      setDialog(closed);
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
      await deleteClient({ scope, clientId: dialog.client.clientId }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Typography variant="h5" component="h2">
          Clients
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-client"
        >
          Create client
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load clients.' : undefined}
        isEmpty={clients.length === 0}
        emptyMessage="No clients in this scope yet."
      >
        <ClientTable
          clients={clients}
          onSelect={setSelectedId}
          onEdit={openEdit}
          onDelete={openDelete}
        />
      </DataStateBoundary>

      <ClientFormDialog
        open={formOpen}
        title={dialog.kind === 'edit' ? 'Edit client' : 'Create client'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Create'}
        initialValues={formInitial}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete client"
        message={
          dialog.kind === 'delete'
            ? `Delete client “${dialog.client.clientId}”? This cannot be undone.`
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

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
import type { ClientResource, MachineClientOwner } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateMachineClientMutation,
  useDeleteClientMutation,
  useListMachineClientsQuery,
  useUpdateClientMutation,
  type ApiError,
} from '@ncode/store';
import { ClientDetailPage } from './ClientDetailPage.js';

export type MachineClientsPageProps = {
  readonly owner: MachineClientOwner;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly client: ClientResource }
  | { readonly kind: 'delete'; readonly client: ClientResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: browses and administers the machine clients the platform or a team owns (ADR-1466);
 * each lives in the control-plane tenant, so its edit, delete and detail go through that tenant's
 * client routes (ADR-1421). Wires the RTK Query machine-client list and the client mutations to the
 * stateless {@link ClientTable}, {@link ClientFormDialog}, and {@link ConfirmDialog}; owns only
 * dialog/error state.
 */
export function MachineClientsPage({ owner }: MachineClientsPageProps) {
  const { data, isLoading, error } = useListMachineClientsQuery(owner);
  const clients = data?.items ?? [];

  const [createMachineClient, { isLoading: isCreating }] = useCreateMachineClientMutation();
  const [updateClient, { isLoading: isUpdating }] = useUpdateClientMutation();
  const [deleteClient, { isLoading: isDeleting }] = useDeleteClientMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const [selectedId, setSelectedId] = useState<string>();
  const selected = clients.find((c) => c.clientId === selectedId);
  if (selected) {
    return <ClientDetailPage client={selected} onBack={() => setSelectedId(undefined)} />;
  }

  const scope = owner.kind === 'platform' ? 'the platform' : 'this team';

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
        await createMachineClient({
          owner,
          body: { isDisabled: values.isDisabled, settings: {} },
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateClient({
          tenantId: dialog.client.tenantId,
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
      await deleteClient({
        tenantId: dialog.client.tenantId,
        clientId: dialog.client.clientId,
      }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          mb: 1,
        }}
      >
        <Typography variant="h5" component="h2">
          Machine clients
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-machine-client"
        >
          Create machine client
        </Button>
      </Box>
      <Typography
        variant="body2"
        sx={{ color: 'text.secondary', mb: 1 }}
        data-testid="machine-clients-description"
      >
        A machine client obtains tokens for itself with the client credentials grant and holds roles
        only within {scope}.
      </Typography>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load machine clients.' : undefined}
        isEmpty={clients.length === 0}
        emptyMessage={
          owner.kind === 'platform'
            ? 'The platform has no machine clients yet.'
            : 'This team has no machine clients yet.'
        }
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
        title={dialog.kind === 'edit' ? 'Edit machine client' : 'Create machine client'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Create'}
        initialValues={formInitial}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete machine client"
        message={
          dialog.kind === 'delete'
            ? `Delete machine client “${dialog.client.clientId}”? This cannot be undone.`
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

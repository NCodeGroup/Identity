import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  ServerFormDialog,
  ServerTable,
  emptyServerFormValues,
  type ServerFormValues,
} from '@ncode/ui';
import type { ServerResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateServerMutation,
  useDeleteServerMutation,
  useListServersQuery,
  useUpdateServerMutation,
  type ApiError,
} from '@ncode/store';
import { ServerDetailPage } from './ServerDetailPage.js';

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly server: ServerResource }
  | { readonly kind: 'delete'; readonly server: ServerResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: the global-scope server browser and CRUD surface. Servers are system-level settings/secret
 * owners; this lists them, wires the create/edit/delete mutations to the stateless {@link ServerTable},
 * {@link ServerFormDialog}, and {@link ConfirmDialog}, and drills into {@link ServerDetailPage} to manage
 * a server's secrets and settings. Owns only dialog/error and drill-in selection state; all persistence,
 * cache invalidation, and error shaping come from the store layer.
 */
export function ServersPage() {
  const { data, isLoading, error } = useListServersQuery();
  const servers = data?.items ?? [];

  const [createServer, { isLoading: isCreating }] = useCreateServerMutation();
  const [updateServer, { isLoading: isUpdating }] = useUpdateServerMutation();
  const [deleteServer, { isLoading: isDeleting }] = useDeleteServerMutation();

  const [selectedId, setSelectedId] = useState<string>();
  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const selected = servers.find((s) => s.serverId === selectedId);
  if (selected) {
    return <ServerDetailPage server={selected} onBack={() => setSelectedId(undefined)} />;
  }

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (serverId: string) => {
    const server = servers.find((s) => s.serverId === serverId);
    if (!server) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', server });
  };
  const openDelete = (serverId: string) => {
    const server = servers.find((s) => s.serverId === serverId);
    if (!server) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', server });
  };

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: ServerFormValues =
    dialog.kind === 'edit' ? { displayName: dialog.server.displayName } : emptyServerFormValues;

  const handleSubmit = async (values: ServerFormValues) => {
    setFormError(undefined);
    try {
      if (dialog.kind === 'create') {
        await createServer({ displayName: values.displayName, settings: {} }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateServer({
          serverId: dialog.server.serverId,
          concurrencyToken: dialog.server.concurrencyToken,
          fields: { displayName: values.displayName },
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
      await deleteServer({ serverId: dialog.server.serverId }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Typography variant="h5" component="h2">
          Servers
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-server"
        >
          Create server
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load servers.' : undefined}
        isEmpty={servers.length === 0}
        emptyMessage="No servers exist yet."
      >
        <ServerTable
          servers={servers.map((s) => ({ serverId: s.serverId, displayName: s.displayName }))}
          onSelect={setSelectedId}
          onEdit={openEdit}
          onDelete={openDelete}
        />
      </DataStateBoundary>

      <ServerFormDialog
        open={formOpen}
        title={dialog.kind === 'edit' ? 'Edit server' : 'Create server'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Create'}
        initialValues={formInitial}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete server"
        message={
          dialog.kind === 'delete'
            ? `Delete “${dialog.server.displayName}”? This cannot be undone.`
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

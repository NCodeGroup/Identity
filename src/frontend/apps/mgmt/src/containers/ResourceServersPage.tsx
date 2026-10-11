import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  ResourceServerFormDialog,
  ResourceServerTable,
  emptyResourceServerFormValues,
  type ResourceServerFormValues,
} from '@ncode/ui';
import type { ManagementScope, ResourceServerResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateResourceServerMutation,
  useDeleteResourceServerMutation,
  useListResourceServersQuery,
  useUpdateResourceServerMutation,
  type ApiError,
} from '@ncode/store';
import { ResourceServerScopesPage } from './ResourceServerScopesPage.js';

export type ResourceServersPageProps = {
  readonly scope: ManagementScope;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly resourceServer: ResourceServerResource }
  | { readonly kind: 'delete'; readonly resourceServer: ResourceServerResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: browses and administers the resource servers (APIs) within a tenant scope. Wires the RTK
 * Query resource-server list and mutations to the stateless {@link ResourceServerTable},
 * {@link ResourceServerFormDialog}, and {@link ConfirmDialog}; owns only dialog/error and drill-in
 * state. Selecting a resource server drills into its scopes ({@link ResourceServerScopesPage}), which
 * reads the embedded scopes from this same list query so a scope mutation refreshes both views.
 */
export function ResourceServersPage({ scope }: ResourceServersPageProps) {
  const { data, isLoading, error } = useListResourceServersQuery({ scope });
  const resourceServers = data?.items ?? [];

  const [createResourceServer, { isLoading: isCreating }] = useCreateResourceServerMutation();
  const [updateResourceServer, { isLoading: isUpdating }] = useUpdateResourceServerMutation();
  const [deleteResourceServer, { isLoading: isDeleting }] = useDeleteResourceServerMutation();

  const [selectedId, setSelectedId] = useState<string>();
  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const selected = resourceServers.find((r) => r.resourceServerId === selectedId);
  if (selected) {
    return (
      <ResourceServerScopesPage
        scope={scope}
        resourceServer={selected}
        onBack={() => setSelectedId(undefined)}
      />
    );
  }

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (resourceServerId: string) => {
    const resourceServer = resourceServers.find((r) => r.resourceServerId === resourceServerId);
    if (!resourceServer) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', resourceServer });
  };
  const openDelete = (resourceServerId: string) => {
    const resourceServer = resourceServers.find((r) => r.resourceServerId === resourceServerId);
    if (!resourceServer) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', resourceServer });
  };

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: ResourceServerFormValues =
    dialog.kind === 'edit'
      ? {
          identifier: dialog.resourceServer.identifier,
          name: dialog.resourceServer.name,
          isDisabled: dialog.resourceServer.isDisabled,
        }
      : emptyResourceServerFormValues;

  const handleSubmit = async (values: ResourceServerFormValues) => {
    setFormError(undefined);
    try {
      if (dialog.kind === 'create') {
        await createResourceServer({
          scope,
          body: {
            identifier: values.identifier,
            name: values.name,
            isDisabled: values.isDisabled,
            settings: {},
          },
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateResourceServer({
          scope,
          resourceServerId: dialog.resourceServer.resourceServerId,
          concurrencyToken: dialog.resourceServer.concurrencyToken,
          fields: { name: values.name, isDisabled: values.isDisabled },
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
      await deleteResourceServer({
        scope,
        resourceServerId: dialog.resourceServer.resourceServerId,
      }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Typography variant="h5" component="h2">
          Resource servers
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-resource-server"
        >
          Create resource server
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load resource servers.' : undefined}
        isEmpty={resourceServers.length === 0}
        emptyMessage="No resource servers in this tenant yet."
      >
        <ResourceServerTable
          resourceServers={resourceServers.map((r) => ({
            resourceServerId: r.resourceServerId,
            name: r.name,
            identifier: r.identifier,
            isDisabled: r.isDisabled,
            isSystem: r.isSystem,
            scopeCount: r.scopes.length,
          }))}
          onSelect={setSelectedId}
          onEdit={openEdit}
          onDelete={openDelete}
        />
      </DataStateBoundary>

      <ResourceServerFormDialog
        open={formOpen}
        title={dialog.kind === 'edit' ? 'Edit resource server' : 'Create resource server'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Create'}
        identifierEditable={dialog.kind !== 'edit'}
        initialValues={formInitial}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete resource server"
        message={
          dialog.kind === 'delete'
            ? `Delete “${dialog.resourceServer.name}”? This cannot be undone.`
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

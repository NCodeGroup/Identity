import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  ScopeFormDialog,
  ScopeTable,
  emptyScopeFormValues,
  type ScopeFormValues,
} from '@ncode/ui';
import type { ManagementScope, ResourceServerResource, ScopeResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateScopeMutation,
  useDeleteScopeMutation,
  useUpdateScopeMutation,
  type ApiError,
} from '@ncode/store';

export type ResourceServerScopesPageProps = {
  readonly scope: ManagementScope;
  readonly resourceServer: ResourceServerResource;
  readonly onBack: () => void;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly scope: ScopeResource }
  | { readonly kind: 'delete'; readonly scope: ScopeResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: manages the scopes (permissions) owned by a single resource server. The resource server
 * (with its embedded scopes) is supplied by the parent's list query, so a scope mutation that
 * invalidates that list re-renders this view with fresh scopes. Wires the RTK Query scope mutations
 * to the stateless {@link ScopeTable}, {@link ScopeFormDialog}, and {@link ConfirmDialog}; owns only
 * dialog/error state. A scope update is a whole-resource PUT (the value is immutable).
 */
export function ResourceServerScopesPage({
  scope,
  resourceServer,
  onBack,
}: ResourceServerScopesPageProps) {
  const resourceServerId = resourceServer.resourceServerId;
  const scopes = resourceServer.scopes;

  const [createScope, { isLoading: isCreating }] = useCreateScopeMutation();
  const [updateScope, { isLoading: isUpdating }] = useUpdateScopeMutation();
  const [deleteScope, { isLoading: isDeleting }] = useDeleteScopeMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (scopeValue: string) => {
    const target = scopes.find((s) => s.value === scopeValue);
    if (!target) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', scope: target });
  };
  const openDelete = (scopeValue: string) => {
    const target = scopes.find((s) => s.value === scopeValue);
    if (!target) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', scope: target });
  };

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: ScopeFormValues =
    dialog.kind === 'edit'
      ? { value: dialog.scope.value, description: dialog.scope.description ?? '' }
      : emptyScopeFormValues;

  const handleSubmit = async (values: ScopeFormValues) => {
    setFormError(undefined);
    const description = values.description.length > 0 ? values.description : null;
    try {
      if (dialog.kind === 'create') {
        await createScope({
          scope,
          resourceServerId,
          body: { value: values.value, description },
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateScope({
          scope,
          resourceServerId,
          scopeValue: dialog.scope.value,
          body: { description },
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
      await deleteScope({ scope, resourceServerId, scopeValue: dialog.scope.value }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Button
        size="small"
        startIcon={<ArrowBackIcon />}
        onClick={onBack}
        data-testid="scopes-back"
        sx={{ mb: 1 }}
      >
        Resource servers
      </Button>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Stack>
          <Typography variant="h5" component="h2">
            Scopes
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {resourceServer.name} · {resourceServer.identifier}
          </Typography>
        </Stack>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-scope"
        >
          Add scope
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={false}
        isEmpty={scopes.length === 0}
        emptyMessage="This resource server has no scopes yet."
      >
        <ScopeTable scopes={scopes} onEdit={openEdit} onDelete={openDelete} />
      </DataStateBoundary>

      <ScopeFormDialog
        open={formOpen}
        title={dialog.kind === 'edit' ? 'Edit scope' : 'Add scope'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Add'}
        valueEditable={dialog.kind !== 'edit'}
        initialValues={formInitial}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete scope"
        message={
          dialog.kind === 'delete'
            ? `Delete scope “${dialog.scope.value}”? This cannot be undone.`
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

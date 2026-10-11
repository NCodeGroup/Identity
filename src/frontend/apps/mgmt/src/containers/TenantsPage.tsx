import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  TenantFormDialog,
  TenantTable,
  emptyTenantFormValues,
  type TenantFormValues,
} from '@ncode/ui';
import type { TenantResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateTenantMutation,
  useDeleteTenantMutation,
  useListTenantsQuery,
  useUpdateTenantMutation,
  type ApiError,
} from '@ncode/store';

export type TenantsPageProps = {
  readonly onOpenTenant: (tenantId: string) => void;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly tenant: TenantResource }
  | { readonly kind: 'delete'; readonly tenant: TenantResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: the global-scope tenant browser and CRUD surface. Wires the RTK Query tenant list and
 * mutations to the stateless {@link TenantTable}, {@link TenantFormDialog}, and {@link ConfirmDialog}.
 * Owns only dialog/error state; all persistence, cache invalidation, and error shaping come from the
 * store layer. Selecting a tenant crosses into that tenant's security boundary.
 */
export function TenantsPage({ onOpenTenant }: TenantsPageProps) {
  const { data, isLoading, error } = useListTenantsQuery();
  const tenants = data?.items ?? [];

  const [createTenant, { isLoading: isCreating }] = useCreateTenantMutation();
  const [updateTenant, { isLoading: isUpdating }] = useUpdateTenantMutation();
  const [deleteTenant, { isLoading: isDeleting }] = useDeleteTenantMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (tenantId: string) => {
    const tenant = tenants.find((t) => t.tenantId === tenantId);
    if (!tenant) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', tenant });
  };
  const openDelete = (tenantId: string) => {
    const tenant = tenants.find((t) => t.tenantId === tenantId);
    if (!tenant) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', tenant });
  };

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: TenantFormValues =
    dialog.kind === 'edit'
      ? {
          displayName: dialog.tenant.displayName,
          domainName: dialog.tenant.domainName ?? '',
          isDisabled: dialog.tenant.isDisabled,
        }
      : emptyTenantFormValues;

  const handleSubmit = async (values: TenantFormValues) => {
    setFormError(undefined);
    const domainName = values.domainName.length > 0 ? values.domainName : null;
    try {
      if (dialog.kind === 'create') {
        await createTenant({
          domainName,
          displayName: values.displayName,
          isDisabled: values.isDisabled,
          settings: {},
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateTenant({
          tenantId: dialog.tenant.tenantId,
          concurrencyToken: dialog.tenant.concurrencyToken,
          fields: {
            domainName,
            displayName: values.displayName,
            isDisabled: values.isDisabled,
          },
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
      await deleteTenant({ tenantId: dialog.tenant.tenantId }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Typography variant="h5" component="h2">
          Tenants
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-tenant"
        >
          Create tenant
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load tenants.' : undefined}
        isEmpty={tenants.length === 0}
        emptyMessage="No tenants exist yet."
      >
        <TenantTable
          tenants={tenants}
          onSelect={onOpenTenant}
          onEdit={openEdit}
          onDelete={openDelete}
        />
      </DataStateBoundary>

      <TenantFormDialog
        open={formOpen}
        title={dialog.kind === 'edit' ? 'Edit tenant' : 'Create tenant'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Create'}
        initialValues={formInitial}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete tenant"
        message={
          dialog.kind === 'delete'
            ? `Delete “${dialog.tenant.displayName}”? This cannot be undone.`
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

import { useCallback, useEffect, useState } from 'react';
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
  isValidTenantId,
  type TenantAddressInput,
  type TenantFormValues,
} from '@ncode/ui';
import type { TenantResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateTenantMutation,
  useDeleteTenantMutation,
  useGetTenantAddressQuery,
  useListTenantsQuery,
  useUpdateTenantMutation,
  type ApiError,
} from '@ncode/store';

export type TenantsPageProps = {
  readonly onOpenTenant: (tenantId: string) => void;
  /**
   * How long the address must stay unchanged before it is previewed. Defaults to 300 ms so typing sends no burst of
   * requests; a test passes `0`.
   */
  readonly addressPreviewDelayMs?: number;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly tenant: TenantResource }
  | { readonly kind: 'delete'; readonly tenant: TenantResource };

const closed: DialogState = { kind: 'none' };

/** How long the address must stay unchanged before it is previewed, so typing sends no burst of requests. */
const defaultAddressPreviewDelayMs = 300;

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

/**
 * Container: the global-scope tenant browser and CRUD surface. Wires the RTK Query tenant list and
 * mutations to the stateless {@link TenantTable}, {@link TenantFormDialog}, and {@link ConfirmDialog}.
 * Owns only dialog/error state; all persistence, cache invalidation, and error shaping come from the
 * store layer. Selecting a tenant crosses into that tenant's security boundary.
 */
export function TenantsPage({
  onOpenTenant,
  addressPreviewDelayMs = defaultAddressPreviewDelayMs,
}: TenantsPageProps) {
  const { data, isLoading, error } = useListTenantsQuery();
  const tenants = data?.items ?? [];

  const [createTenant, { isLoading: isCreating }] = useCreateTenantMutation();
  const [updateTenant, { isLoading: isUpdating }] = useUpdateTenantMutation();
  const [deleteTenant, { isLoading: isDeleting }] = useDeleteTenantMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();
  const [addressInput, setAddressInput] = useState<TenantAddressInput>();
  const handleAddressChange = useCallback((address: TenantAddressInput) => {
    setAddressInput(address);
  }, []);

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
          tenantId: dialog.tenant.tenantId,
          displayName: dialog.tenant.displayName,
          issuer: dialog.tenant.issuer ?? '',
          mtlsEndpointAliasAuthority: dialog.tenant.mtlsEndpointAliasAuthority ?? '',
          isDisabled: dialog.tenant.isDisabled,
        }
      : emptyTenantFormValues;

  // The address is previewed against the server's own rules: for a new tenant always, and for an existing
  // one only when its issuer or mTLS endpoint alias changes, because the server judges an address only
  // when it can change.
  const debouncedAddress = useDebouncedValue(addressInput, addressPreviewDelayMs);
  const isAddressUnchanged =
    dialog.kind === 'edit' &&
    debouncedAddress !== undefined &&
    debouncedAddress.issuer === (dialog.tenant.issuer ?? '') &&
    debouncedAddress.mtlsEndpointAliasAuthority ===
      (dialog.tenant.mtlsEndpointAliasAuthority ?? '');
  const shouldPreview =
    formOpen &&
    debouncedAddress !== undefined &&
    isValidTenantId(debouncedAddress.tenantId) &&
    !isAddressUnchanged;
  const { data: preview, originalArgs: previewArgs } = useGetTenantAddressQuery(
    {
      tenantId: debouncedAddress?.tenantId ?? '',
      ...(debouncedAddress?.issuer ? { issuer: debouncedAddress.issuer } : {}),
      ...(debouncedAddress?.mtlsEndpointAliasAuthority
        ? { mtlsEndpointAliasAuthority: debouncedAddress.mtlsEndpointAliasAuthority }
        : {}),
    },
    { skip: !shouldPreview, refetchOnMountOrArgChange: true },
  );

  // A preview is shown only while it describes the address as it is entered now.
  const addressPreview =
    shouldPreview &&
    preview &&
    addressInput &&
    previewArgs?.tenantId === addressInput.tenantId &&
    (previewArgs.issuer ?? '') === addressInput.issuer &&
    (previewArgs.mtlsEndpointAliasAuthority ?? '') === addressInput.mtlsEndpointAliasAuthority
      ? preview
      : undefined;

  const handleSubmit = async (values: TenantFormValues) => {
    setFormError(undefined);
    const issuer = values.issuer.length > 0 ? values.issuer : null;
    const mtlsEndpointAliasAuthority =
      values.mtlsEndpointAliasAuthority.length > 0 ? values.mtlsEndpointAliasAuthority : null;
    try {
      if (dialog.kind === 'create') {
        await createTenant({
          tenantId: values.tenantId,
          issuer,
          mtlsEndpointAliasAuthority,
          displayName: values.displayName,
          isDisabled: values.isDisabled,
          settings: {},
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateTenant({
          tenantId: dialog.tenant.tenantId,
          concurrencyToken: dialog.tenant.concurrencyToken,
          fields: {
            issuer,
            mtlsEndpointAliasAuthority,
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
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
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
        isTenantIdLocked={dialog.kind === 'edit'}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        addressPreview={addressPreview}
        onAddressChange={handleAddressChange}
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

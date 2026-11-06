import { useCallback, useEffect, useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  TenantEnvironmentFilter,
  TenantFormDialog,
  TenantPromotionDialog,
  TenantTable,
  allTenantEnvironments,
  emptyTenantFormValues,
  isValidTenantId,
  matchesTenantEnvironment,
  tenantEnvironmentTypes,
  type TenantAddressInput,
  type TenantFormValues,
} from '@ncode/ui';
import type { PromoteTenantRequest, TenantPromotionResource, TenantResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateTenantMutation,
  useDeleteTenantMutation,
  useGetNewTenantAddressQuery,
  useGetTenantAddressQuery,
  useListTenantsQuery,
  usePreviewTenantPromotionMutation,
  usePromoteTenantMutation,
  useUpdateTenantMutation,
  type ApiError,
} from '@ncode/store';

export type TenantsPageProps = {
  /** The team whose tenants are listed and created. */
  readonly teamId: string;
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
  | { readonly kind: 'promote' }
  | { readonly kind: 'edit'; readonly tenant: TenantResource }
  | { readonly kind: 'delete'; readonly tenant: TenantResource };

const closed: DialogState = { kind: 'none' };

/** How long the address must stay unchanged before it is previewed, so typing sends no burst of requests. */
const defaultAddressPreviewDelayMs = 300;

/**
 * The message of a failed promotion request: the problem's detail, followed by each error a `409` problem lists in
 * its `errors` extension.
 */
function promotionErrorMessage(error: ApiError): string {
  const message = apiErrorMessage(error);
  const data = 'status' in error && error.status === 409 ? error.data : undefined;
  const errors =
    data && typeof data === 'object' ? (data as { errors?: unknown }).errors : undefined;
  if (!Array.isArray(errors)) {
    return message;
  }
  const lines = errors.flatMap((item: unknown) => {
    if (!item || typeof item !== 'object') {
      return [];
    }
    const { resourceType, resourceId, detail } = item as {
      resourceType?: unknown;
      resourceId?: unknown;
      detail?: unknown;
    };
    if (typeof detail !== 'string') {
      return [];
    }
    const resource = [resourceType, resourceId]
      .filter((part): part is string => typeof part === 'string' && part.length > 0)
      .join(' ');
    return [resource.length > 0 ? `${resource}: ${detail}` : detail];
  });
  return lines.length > 0 ? `${message} ${lines.join(' ')}` : message;
}

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

/**
 * Container: a team's tenant browser and CRUD surface. Wires the RTK Query tenant list and
 * mutations to the stateless {@link TenantTable}, {@link TenantEnvironmentFilter}, {@link TenantFormDialog},
 * and {@link ConfirmDialog}. Owns only dialog, filter, and error state; all persistence, cache invalidation, and error shaping come from the
 * store layer. Selecting a tenant crosses into that tenant's security boundary.
 */
export function TenantsPage({
  teamId,
  onOpenTenant,
  addressPreviewDelayMs = defaultAddressPreviewDelayMs,
}: TenantsPageProps) {
  const { data, isLoading, error } = useListTenantsQuery({ teamId });
  const tenants = data?.items ?? [];

  const [environmentFilter, setEnvironmentFilter] = useState(allTenantEnvironments);
  const environmentTypes = tenantEnvironmentTypes(tenants);
  // A filter whose type no listed tenant has any longer (its last tenant was deleted) shows every tenant.
  const activeEnvironment = environmentTypes.includes(environmentFilter)
    ? environmentFilter
    : allTenantEnvironments;
  const visibleTenants = tenants.filter((tenant) =>
    matchesTenantEnvironment(tenant, activeEnvironment),
  );

  const [createTenant, { isLoading: isCreating }] = useCreateTenantMutation();
  const [updateTenant, { isLoading: isUpdating }] = useUpdateTenantMutation();
  const [deleteTenant, { isLoading: isDeleting }] = useDeleteTenantMutation();
  const [previewPromotion, { isLoading: isPreviewingPromotion }] =
    usePreviewTenantPromotionMutation();
  const [promoteTenant, { isLoading: isPromoting }] = usePromoteTenantMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();
  const [promotionReport, setPromotionReport] = useState<TenantPromotionResource>();
  const [promotionError, setPromotionError] = useState<string>();
  const [addressInput, setAddressInput] = useState<TenantAddressInput>();
  const handleAddressChange = useCallback((address: TenantAddressInput) => {
    setAddressInput(address);
  }, []);

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openPromote = () => {
    setPromotionReport(undefined);
    setPromotionError(undefined);
    setDialog({ kind: 'promote' });
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

  // The address is previewed against the server's own rules: for a new tenant through its team's route,
  // always, and for an existing tenant through its own route, only when its issuer or mTLS endpoint alias
  // changes, because the server judges an address only when it can change.
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
  const previewArgsInput = {
    tenantId: debouncedAddress?.tenantId ?? '',
    ...(debouncedAddress?.issuer ? { issuer: debouncedAddress.issuer } : {}),
    ...(debouncedAddress?.mtlsEndpointAliasAuthority
      ? { mtlsEndpointAliasAuthority: debouncedAddress.mtlsEndpointAliasAuthority }
      : {}),
  };
  const newTenantPreview = useGetNewTenantAddressQuery(
    { teamId, ...previewArgsInput },
    { skip: dialog.kind !== 'create' || !shouldPreview, refetchOnMountOrArgChange: true },
  );
  const existingTenantPreview = useGetTenantAddressQuery(previewArgsInput, {
    skip: dialog.kind !== 'edit' || !shouldPreview,
    refetchOnMountOrArgChange: true,
  });
  const { data: preview, originalArgs: previewArgs } =
    dialog.kind === 'edit' ? existingTenantPreview : newTenantPreview;

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
          teamId,
          body: {
            tenantId: values.tenantId,
            issuer,
            mtlsEndpointAliasAuthority,
            displayName: values.displayName,
            isDisabled: values.isDisabled,
            settings: {},
          },
        }).unwrap();
      } else if (dialog.kind === 'edit') {
        await updateTenant({
          teamId,
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
      await deleteTenant({ teamId, tenantId: dialog.tenant.tenantId }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  const handlePreviewPromotion = async (body: PromoteTenantRequest) => {
    setPromotionError(undefined);
    try {
      setPromotionReport(await previewPromotion({ teamId, body }).unwrap());
    } catch (previewError) {
      setPromotionError(promotionErrorMessage(previewError as ApiError));
    }
  };

  const handlePromote = async (body: PromoteTenantRequest) => {
    setPromotionError(undefined);
    try {
      setPromotionReport(await promoteTenant({ teamId, body }).unwrap());
    } catch (promoteError) {
      setPromotionError(promotionErrorMessage(promoteError as ApiError));
    }
  };

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Typography variant="h5" component="h2">
          Tenants
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <TenantEnvironmentFilter
            types={environmentTypes}
            value={activeEnvironment}
            onChange={setEnvironmentFilter}
          />
          <Button
            variant="outlined"
            onClick={openPromote}
            disabled={tenants.length < 2}
            data-testid="promote-tenant"
          >
            Promote
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={openCreate}
            data-testid="create-tenant"
          >
            Create tenant
          </Button>
        </Box>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load tenants.' : undefined}
        isEmpty={tenants.length === 0}
        emptyMessage="No tenants exist yet."
      >
        <TenantTable
          tenants={visibleTenants}
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

      <TenantPromotionDialog
        open={dialog.kind === 'promote'}
        tenantIds={tenants.map((tenant) => tenant.tenantId)}
        report={promotionReport}
        isBusy={isPreviewingPromotion || isPromoting}
        error={promotionError}
        onPreview={handlePreviewPromotion}
        onPromote={handlePromote}
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

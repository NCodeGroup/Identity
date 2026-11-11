import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { ClientFormDialog, ClientTable, ConfirmDialog, DataStateBoundary } from '@ncode/ui';
import { useCreateClientMutation, useListClientsQuery } from '@ncode/store';
import { ClientDetailPage } from './ClientDetailPage.js';
import { useClientAdministration } from './useClientAdministration.js';

export type ClientsPageProps = {
  readonly tenantId: string;
  /**
   * The resource server whose clients the page administers: it lists only the clients that act for
   * it and creates clients bound to it. When omitted, the page administers every client in the
   * tenant.
   */
  readonly resourceServerId?: string;
  /** When set, the page renders inside a parent's detail view and omits its own heading. */
  readonly embedded?: boolean;
};

/**
 * Container: browses and administers the clients of a tenant. The tenant is passed down from the
 * stateful shell, so a tenant administrator view is always bound to its own tenant (the API path is
 * nested under the tenant — the boundary is structural, not a filter). Given a resource
 * server, it administers the clients that act for it — that resource server's credentials at the
 * token introspection endpoint. Wires the RTK Query client list and mutations to the stateless
 * {@link ClientTable}, {@link ClientFormDialog}, and {@link ConfirmDialog}; the dialog and error
 * state live in {@link useClientAdministration}.
 */
export function ClientsPage({ tenantId, resourceServerId, embedded = false }: ClientsPageProps) {
  const { data, isLoading, error } = useListClientsQuery(
    resourceServerId ? { tenantId, resourceServerId } : { tenantId },
  );
  const clients = data?.items ?? [];

  const [createClient] = useCreateClientMutation();
  const admin = useClientAdministration({
    clients,
    createClient: (values) =>
      createClient({
        tenantId,
        body: {
          isDisabled: values.isDisabled,
          ...(resourceServerId ? { resourceServerId } : {}),
          settings: {},
        },
      }).unwrap(),
  });
  if (admin.selected) {
    return <ClientDetailPage client={admin.selected} onBack={admin.clearSelection} />;
  }

  return (
    <>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: embedded ? 'flex-end' : 'space-between',
          mb: 1,
        }}
      >
        {!embedded && (
          <Typography variant="h5" component="h2">
            Clients
          </Typography>
        )}
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={admin.openCreate}
          data-testid="create-client"
        >
          Create client
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load clients.' : undefined}
        isEmpty={clients.length === 0}
        emptyMessage={
          resourceServerId
            ? 'No client acts for this resource server yet.'
            : 'No clients in this tenant yet.'
        }
      >
        <ClientTable
          clients={clients}
          onSelect={admin.select}
          onEdit={admin.openEdit}
          onDelete={admin.openDelete}
        />
      </DataStateBoundary>

      <ClientFormDialog
        open={admin.formOpen}
        title={admin.isEditing ? 'Edit client' : 'Create client'}
        submitLabel={admin.isEditing ? 'Save' : 'Create'}
        initialValues={admin.formInitial}
        isSubmitting={admin.isSubmitting}
        error={admin.formError}
        onSubmit={admin.handleSubmit}
        onCancel={admin.close}
      />

      <ConfirmDialog
        open={admin.deleteTarget !== undefined}
        title="Delete client"
        message={
          admin.deleteTarget
            ? `Delete client “${admin.deleteTarget.clientId}”? This cannot be undone.`
            : ''
        }
        isBusy={admin.isDeleting}
        error={admin.deleteError}
        onConfirm={admin.handleDelete}
        onCancel={admin.close}
      />
    </>
  );
}

import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { ClientFormDialog, ClientTable, ConfirmDialog, DataStateBoundary } from '@ncode/ui';
import type { MachineClientOwner } from '@ncode/api';
import { useCreateMachineClientMutation, useListMachineClientsQuery } from '@ncode/store';
import { ClientDetailPage } from './ClientDetailPage.js';
import { useClientAdministration } from './useClientAdministration.js';

export type MachineClientsPageProps = {
  readonly owner: MachineClientOwner;
};

/**
 * Container: browses and administers the machine clients the platform or a team owns (ADR-1466);
 * each lives in the control-plane tenant, so its edit, delete and detail go through that tenant's
 * client routes (ADR-1421). Wires the RTK Query machine-client list and the client mutations to the
 * stateless {@link ClientTable}, {@link ClientFormDialog}, and {@link ConfirmDialog}; the dialog and
 * error state live in {@link useClientAdministration}.
 */
export function MachineClientsPage({ owner }: MachineClientsPageProps) {
  const { data, isLoading, error } = useListMachineClientsQuery(owner);
  const clients = data?.items ?? [];

  const [createMachineClient] = useCreateMachineClientMutation();
  const admin = useClientAdministration({
    clients,
    createClient: (values) =>
      createMachineClient({
        owner,
        body: { isDisabled: values.isDisabled, settings: {} },
      }).unwrap(),
  });
  if (admin.selected) {
    return <ClientDetailPage client={admin.selected} onBack={admin.clearSelection} />;
  }

  const scope = owner.kind === 'platform' ? 'the platform' : 'this team';

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
          onClick={admin.openCreate}
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
          onSelect={admin.select}
          onEdit={admin.openEdit}
          onDelete={admin.openDelete}
        />
      </DataStateBoundary>

      <ClientFormDialog
        open={admin.formOpen}
        title={admin.isEditing ? 'Edit machine client' : 'Create machine client'}
        submitLabel={admin.isEditing ? 'Save' : 'Create'}
        initialValues={admin.formInitial}
        isSubmitting={admin.isSubmitting}
        error={admin.formError}
        onSubmit={admin.handleSubmit}
        onCancel={admin.close}
      />

      <ConfirmDialog
        open={admin.deleteTarget !== undefined}
        title="Delete machine client"
        message={
          admin.deleteTarget
            ? `Delete machine client “${admin.deleteTarget.clientId}”? This cannot be undone.`
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

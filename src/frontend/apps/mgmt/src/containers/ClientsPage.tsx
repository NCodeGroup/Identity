import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Typography from '@mui/material/Typography';
import { DataStateBoundary } from '@ncode/ui';
import { type ManagementScope } from '@ncode/api';
import { useListClientsQuery } from '@ncode/store';

export type ClientsPageProps = {
  readonly scope: ManagementScope;
};

/**
 * Container: browses the clients within a given management scope. The scope is passed down from the
 * stateful shell, so a tenant administrator view is always bound to its own tenant (the API path is
 * nested under the tenant — the boundary is structural, not a filter).
 */
export function ClientsPage({ scope }: ClientsPageProps) {
  const { data, isLoading, error } = useListClientsQuery({ scope });
  const clients = data?.items ?? [];

  return (
    <>
      <Typography variant="h5" component="h2" gutterBottom>
        Clients
      </Typography>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load clients.' : undefined}
        isEmpty={clients.length === 0}
        emptyMessage="No clients in this scope yet."
      >
        <List dense>
          {clients.map((client) => (
            <ListItem key={client.clientId} divider>
              <ListItemText
                primary={client.displayName}
                secondary={`${client.clientId}${client.isDisabled ? ' · disabled' : ''}`}
              />
            </ListItem>
          ))}
        </List>
      </DataStateBoundary>
    </>
  );
}

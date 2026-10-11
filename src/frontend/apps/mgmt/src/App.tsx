import { useState } from 'react';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import { GLOBAL_SCOPE, isGlobalScope, tenantScope, type ManagementScope } from '@ncode/api';
import { AppLayout } from './components/AppLayout.js';
import { TenantsPage } from './containers/TenantsPage.js';
import { ClientsPage } from './containers/ClientsPage.js';
import { ResourceServersPage } from './containers/ResourceServersPage.js';

type TenantSurface = 'clients' | 'resource-servers';

/**
 * Stateful shell. Owns the active {@link ManagementScope} (the global vs tenant security boundary)
 * and, within a tenant, the active surface (clients vs resource servers). All data fetching lives in
 * the child containers; this component only wires scope/surface state and navigation.
 */
export function App() {
  const [scope, setScope] = useState<ManagementScope>(GLOBAL_SCOPE);
  const [tenantSurface, setTenantSurface] = useState<TenantSurface>('clients');

  const openTenant = (tenantId: string) => {
    setTenantSurface('clients');
    setScope(tenantScope(tenantId));
  };

  return (
    <AppLayout
      title="Management"
      scopeKind={scope.kind}
      {...(scope.kind === 'tenant' ? { scopeLabel: scope.tenantId } : {})}
      {...(scope.kind === 'tenant' ? { onExitTenant: () => setScope(GLOBAL_SCOPE) } : {})}
    >
      {isGlobalScope(scope) ? (
        <TenantsPage onOpenTenant={openTenant} />
      ) : (
        <>
          <Tabs
            value={tenantSurface}
            onChange={(_event, value: TenantSurface) => setTenantSurface(value)}
            sx={{ mb: 2 }}
          >
            <Tab value="clients" label="Clients" data-testid="tenant-tab-clients" />
            <Tab
              value="resource-servers"
              label="Resource servers"
              data-testid="tenant-tab-resource-servers"
            />
          </Tabs>
          {tenantSurface === 'clients' ? (
            <ClientsPage scope={scope} />
          ) : (
            <ResourceServersPage scope={scope} />
          )}
        </>
      )}
    </AppLayout>
  );
}

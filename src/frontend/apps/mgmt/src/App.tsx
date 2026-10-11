import { useState } from 'react';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import { GLOBAL_SCOPE, tenantScope, type ManagementScope } from '@ncode/api';
import { AppLayout } from './components/AppLayout.js';
import { TenantsPage } from './containers/TenantsPage.js';
import { ClientsPage } from './containers/ClientsPage.js';
import { ResourceServersPage } from './containers/ResourceServersPage.js';
import { GrantsPage } from './containers/GrantsPage.js';

type TenantSurface = 'clients' | 'resource-servers' | 'grants';

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
      {scope.kind === 'global' ? (
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
            <Tab value="grants" label="Grants" data-testid="tenant-tab-grants" />
          </Tabs>
          {tenantSurface === 'clients' ? (
            <ClientsPage scope={scope} />
          ) : tenantSurface === 'resource-servers' ? (
            <ResourceServersPage scope={scope} />
          ) : (
            <GrantsPage tenantId={scope.tenantId} />
          )}
        </>
      )}
    </AppLayout>
  );
}

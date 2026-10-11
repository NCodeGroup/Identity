import { useState } from 'react';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import { GLOBAL_SCOPE, tenantScope, type ManagementScope } from '@ncode/api';
import { AppLayout } from './components/AppLayout.js';
import { TenantsPage } from './containers/TenantsPage.js';
import { ServersPage } from './containers/ServersPage.js';
import { ClientsPage } from './containers/ClientsPage.js';
import { ResourceServersPage } from './containers/ResourceServersPage.js';
import { GrantsPage } from './containers/GrantsPage.js';
import { TenantDetailPage } from './containers/TenantDetailPage.js';

type GlobalSurface = 'tenants' | 'servers';
type TenantSurface = 'clients' | 'resource-servers' | 'grants' | 'tenant';

/**
 * Stateful shell. Owns the active {@link ManagementScope} (the global vs tenant security boundary) and,
 * within each boundary, the active surface. In the global scope that is tenants vs the server (a
 * system-level settings/secret owner); within a tenant it is clients, resource servers, grants, or the
 * tenant's own secrets/settings. All data fetching lives in the child containers; this component only
 * wires scope/surface state and navigation.
 */
export function App() {
  const [scope, setScope] = useState<ManagementScope>(GLOBAL_SCOPE);
  const [globalSurface, setGlobalSurface] = useState<GlobalSurface>('tenants');
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
        <>
          <Tabs
            value={globalSurface}
            onChange={(_event, value: GlobalSurface) => setGlobalSurface(value)}
            sx={{ mb: 2 }}
          >
            <Tab value="tenants" label="Tenants" data-testid="global-tab-tenants" />
            <Tab value="servers" label="Servers" data-testid="global-tab-servers" />
          </Tabs>
          {globalSurface === 'tenants' ? (
            <TenantsPage onOpenTenant={openTenant} />
          ) : (
            <ServersPage />
          )}
        </>
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
            <Tab value="tenant" label="Tenant" data-testid="tenant-tab-tenant" />
          </Tabs>
          {tenantSurface === 'clients' ? (
            <ClientsPage scope={scope} />
          ) : tenantSurface === 'resource-servers' ? (
            <ResourceServersPage scope={scope} />
          ) : tenantSurface === 'grants' ? (
            <GrantsPage tenantId={scope.tenantId} />
          ) : (
            <TenantDetailPage tenantId={scope.tenantId} />
          )}
        </>
      )}
    </AppLayout>
  );
}

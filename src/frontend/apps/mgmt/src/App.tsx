import { useState } from 'react';
import { GLOBAL_SCOPE, isGlobalScope, tenantScope, type ManagementScope } from '@ncode/api';
import { AppLayout } from './components/AppLayout.js';
import { TenantsPage } from './containers/TenantsPage.js';
import { ClientsPage } from './containers/ClientsPage.js';

/**
 * Stateful shell. Owns the active {@link ManagementScope} (the global vs tenant security boundary)
 * and switches the rendered surface accordingly. All data fetching lives in the child containers;
 * this component only wires scope state and navigation.
 */
export function App() {
  const [scope, setScope] = useState<ManagementScope>(GLOBAL_SCOPE);

  return (
    <AppLayout
      title="Management"
      scopeKind={scope.kind}
      {...(scope.kind === 'tenant' ? { scopeLabel: scope.tenantId } : {})}
      {...(scope.kind === 'tenant' ? { onExitTenant: () => setScope(GLOBAL_SCOPE) } : {})}
    >
      {isGlobalScope(scope) ? (
        <TenantsPage onOpenTenant={(tenantId) => setScope(tenantScope(tenantId))} />
      ) : (
        <ClientsPage scope={scope} />
      )}
    </AppLayout>
  );
}

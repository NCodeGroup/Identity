import { Fragment, lazy, Suspense } from 'react';
import CircularProgress from '@mui/material/CircularProgress';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import { signedOut, useAppDispatch, useAppSelector } from '@ncode/store';
import { AppLayout } from './components/AppLayout.js';
import { endSessionUrlFromRuntime } from './auth.js';
import { useConsoleRoute, navigateTo } from './navigation.js';
import {
  globalRoute,
  routeScope,
  tenantRoute,
  type GlobalSurface,
  type TenantSurface,
} from './routes.js';

// Each surface's container is its own chunk, loaded when the surface is first visited (ADR-0175).
const TenantsPage = lazy(() =>
  import('./containers/TenantsPage.js').then((m) => ({ default: m.TenantsPage })),
);
const ServersPage = lazy(() =>
  import('./containers/ServersPage.js').then((m) => ({ default: m.ServersPage })),
);
const ClientsPage = lazy(() =>
  import('./containers/ClientsPage.js').then((m) => ({ default: m.ClientsPage })),
);
const ResourceServersPage = lazy(() =>
  import('./containers/ResourceServersPage.js').then((m) => ({ default: m.ResourceServersPage })),
);
const GrantsPage = lazy(() =>
  import('./containers/GrantsPage.js').then((m) => ({ default: m.GrantsPage })),
);
const TenantDetailPage = lazy(() =>
  import('./containers/TenantDetailPage.js').then((m) => ({ default: m.TenantDetailPage })),
);

const surfaceFallback = <CircularProgress aria-label="Loading" data-testid="surface-loading" />;

/**
 * Stateful shell. The address bar owns the navigation state (ADR-0174): the route names the active
 * management scope (the global vs tenant security boundary) and, within it, the active surface. In the
 * global scope that is tenants vs the server (a system-level settings/secret owner); within a tenant it
 * is clients, resource servers, grants, or the tenant's own secrets/settings. All data fetching lives in
 * the child containers; this component only maps the route to a surface and tab changes to navigation.
 */
export function App() {
  const dispatch = useAppDispatch();
  const runtime = useAppSelector((state) => state.config.runtime);
  const subject = useAppSelector((state) => state.auth.subject);
  const idToken = useAppSelector((state) => state.auth.idToken);
  const isSignedIn = useAppSelector((state) => state.auth.accessToken !== null);

  const route = useConsoleRoute();
  const scope = routeScope(route);

  const openTenant = (tenantId: string) => navigateTo(tenantRoute(tenantId));

  // Clear local tokens, then hand off to the OP's RP-Initiated Logout so the server session and its
  // participating relying parties are also ended; when no end_session_endpoint is advertised, clearing
  // local state alone returns the app to the sign-in screen.
  const handleSignOut = () => {
    const endSessionUrl = runtime ? endSessionUrlFromRuntime(runtime, idToken) : null;
    dispatch(signedOut());
    if (endSessionUrl) {
      window.location.assign(endSessionUrl);
    }
  };

  return (
    <AppLayout
      title="Management"
      scopeKind={scope.kind}
      {...(scope.kind === 'tenant' ? { scopeLabel: scope.tenantId } : {})}
      {...(scope.kind === 'tenant'
        ? { onExitTenant: () => navigateTo(globalRoute('tenants')) }
        : {})}
      {...(isSignedIn && subject ? { subject } : {})}
      {...(isSignedIn ? { onSignOut: handleSignOut } : {})}
    >
      {route.kind === 'global' ? (
        <>
          <Tabs
            value={route.surface}
            onChange={(_event, value: GlobalSurface) => navigateTo(globalRoute(value))}
            sx={{ mb: 2 }}
          >
            <Tab value="tenants" label="Tenants" data-testid="global-tab-tenants" />
            <Tab value="servers" label="Servers" data-testid="global-tab-servers" />
          </Tabs>
          <Suspense fallback={surfaceFallback}>
            {route.surface === 'tenants' ? (
              <TenantsPage onOpenTenant={openTenant} />
            ) : (
              <ServersPage />
            )}
          </Suspense>
        </>
      ) : (
        // Keyed by tenant: moving between tenants (e.g. back/forward) remounts the surface, so no per-tenant UI
        // state such as an open detail view leaks across the boundary.
        <Fragment key={route.tenantId}>
          <Tabs
            value={route.surface}
            onChange={(_event, value: TenantSurface) =>
              navigateTo(tenantRoute(route.tenantId, value))
            }
            sx={{ mb: 2 }}
          >
            <Tab value="clients" label="Clients" data-testid="tenant-tab-clients" />
            <Tab
              value="resource-servers"
              label="Resource servers"
              data-testid="tenant-tab-resource-servers"
            />
            <Tab value="grants" label="Grants" data-testid="tenant-tab-grants" />
            <Tab value="settings" label="Settings" data-testid="tenant-tab-settings" />
          </Tabs>
          <Suspense fallback={surfaceFallback}>
            {route.surface === 'clients' ? (
              <ClientsPage scope={scope} />
            ) : route.surface === 'resource-servers' ? (
              <ResourceServersPage scope={scope} />
            ) : route.surface === 'grants' ? (
              <GrantsPage tenantId={route.tenantId} />
            ) : (
              <TenantDetailPage tenantId={route.tenantId} />
            )}
          </Suspense>
        </Fragment>
      )}
    </AppLayout>
  );
}

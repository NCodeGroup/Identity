import { Fragment, lazy, Suspense } from 'react';
import CircularProgress from '@mui/material/CircularProgress';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import { signedOut, useAppDispatch, useAppSelector, useGetTenantQuery } from '@ncode/store';
import { AppLayout } from './components/AppLayout.js';
import { endSessionUrlFromRuntime } from './auth.js';
import { useConsoleRoute, navigateTo } from './navigation.js';
import {
  platformRoute,
  routeScope,
  teamRoute,
  tenantRoute,
  type PlatformSurface,
  type TeamSurface,
  type TenantSurface,
} from './routes.js';

// Each surface's container is its own chunk, loaded when the surface is first visited (ADR-1175).
const TeamsPage = lazy(() =>
  import('./containers/TeamsPage.js').then((m) => ({ default: m.TeamsPage })),
);
const TeamDetailPage = lazy(() =>
  import('./containers/TeamDetailPage.js').then((m) => ({ default: m.TeamDetailPage })),
);
const PlatformSettingsPage = lazy(() =>
  import('./containers/PlatformSettingsPage.js').then((m) => ({
    default: m.PlatformSettingsPage,
  })),
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
 * Stateful shell. The address bar owns the navigation state (ADR-1174): the route names the active
 * management scope (the platform, team or tenant security boundary) and, within it, the active surface. In the
 * platform scope that is the teams or the platform's settings; within a team it is the team's tenants, settings
 * or usage; within a tenant it is clients, resource servers, grants, or one of the tenant's own surfaces:
 * secrets, settings, or usage (ADR-1255). All data fetching lives in the child containers; this component only maps the route to a
 * surface and tab changes to navigation.
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
  const openTeam = (teamId: string) => navigateTo(teamRoute(teamId));

  // A tenant's enclosing scope is its team; until the tenant loads, the exit returns to the platform.
  const tenantId = route.kind === 'tenant' ? route.tenantId : undefined;
  const { data: tenant } = useGetTenantQuery(tenantId ?? '', { skip: tenantId === undefined });

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
      {...(scope.kind === 'team' ? { scopeLabel: scope.teamId } : {})}
      {...(scope.kind === 'tenant' ? { scopeLabel: scope.tenantId } : {})}
      {...(route.kind === 'team'
        ? { onExitScope: () => navigateTo(platformRoute('teams')), exitLabel: 'Platform' }
        : {})}
      {...(route.kind === 'tenant'
        ? {
            onExitScope: () =>
              navigateTo(tenant ? teamRoute(tenant.teamId, 'tenants') : platformRoute('teams')),
            exitLabel: tenant ? 'Team' : 'Platform',
          }
        : {})}
      {...(isSignedIn && subject ? { subject } : {})}
      {...(isSignedIn ? { onSignOut: handleSignOut } : {})}
    >
      {route.kind === 'platform' ? (
        <>
          <Tabs
            value={route.surface}
            onChange={(_event, value: PlatformSurface) => navigateTo(platformRoute(value))}
            sx={{ mb: 2 }}
          >
            <Tab value="teams" label="Teams" data-testid="platform-tab-teams" />
            <Tab value="settings" label="Settings" data-testid="platform-tab-settings" />
          </Tabs>
          <Suspense fallback={surfaceFallback}>
            {route.surface === 'teams' ? (
              <TeamsPage onOpenTeam={openTeam} />
            ) : (
              <PlatformSettingsPage />
            )}
          </Suspense>
        </>
      ) : route.kind === 'team' ? (
        // Keyed by team, like a tenant: no per-team UI state leaks across the boundary.
        <Fragment key={route.teamId}>
          <Tabs
            value={route.surface}
            onChange={(_event, value: TeamSurface) => navigateTo(teamRoute(route.teamId, value))}
            sx={{ mb: 2 }}
          >
            <Tab value="tenants" label="Tenants" data-testid="team-tab-tenants" />
            <Tab value="settings" label="Settings" data-testid="team-tab-settings" />
            <Tab value="usage" label="Usage" data-testid="team-tab-usage" />
          </Tabs>
          <Suspense fallback={surfaceFallback}>
            <TeamDetailPage
              teamId={route.teamId}
              surface={route.surface}
              onOpenTenant={openTenant}
            />
          </Suspense>
        </Fragment>
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
            <Tab value="secrets" label="Secrets" data-testid="tenant-tab-secrets" />
            <Tab value="settings" label="Settings" data-testid="tenant-tab-settings" />
            <Tab value="usage" label="Usage" data-testid="tenant-tab-usage" />
          </Tabs>
          <Suspense fallback={surfaceFallback}>
            {route.surface === 'clients' ? (
              <ClientsPage scope={scope} />
            ) : route.surface === 'resource-servers' ? (
              <ResourceServersPage scope={scope} />
            ) : route.surface === 'grants' ? (
              <GrantsPage tenantId={route.tenantId} />
            ) : (
              <TenantDetailPage tenantId={route.tenantId} surface={route.surface} />
            )}
          </Suspense>
        </Fragment>
      )}
    </AppLayout>
  );
}

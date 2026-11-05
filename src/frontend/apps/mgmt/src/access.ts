import type { CallerResource, TeamResource, TenantResource } from '@ncode/api';
import { HOME_ROUTE, platformRoute, teamRoute, tenantRoute, type ConsoleRoute } from './routes.js';

/**
 * Where the signed-in caller holds authority (ADR-1420): whether it administers the platform, and the teams and
 * tenants at which it holds a role assignment. Authority stays per resource; the console only uses this to decide
 * where to send the caller and what its back controls lead to.
 *
 * This module is UI-free so the access rules are unit-tested.
 */
export type CallerAccess = {
  readonly isPlatformAdministrator: boolean;
  readonly teamIds: readonly string[];
  readonly tenantIds: readonly string[];
};

/** A back control's destination and its label. */
export type ScopeExit = {
  readonly route: ConsoleRoute;
  readonly label: string;
};

/** Combines the caller's own resource with the teams and tenants it holds a role at. */
export function toCallerAccess(
  caller: CallerResource,
  teams: readonly TeamResource[],
  tenants: readonly TenantResource[],
): CallerAccess {
  return {
    isPlatformAdministrator: caller.isPlatformAdministrator,
    teamIds: teams.map((team) => team.teamId),
    tenantIds: tenants.map((tenant) => tenant.tenantId),
  };
}

/**
 * The one place the caller can go, or `null` when it has none or more than one. The destinations are the platform's
 * team list for a platform administrator, each team and each tenant the caller holds a role at.
 */
export function soleDestination(access: CallerAccess): ConsoleRoute | null {
  const destinations: ConsoleRoute[] = [
    ...(access.isPlatformAdministrator ? [platformRoute('teams')] : []),
    ...access.teamIds.map((teamId) => teamRoute(teamId)),
    ...access.tenantIds.map((tenantId) => tenantRoute(tenantId)),
  ];
  return destinations.length === 1 ? (destinations[0] ?? null) : null;
}

/**
 * Where a team's back control leads: the platform for a platform administrator, or while the caller's access is
 * unknown; otherwise the caller's home.
 */
export function teamExit(access: CallerAccess | undefined): ScopeExit {
  if (access === undefined || access.isPlatformAdministrator) {
    return { route: platformRoute('teams'), label: 'Platform' };
  }
  return { route: HOME_ROUTE, label: 'Home' };
}

/**
 * Where a tenant's back control leads: its team when the caller's access is unknown, the caller administers the
 * platform or holds a role at that team; otherwise the same place a team's back control leads.
 */
export function tenantExit(
  access: CallerAccess | undefined,
  teamId: string | undefined,
): ScopeExit {
  if (
    teamId !== undefined &&
    (access === undefined || access.isPlatformAdministrator || access.teamIds.includes(teamId))
  ) {
    return { route: teamRoute(teamId, 'tenants'), label: 'Team' };
  }
  return teamExit(access);
}

/**
 * The route the caller may open: a platform route becomes the caller's home once its access is known and it lacks
 * platform authority; every other route is kept.
 */
export function permittedRoute(
  route: ConsoleRoute,
  access: CallerAccess | undefined,
): ConsoleRoute {
  if (access !== undefined && !access.isPlatformAdministrator && route.kind === 'platform') {
    return HOME_ROUTE;
  }
  return route;
}

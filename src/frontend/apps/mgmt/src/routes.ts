import { GLOBAL_SCOPE, tenantScope, type ManagementScope } from '@ncode/api';

/**
 * The console's URL is its navigation state (ADR-0174). The path names the security boundary first and the surface
 * within it second, so the global ↔ tenant boundary (ADR-0064) stays explicit in every address:
 *
 * - `/tenants`, `/servers` — the global scope's surfaces;
 * - `/tenants/{tenantId}/{surface}` — a tenant scope's surfaces, the tenant id percent-encoded as one segment.
 *
 * This module is UI-free so the route grammar is unit-tested; `navigation.ts` binds it to the browser history.
 */

/** The surfaces of the global scope, in tab order. */
export const GLOBAL_SURFACES = ['tenants', 'servers'] as const;

/** The surfaces of a tenant scope, in tab order. */
export const TENANT_SURFACES = ['clients', 'resource-servers', 'grants', 'settings'] as const;

export type GlobalSurface = (typeof GLOBAL_SURFACES)[number];
export type TenantSurface = (typeof TENANT_SURFACES)[number];

export type ConsoleRoute =
  | { readonly kind: 'global'; readonly surface: GlobalSurface }
  | { readonly kind: 'tenant'; readonly tenantId: string; readonly surface: TenantSurface };

/** Where the console lands when the path names no route: the global tenant list. */
export const DEFAULT_ROUTE: ConsoleRoute = { kind: 'global', surface: 'tenants' };

/** The surface a tenant opens on when the path names the tenant but no surface. */
export const DEFAULT_TENANT_SURFACE: TenantSurface = 'clients';

/** Builds the route of a global surface. */
export function globalRoute(surface: GlobalSurface): ConsoleRoute {
  return { kind: 'global', surface };
}

/** Builds the route of a tenant surface, rejecting an empty/blank tenant id. */
export function tenantRoute(
  tenantId: string,
  surface: TenantSurface = DEFAULT_TENANT_SURFACE,
): ConsoleRoute {
  tenantScope(tenantId);
  return { kind: 'tenant', tenantId, surface };
}

/** The management scope a route addresses — derived from the route, never held separately. */
export function routeScope(route: ConsoleRoute): ManagementScope {
  return route.kind === 'global' ? GLOBAL_SCOPE : tenantScope(route.tenantId);
}

/** Formats a route as its canonical path. */
export function routePath(route: ConsoleRoute): string {
  return route.kind === 'global'
    ? `/${route.surface}`
    : `/tenants/${encodeURIComponent(route.tenantId)}/${route.surface}`;
}

function isGlobalSurface(value: string): value is GlobalSurface {
  return (GLOBAL_SURFACES as readonly string[]).includes(value);
}

function isTenantSurface(value: string): value is TenantSurface {
  return (TENANT_SURFACES as readonly string[]).includes(value);
}

function decodeSegment(segment: string): string | null {
  try {
    return decodeURIComponent(segment);
  } catch {
    return null;
  }
}

/**
 * Parses a path into the route it names, or `null` when it names none. A single trailing slash is tolerated, and a
 * tenant path without a surface names the tenant's default surface.
 */
export function parseRoute(pathname: string): ConsoleRoute | null {
  if (!pathname.startsWith('/')) {
    return null;
  }
  const trimmed = pathname.length > 1 && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  const segments = trimmed.slice(1).split('/');

  if (segments.length === 1) {
    const [surface = ''] = segments;
    return isGlobalSurface(surface) ? globalRoute(surface) : null;
  }

  const [root, encodedTenantId = '', surface = DEFAULT_TENANT_SURFACE, ...rest] = segments;
  if (root !== 'tenants' || rest.length > 0 || !isTenantSurface(surface)) {
    return null;
  }
  const tenantId = decodeSegment(encodedTenantId);
  if (tenantId === null || tenantId.trim().length === 0) {
    return null;
  }
  return { kind: 'tenant', tenantId, surface };
}

/**
 * Resolves any path to the route the console shows for it and that route's canonical path: a path naming no route
 * resolves to {@link DEFAULT_ROUTE}. The canonical path is always built by {@link routePath}, so it is a same-origin
 * console path whatever the input — safe to restore after a redirect round trip.
 */
export function resolveRoute(pathname: string): {
  readonly route: ConsoleRoute;
  readonly path: string;
} {
  const route = parseRoute(pathname) ?? DEFAULT_ROUTE;
  return { route, path: routePath(route) };
}

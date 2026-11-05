/**
 * The Management API sits on three distinct security boundaries:
 *
 * - the **platform** scope (`api/platform/...`, `api/teams`) — administration of the whole platform,
 * - a **team** scope (`api/teams/{teamId}/...`) — administration of one team and its tenants, and
 * - a **tenant** scope (`api/tenants/{tenantId}/...`) — administration of one tenant.
 *
 * Treating these as one list with a filter hides the boundary; the UI models them as distinct
 * browsing contexts (see docs/decisions/1064). This module is UI-free so the boundary rules are unit-tested.
 */

export type ManagementScope =
  | { readonly kind: 'platform' }
  | { readonly kind: 'team'; readonly teamId: string }
  | { readonly kind: 'tenant'; readonly tenantId: string };

export const PLATFORM_SCOPE: ManagementScope = { kind: 'platform' };

/** Builds a team scope, rejecting an empty/blank team id. */
export function teamScope(teamId: string): ManagementScope {
  if (teamId.trim().length === 0) {
    throw new Error('teamId must be a non-empty string.');
  }
  return { kind: 'team', teamId };
}

/** Builds a tenant scope, rejecting an empty/blank tenant id. */
export function tenantScope(tenantId: string): ManagementScope {
  if (tenantId.trim().length === 0) {
    throw new Error('tenantId must be a non-empty string.');
  }
  return { kind: 'tenant', tenantId };
}

/** A stable, human-readable key for a scope (cache tags use {@link scopeTagId}; console routes are in apps/mgmt). */
export function scopeKey(scope: ManagementScope): string {
  switch (scope.kind) {
    case 'platform':
      return 'platform';
    case 'team':
      return `team:${scope.teamId}`;
    case 'tenant':
      return `tenant:${scope.tenantId}`;
  }
}

/** True when a scope grants platform-wide administrative reach. */
export function isPlatformScope(scope: ManagementScope): scope is { kind: 'platform' } {
  return scope.kind === 'platform';
}

/**
 * Builds the API path prefix for a scope. Platform resources live under `api`; team and tenant
 * resources are always nested under their team or tenant so an administrator can never address
 * another team or tenant.
 */
export function scopeApiPrefix(scope: ManagementScope): string {
  switch (scope.kind) {
    case 'platform':
      return 'api';
    case 'team':
      return `api/teams/${encodeURIComponent(scope.teamId)}`;
    case 'tenant':
      return `api/tenants/${encodeURIComponent(scope.tenantId)}`;
  }
}

/** The cache-tag id of a scope's collection of one resource type. */
export const LIST_TAG = 'LIST';

/**
 * Builds a cache-tag id that names a resource within its scope (for example `platform:LIST`,
 * `team:core:LIST`, `tenant:acme:LIST`, or `tenant:acme:client:web`), so a mutation in one scope never
 * invalidates another scope's caches. The team or tenant id is URI-encoded, so it can never contain the
 * `:` separator and two scopes can never produce the same id.
 */
export function scopeTagId(scope: ManagementScope, id: string): string {
  return `${scopeTagPrefix(scope)}:${id}`;
}

function scopeTagPrefix(scope: ManagementScope): string {
  switch (scope.kind) {
    case 'platform':
      return 'platform';
    case 'team':
      return `team:${encodeURIComponent(scope.teamId)}`;
    case 'tenant':
      return `tenant:${encodeURIComponent(scope.tenantId)}`;
  }
}

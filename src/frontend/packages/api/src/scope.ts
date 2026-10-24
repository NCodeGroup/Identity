/**
 * The Management API sits on two distinct security boundaries:
 *
 * - the **global** scope  (`api/servers/...`)  — system-wide administration, and
 * - the **tenant** scope   (`api/tenants/{tenantId}/...`) — per-tenant administration.
 *
 * Treating these as one list with a filter hides the boundary; the UI models them as distinct
 * browsing contexts (see docs/adr/0064). This module is UI-free so the boundary rules are unit-tested.
 */

export type ManagementScope =
  { readonly kind: 'global' } | { readonly kind: 'tenant'; readonly tenantId: string };

export const GLOBAL_SCOPE: ManagementScope = { kind: 'global' };

/** Builds a tenant scope, rejecting an empty/blank tenant id. */
export function tenantScope(tenantId: string): ManagementScope {
  if (tenantId.trim().length === 0) {
    throw new Error('tenantId must be a non-empty string.');
  }
  return { kind: 'tenant', tenantId };
}

/** A stable, human-readable key for a scope (cache tags use {@link scopeTagId}; console routes are in apps/mgmt). */
export function scopeKey(scope: ManagementScope): string {
  return scope.kind === 'global' ? 'global' : `tenant:${scope.tenantId}`;
}

/** True when a scope grants system-wide (cross-tenant) administrative reach. */
export function isGlobalScope(scope: ManagementScope): scope is { kind: 'global' } {
  return scope.kind === 'global';
}

/**
 * Builds the API path prefix for a scope. Global resources live under `api`; tenant resources are
 * always nested under their tenant so a tenant administrator can never address another tenant.
 */
export function scopeApiPrefix(scope: ManagementScope): string {
  return scope.kind === 'global' ? 'api' : `api/tenants/${encodeURIComponent(scope.tenantId)}`;
}

/** The cache-tag id of a scope's collection of one resource type. */
export const LIST_TAG = 'LIST';

/**
 * Builds a cache-tag id that names a resource within its scope (for example `global:LIST`,
 * `tenant:acme:LIST`, or `tenant:acme:client:web`), so a mutation in one scope never invalidates
 * another scope's caches. The tenant id is URI-encoded, so it can never contain the `:` separator and
 * two scopes can never produce the same id.
 */
export function scopeTagId(scope: ManagementScope, id: string): string {
  const prefix =
    scope.kind === 'global' ? 'global' : `tenant:${encodeURIComponent(scope.tenantId)}`;
  return `${prefix}:${id}`;
}

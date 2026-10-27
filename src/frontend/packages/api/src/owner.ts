/**
 * The owner of a collection of secrets or of a settings document: a server, a tenant, or a client or
 * resource server inside a scope (see docs/decisions/0257). Secrets and settings are managed the same way for every owner,
 * so the UI and the store address them through one owner value instead of one endpoint per owner. This module is
 * UI-free so the addressing and cache-tag rules are unit-tested.
 */

import { GLOBAL_SCOPE, scopeApiPrefix, scopeTagId, type ManagementScope } from './scope.js';

export type ManagementOwner =
  | { readonly kind: 'server'; readonly serverId: string }
  | { readonly kind: 'tenant'; readonly tenantId: string }
  | { readonly kind: 'client'; readonly scope: ManagementScope; readonly clientId: string }
  | {
      readonly kind: 'resource-server';
      readonly scope: ManagementScope;
      readonly resourceServerId: string;
    };

/** The client variant of {@link ManagementOwner}; only a client's secrets rotate in one call. */
export type ClientOwner = Extract<ManagementOwner, { readonly kind: 'client' }>;

/** The scope an owner is administered in: servers are global, a tenant is its own scope. */
export function ownerScope(owner: ManagementOwner): ManagementScope {
  switch (owner.kind) {
    case 'server':
      return GLOBAL_SCOPE;
    case 'tenant':
      return { kind: 'tenant', tenantId: owner.tenantId };
    case 'client':
    case 'resource-server':
      return owner.scope;
  }
}

/** Builds the API path of an owner (every id is URI-encoded), under which `secrets` and `settings` live. */
export function ownerApiPath(owner: ManagementOwner): string {
  switch (owner.kind) {
    case 'server':
      return `api/servers/${encodeURIComponent(owner.serverId)}`;
    case 'tenant':
      return `api/tenants/${encodeURIComponent(owner.tenantId)}`;
    case 'client':
      return `${scopeApiPrefix(owner.scope)}/clients/${encodeURIComponent(owner.clientId)}`;
    case 'resource-server':
      return `${scopeApiPrefix(owner.scope)}/resource-servers/${encodeURIComponent(owner.resourceServerId)}`;
  }
}

/** A key for an owner within its scope; the id is URI-encoded so it can never contain the `:` separator. */
function ownerKey(owner: ManagementOwner): string {
  switch (owner.kind) {
    case 'server':
      return `server:${encodeURIComponent(owner.serverId)}`;
    case 'tenant':
      return `tenant:${encodeURIComponent(owner.tenantId)}`;
    case 'client':
      return `client:${encodeURIComponent(owner.clientId)}`;
    case 'resource-server':
      return `resource-server:${encodeURIComponent(owner.resourceServerId)}`;
  }
}

/** The cache-tag id of an owner's secrets and settings within the owner's scope. */
export function ownerTagId(owner: ManagementOwner): string {
  return scopeTagId(ownerScope(owner), ownerKey(owner));
}

/**
 * The cache-tag id of an owner's effective settings. The owner's id is encoded inside its key, so
 * this never equals {@link ownerTagId} of another owner of the same kind.
 */
export function ownerEffectiveTagId(owner: ManagementOwner): string {
  return scopeTagId(ownerScope(owner), `${ownerKey(owner)}:effective`);
}

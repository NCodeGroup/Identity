/**
 * The owner of a collection of secrets or of a settings document: the platform, a team, a tenant, or a client or
 * resource server inside a scope (see docs/decisions/1257). Secrets and settings are managed the same way for every owner,
 * so the UI and the store address them through one owner value instead of one endpoint per owner. This module is
 * UI-free so the addressing and cache-tag rules are unit-tested.
 */

import {
  PLATFORM_SCOPE,
  scopeApiPrefix,
  scopeTagId,
  teamScope,
  type ManagementScope,
} from './scope.js';

export type ManagementOwner =
  | { readonly kind: 'platform' }
  | { readonly kind: 'team'; readonly teamId: string }
  | { readonly kind: 'tenant'; readonly tenantId: string }
  | { readonly kind: 'client'; readonly scope: ManagementScope; readonly clientId: string }
  | {
      readonly kind: 'resource-server';
      readonly scope: ManagementScope;
      readonly resourceServerId: string;
    };

/** The client variant of {@link ManagementOwner}; only a client's secrets rotate in one call. */
export type ClientOwner = Extract<ManagementOwner, { readonly kind: 'client' }>;

/** The owners that hold secrets; the platform and a team hold settings only. */
export type SecretOwner = Exclude<
  ManagementOwner,
  { readonly kind: 'platform' } | { readonly kind: 'team' }
>;

/** The owners that have master keys to report and rekey: the platform or a tenant. */
export type EncryptionKeyOwner = Extract<ManagementOwner, { readonly kind: 'platform' | 'tenant' }>;

/** The scope an owner is administered in: the platform, a team and a tenant are each their own scope. */
export function ownerScope(owner: ManagementOwner): ManagementScope {
  switch (owner.kind) {
    case 'platform':
      return PLATFORM_SCOPE;
    case 'team':
      return teamScope(owner.teamId);
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
    case 'platform':
      return 'api/platform';
    case 'team':
      return `api/teams/${encodeURIComponent(owner.teamId)}`;
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
    case 'platform':
      return 'platform';
    case 'team':
      return `team:${encodeURIComponent(owner.teamId)}`;
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

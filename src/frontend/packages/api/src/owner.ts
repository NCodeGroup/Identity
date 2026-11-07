/**
 * The owner of a collection of secrets or of a settings document: the platform, a team, a tenant, or a client or
 * resource server inside a tenant (see docs/decisions/1257). Secrets and settings are managed the same way for every owner,
 * so the UI and the store address them through one owner value instead of one endpoint per owner. This module is
 * UI-free so the addressing and cache-tag rules are unit-tested.
 */

import { PLATFORM_SCOPE, scopeTagId, teamScope, type ManagementScope } from './scope.js';

export type ManagementOwner =
  | { readonly kind: 'platform' }
  | { readonly kind: 'team'; readonly teamId: string }
  | { readonly kind: 'tenant'; readonly tenantId: string }
  | { readonly kind: 'client'; readonly tenantId: string; readonly clientId: string }
  | {
      readonly kind: 'resource-server';
      readonly tenantId: string;
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

/** The owners that hold an owner list: every node of the resource tree except the platform. */
export type RoleOwner = Exclude<ManagementOwner, { readonly kind: 'platform' }>;

/** The owners that also hold an administrator list: a team and a tenant. */
export type AdministeredOwner = Extract<ManagementOwner, { readonly kind: 'team' | 'tenant' }>;

/** One list of principals holding a role at a node: the owners of a node, or the administrators of a team or tenant. */
export type RoleList =
  | { readonly role: 'owners'; readonly owner: RoleOwner }
  | { readonly role: 'admins'; readonly owner: AdministeredOwner };

/** The scope an owner is administered in: the platform, a team and a tenant are each their own scope. */
export function ownerScope(owner: ManagementOwner): ManagementScope {
  switch (owner.kind) {
    case 'platform':
      return PLATFORM_SCOPE;
    case 'team':
      return teamScope(owner.teamId);
    case 'tenant':
    case 'client':
    case 'resource-server':
      return { kind: 'tenant', tenantId: owner.tenantId };
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
      return `api/tenants/${encodeURIComponent(owner.tenantId)}/clients/${encodeURIComponent(owner.clientId)}`;
    case 'resource-server':
      return `api/tenants/${encodeURIComponent(owner.tenantId)}/resource-servers/${encodeURIComponent(owner.resourceServerId)}`;
  }
}

/** Builds the API path of a role list (`owners` or `admins`) under its node. */
export function roleListApiPath(list: RoleList): string {
  return `${ownerApiPath(list.owner)}/${list.role}`;
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

/**
 * The cache-tag id of an owner's setting limits. The owner's id is encoded inside its key, so
 * this never equals {@link ownerTagId} of another owner of the same kind.
 */
export function ownerLimitsTagId(owner: ManagementOwner): string {
  return scopeTagId(ownerScope(owner), `${ownerKey(owner)}:limits`);
}

/**
 * The cache-tag id of a role list. The node's id is encoded inside its key, so the owners and the
 * administrators of a node differ from each other and from {@link ownerTagId} of any owner.
 */
export function roleListTagId(list: RoleList): string {
  return scopeTagId(ownerScope(list.owner), `${ownerKey(list.owner)}:${list.role}`);
}

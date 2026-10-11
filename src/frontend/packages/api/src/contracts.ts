/**
 * TypeScript mirrors of the Management REST contracts
 * (NCode.Identity.OpenId.Management.Abstractions / Contracts). These are hand-modeled from the
 * server resources; keep them in sync with the C# contracts.
 */

/** Mirror of `CollectionResource<T>`. */
export type CollectionResource<T> = {
  readonly items: readonly T[];
  readonly continuationToken?: string;
};

/** Mirror of `TenantResource`. */
export type TenantResource = {
  readonly tenantId: string;
  readonly concurrencyToken: string;
  readonly domainName: string | null;
  readonly isDisabled: boolean;
  readonly displayName: string;
};

/**
 * Mirror of `CreateTenantRequest`. The tenant id is server-generated (not supplied), and `settings`
 * is the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 */
export type CreateTenantRequest = {
  readonly domainName: string | null;
  readonly displayName: string;
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable tenant metadata a JSON Patch may modify (mirror of `UpdateTenantRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateTenantFields = {
  readonly domainName: string | null;
  readonly displayName: string;
  readonly isDisabled: boolean;
};

/** Mirror of `ServerResource` (global scope). */
export type ServerResource = {
  readonly serverId: string;
  readonly concurrencyToken: string;
  readonly displayName: string;
};

/** Mirror of `ClientResource` (tenant scope). A client has no display name; it is identified by id. */
export type ClientResource = {
  readonly clientId: string;
  readonly tenantId: string;
  readonly concurrencyToken: string;
  readonly isDisabled: boolean;
};

/**
 * Mirror of `CreateClientRequest`. The client id is server-generated (not supplied), and `settings`
 * is the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 */
export type CreateClientRequest = {
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable client metadata a JSON Patch may modify (mirror of `UpdateClientRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateClientFields = {
  readonly isDisabled: boolean;
};

/** Mirror of `ScopeResource` — a scope (a permission, in Auth0 terms) owned by a resource server. */
export type ScopeResource = {
  readonly value: string;
  readonly description: string | null;
  readonly isSystem: boolean;
};

/**
 * Mirror of `ResourceServerResource` (an API, in Auth0 terms; tenant scope). The server embeds the
 * owned {@link ScopeResource} list inline, so browsing resource servers already yields their scopes.
 * `isSystem` marks a reserved, system-owned resource server that cannot be deleted. The opaque
 * JSON `settings` object is managed through its own surface and is not modeled here.
 */
export type ResourceServerResource = {
  readonly tenantId: string;
  readonly resourceServerId: string;
  readonly identifier: string;
  readonly concurrencyToken: string;
  readonly name: string;
  readonly isSystem: boolean;
  readonly isDisabled: boolean;
  readonly scopes: readonly ScopeResource[];
};

/**
 * Mirror of `CreateResourceServerRequest`. The resource-server id is server-generated (not supplied);
 * `identifier` is the operator-chosen audience and is immutable after creation. `settings` is the
 * initial JSON settings object (empty by default; settings have their own surface).
 */
export type CreateResourceServerRequest = {
  readonly identifier: string;
  readonly name: string;
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable resource-server metadata a JSON Patch may modify (subset of `UpdateResourceServerRequest`;
 * `identifier` is immutable and `settings` is managed separately). The store turns this into a
 * `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateResourceServerFields = {
  readonly name: string;
  readonly isDisabled: boolean;
};

/**
 * Mirror of `CreateScopeRequest`. The scope `value` is the operator-chosen permission string and is
 * immutable after creation (it is the resource identifier on the wire).
 */
export type CreateScopeRequest = {
  readonly value: string;
  readonly description: string | null;
};

/**
 * Mirror of `UpdateScopeRequest`. The server binds scope updates as a whole-resource `PUT` (not a
 * JSON Patch), and the scope value is immutable, so only the description is sent.
 */
export type UpdateScopeRequest = {
  readonly description: string | null;
};

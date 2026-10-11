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

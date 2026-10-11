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

/** Mirror of `ServerResource` (global scope). */
export type ServerResource = {
  readonly serverId: string;
  readonly concurrencyToken: string;
  readonly displayName: string;
};

/** Mirror of `ClientResource` (tenant scope). */
export type ClientResource = {
  readonly clientId: string;
  readonly tenantId: string;
  readonly concurrencyToken: string;
  readonly displayName: string;
  readonly isDisabled: boolean;
};

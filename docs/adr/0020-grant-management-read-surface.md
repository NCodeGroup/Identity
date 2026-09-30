# 20. Grant administration is a tenant-scoped, metadata-only read surface with opaque ids

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

The management API administers clients, servers, and tenants, but persisted **grants** (authorization codes, refresh
tokens, continuations, and similar runtime artifacts) have no administrative surface. An operator cannot see what
grants exist for a tenant or revoke a compromised one without direct database access.

Grants are unlike the other families in three ways that shape the surface:

- **They are created by the runtime, not by an operator.** `DefaultPersistedGrantService` mints a grant during a
  token flow; there is no `POST /grants`. The admin needs to **list**, **inspect**, and **revoke** — nothing more.
- **Their natural key is a secret.** A grant is looked up at runtime by `HashedKey`, the hash of the grant's token
  key. Exposing that value on an admin surface would leak a token identifier. Grants must therefore be addressed by
  something other than their natural key.
- **Their payload is sensitive.** `PayloadJson` holds the serialized grant (claims, tokens, subject material). It has
  no place in an administrative projection.

## Decision

**Grant administration is a read-only, tenant-scoped family — `GET /api/grants`, `GET /api/grants/{grantId}`,
`DELETE /api/grants/{grantId}` — addressed by an opaque `GrantId`, projecting metadata only, where `DELETE` is a
soft-revoke.**

- **Opaque `GrantId`, secret `HashedKey`.** Every grant carries a server-generated opaque `GrantId`
  (`ICryptoService.GenerateResourceId()` — the same 128-bit CSPRNG/base64url scheme as every other resource id,
  [ADR-0014](0014-server-generated-opaque-resource-ids.md)), minted by `DefaultPersistedGrantService` at creation and
  stored alongside the internal `HashedKey`. The admin surface lists, gets, and revokes **by `GrantId`**; `HashedKey`
  is never accepted or returned. The runtime token lookup keeps using `HashedKey` unchanged.
- **Metadata-only projection.** `GrantResource` exposes `GrantId`, `GrantType`, `TenantId`, `ClientId`, `SubjectId`,
  and the lifecycle timestamps (`CreatedWhen` / `ExpiresWhen` / `RevokedWhen` / `ConsumedWhen`). It deliberately omits
  both `HashedKey` and `PayloadJson`, mirroring the "no secret material on the surface" stance of
  [ADR-0011](0011-secret-management-api.md).
- **`DELETE` is a soft-revoke.** `DELETE /api/grants/{grantId}` sets `RevokedWhen` and keeps the row for audit rather
  than deleting it; revocation is idempotent (an already-revoked grant is left untouched and still returns `204`). A
  revoked grant is inert at runtime (`DefaultPersistedGrantService` already treats `RevokedWhen` as non-active).
- **Tenant-scoped like clients ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)).** The family
  runs under `TenantScopeEndpointFilter`, so the `OpenIdDbContext` global query filter scopes every read to the
  ambient tenant. A grant's tenant is **optional**; the filter is null-safe (`Tenant != null && …`) so a tenant-scoped
  request sees only its own tenant's grants and a non-tenant (global) grant is simply not materialized. List/get/revoke
  authorize against the request's tenant scope (a `TenantScopeResource`, so `TenantAdminHandler` matches and
  `GlobalAdminHandler` bypasses), consistent with the client family.
- **Keyset pagination ([ADR-0019](0019-collection-endpoints-keyset-pagination.md)) over the surrogate id.** `GET
/api/grants` returns a `CollectionResource<GrantResource>` page. Grants have no normalized natural key to order on
  (the natural key is the secret `HashedKey`), so the page orders on the chronological surrogate `Id`. To let a store
  that is **not** keyed by a resource id reuse the cursor machinery, the shared keyset mechanics live in `BaseStore`
  (not `BaseStoreWithResourceId`); each store supplies only its ordered, `Include`-shaped query and its sort key.

## Options considered

- **Address grants by `HashedKey`.** Rejected: `HashedKey` is a token-derived secret; putting it in URLs, logs, and
  responses leaks a grant identifier. An opaque `GrantId` decouples the admin identity from the runtime secret.
- **Hard-delete on `DELETE`.** Rejected: revocation must leave an audit trail, and a soft-revoke is also what makes
  `DELETE` idempotent and safe to retry. Physical cleanup of expired/revoked rows is a separate retention concern.
- **Return the full grant (including payload).** Rejected: the payload is sensitive and an admin does not need it to
  triage or revoke. A metadata projection is the least-privilege surface.
- **A central-admin (unscoped) grant surface.** Rejected for now (YAGNI): grants in practice are tenant-owned, and a
  tenant-scoped surface covers the operator need. Non-tenant (global) grants fall outside this surface; a dedicated
  central-admin read model can be added later without changing this contract.
- **A writable grant API (create/update).** Rejected: grants are a runtime artifact minted by the token pipeline, not
  an operator-authored resource. The admin need is observe-and-revoke.

## Consequences

- An operator can enumerate and inspect a tenant's grants and revoke a compromised one, without any exposure of the
  token secret or grant payload.
- The opaque `GrantId` is a new persisted column (unique-indexed, `NormalizedGrantId`) assigned at creation; existing
  runtime lookup by `HashedKey` is unaffected.
- Lifting the keyset mechanics into `BaseStore` lets any store paginate, including ones not keyed by a resource id;
  `BaseStoreWithResourceId` keeps only its by-id concerns.
- Revoke is idempotent and audit-preserving; a background retention job (not part of this surface) can later purge
  inert rows.
- Global (non-tenant) grants are intentionally invisible to this tenant-scoped surface until a central-admin read model
  is introduced.

## References

- [ADR-0011](0011-secret-management-api.md) — the "no secret material on the surface" precedent for a metadata-only
  projection.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — the opaque server-generated id scheme reused for
  `GrantId`.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the query filter that scopes the grant
  surface to the ambient tenant.
- [ADR-0019](0019-collection-endpoints-keyset-pagination.md) — the keyset pagination shape the grant list uses.
- Code: `NCode.Identity.OpenId.Persistence.DataContracts.PersistedGrant.GrantId`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Entities.GrantEntity` (`GrantId` / `NormalizedGrantId`),
  `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.GrantStore`,
  `NCode.Identity.OpenId.Management.Contracts.Grants.GrantResource`,
  `NCode.Identity.OpenId.Management.Endpoints.Grants.GrantApiEndpointHandler`.

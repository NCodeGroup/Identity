# 22. Grant administration gains specific subject/client filters and bulk revocation

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

[ADR-0020](0020-grant-management-read-surface.md) introduced a tenant-scoped grant read surface: list a page, get one by
opaque `GrantId`, and soft-revoke one. That covers inspection and single-grant revocation, but not the two operations an
operator actually reaches for during an incident:

- **See everything for a subject or a client** — "what does user X have?" or "what did client Y issue?" The list can
  only page the _entire_ tenant, so triaging a specific principal means scanning every grant.
- **Revoke in bulk** — "sign user X out everywhere" (account compromise or deactivation) and "kill client Y's tokens"
  (client compromise or decommission). Single-grant revoke forces an enumerate-then-revoke loop.

The schema already anticipates these access paths: `GrantEntity` carries `(TenantId, ClientId)` and
`(TenantId, NormalizedSubjectId)` indexes. The capability was missing, not the data model.

## Decision

**The grant read surface gains optional `subjectId`/`clientId` filters on the list, and a bulk soft-revoke keyed by the
same filters.**

- **Filtered list.** `GET /api/grants?subjectId=&clientId=&cursor=&limit=` — both filters are optional and combine with
  AND; omitting them yields the existing full-tenant page. Filtering stays within the keyset pagination of
  [ADR-0019](0019-collection-endpoints-keyset-pagination.md); this is exactly the "a **specific** filter, backed by an
  explicit store method" that ADR-0019 permits — not a general query protocol.
- **Bulk revoke.** `DELETE /api/grants?subjectId=&clientId=` soft-revokes every **active** matching grant (sets
  `RevokedWhen`, keeps rows for audit), mirroring the single-grant `DELETE /api/grants/{grantId}`. **At least one filter
  is required** — an unfiltered call returns `400 Bad Request` rather than silently revoking the whole tenant. It
  returns `GrantRevocationResult { revoked }` so the operator sees how many grants were affected. Revocation is
  idempotent: already-revoked grants are not re-touched and not counted.
- **Tenant isolation and authorization are unchanged.** Both run under `TenantScopeEndpointFilter`, so the
  [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) query filter scopes every read and revoke to
  the ambient tenant; a tenant admin cannot reach another tenant's grants. List authorizes with `Operations.List` and
  bulk revoke with `Operations.Delete`, both against the tenant scope — consistent with the rest of the grant surface.
- **Store shape.** `IGrantStore.GetPageAsync` takes the optional `subjectId`/`clientId`; a new `RevokeWhereAsync`
  loads the matching active grants, stamps `RevokedWhen`, and returns the count, letting the concurrency-token
  interceptor ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)) bump exactly the changed rows. The keyset
  page-building primitives are lifted to `BaseStore` (`BuildPageAsync`, protected cursor codec) so the filtered query
  reuses them without duplicating the mechanics.

## Options considered

- **A general query protocol (OData/JSON:API) over grants.** Rejected for the same reasons as
  [ADR-0019](0019-collection-endpoints-keyset-pagination.md): it leaks `IQueryable`, is a denial-of-service and
  complexity surface over a security store, and is a heavyweight versioned contract. Two indexed, first-class filters
  meet the real need.
- **A dedicated `POST /api/grants/revoke` action instead of `DELETE` with query filters.** Rejected: `DELETE` on the
  collection with a required filter mirrors the single-grant `DELETE /api/grants/{grantId}` and the list's own
  `subjectId`/`clientId` query, keeping the surface symmetric. The required-filter guard removes the accidental
  revoke-all footgun that would otherwise motivate a distinct verb.
- **Bulk revoke via a single set-based `ExecuteUpdate`.** Rejected: it bypasses the concurrency-token interceptor and
  the change tracker, so running tenant instances would not observe the bump. Loading and stamping the matching active
  grants keeps revocation consistent with single-grant revoke; the match set (one subject's or client's grants) is
  bounded.
- **Allow an unfiltered bulk revoke (revoke the whole tenant).** Rejected: too dangerous for a single request. A
  tenant-wide purge, if ever needed, should be a separate, explicit operation.

## Consequences

- Operators can triage and revoke by principal: list a user's or client's grants, and revoke all of them in one call
  with a reported count — the core incident-response operations.
- The read surface stays keyset-paginated with no query language to secure or version; the two filters are indexed.
- Bulk revoke is tenant-isolated, idempotent, and audit-preserving; the required-filter guard prevents accidental
  tenant-wide revocation.
- `RevokeWhereAsync` materializes the matching active grants; for a single subject or client this is bounded, but a
  pathologically large match set is loaded into memory. A future set-based path can be added if that ever matters,
  provided it also bumps the row tokens.

## References

- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor that bumps row tokens on the grants a
  bulk revoke changes.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the query filter that scopes the filtered
  list and bulk revoke to the ambient tenant.
- [ADR-0019](0019-collection-endpoints-keyset-pagination.md) — keyset pagination and its allowance for specific,
  store-backed filters.
- [ADR-0020](0020-grant-management-read-surface.md) — the grant read surface this extends.
- Code: `NCode.Identity.OpenId.Persistence.Stores.IGrantStore` (`GetPageAsync`, `RevokeWhereAsync`),
  `NCode.Identity.OpenId.Management.Contracts.Grants.GrantRevocationResult`,
  `NCode.Identity.OpenId.Management.Endpoints.Grants.GrantApiEndpointHandler`.

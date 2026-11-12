# 1020. A tenant's grants are listed, inspected and revoked by opaque identifier, never by their key

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

The management API administers clients, resource servers and tenants, but persisted grants (authorization codes, refresh
tokens, continuations and similar runtime artifacts) need an administrative surface too. An operator who cannot see
which grants a tenant holds, or revoke a compromised one, needs direct database access.

Grants differ from the other resource families in three ways that shape the surface:

- The runtime mints them. `DefaultPersistedGrantService` creates a grant during a token flow, so an operator never
  creates or updates one. The administrator needs to list, inspect and revoke.
- Their natural key is a secret. The runtime looks a grant up by `HashedKey`, the hash of the grant's token key, and
  exposing it on an administrative surface would leak a token identifier.
- Their payload is sensitive. `PayloadJson` holds the serialized grant (claims, tokens, subject material), which has no
  place in an administrative projection.

Incident response adds two operations: seeing everything a subject or a client holds ("what does user X have?", "what
did client Y issue?") and revoking in bulk ("sign user X out everywhere", "kill client Y's tokens"). The schema already
carries the access paths, with `GrantEntity` indexed on `(TenantId, ClientId)` and `(TenantId, NormalizedSubjectId)`.

## Decision

**Grants are administered under `/api/tenants/{tenantId}/grants`, addressed by an opaque `GrantId`, projected as
metadata only and revoked softly, one at a time or in bulk by filter.**

### Routes

| Route                                                  | Effect                                              |
| ------------------------------------------------------ | --------------------------------------------------- |
| `GET /api/tenants/{tenantId}/grants`                   | Lists a page of the tenant's grants.                |
| `GET /api/tenants/{tenantId}/grants/{grantId}`         | Reads one grant.                                    |
| `DELETE /api/tenants/{tenantId}/grants/{grantId}`      | Revokes one grant.                                  |
| `DELETE /api/tenants/{tenantId}/grants?subjectId=&...` | Revokes every unrevoked grant that matches filters. |

There is no generic create or update, because the runtime mints grants. The handler is `GrantApiEndpointHandler`.

### Identifier

Every grant carries a server-generated opaque `GrantId`, produced by `ICryptoService.GenerateResourceId()` (the scheme
of [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md)) and
unique-indexed as `NormalizedGrantId`. `DefaultPersistedGrantService` mints it when it creates the grant. The surface
lists, reads and revokes by `GrantId` and never accepts or returns `HashedKey`, which the runtime token lookup keeps
using.

### Projection

`GrantResource` carries metadata only: `GrantId`, `GrantType`, `TenantId`, `ClientId`, `SubjectId`, `SessionId`,
`CreatedWhen`, `ExpiresWhen`, `RevokedWhen` and `ConsumedWhen`. It never carries `HashedKey` or `PayloadJson`, in the
way [ADR-1011](1011-secret-management-api.md) keeps secret material off its surface.

### Listing and filters

The list takes optional `subjectId`, `clientId`, `sessionId` and `grantType` filters, combined with AND and backed by
`IGrantStore.GetPageAsync(GrantFilter, PageRequest, ...)`. It is keyset-paged over the surrogate id and answers
`CollectionResource<GrantResource>` with `cursor` and `limit`, as
[ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md) shapes
every collection. The filters are specific, store-backed and indexed; there is no query protocol.

### Revocation

`DELETE .../grants/{grantId}` is a soft revoke: it stamps `RevokedWhen` and keeps the row for audit. It is idempotent,
so an already-revoked grant is left alone and still answers `204`. A revoked grant is inert at runtime, because
`DefaultPersistedGrantService` treats `RevokedWhen` as not active.

The bulk revoke, `DELETE .../grants?subjectId=&clientId=&sessionId=`, revokes every unrevoked matching grant through
`IPersistedGrantService.RevokeWhereAsync`. That method loads the matching rows and stamps them, so the
concurrency-token interceptor of
[ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)
bumps exactly the rows that changed. The bulk revoke:

- requires at least one filter and answers `400` otherwise;
- answers `200` with `GrantRevocationResult { revoked }`, which does not count grants that were already revoked;
- is audited.

A revocation retries a lost race and never answers `412`, as
[ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md) holds for the grant row.

### Scope and authorization

The routes run under `TenantScopeEndpointFilter`, so the persistence query filter of
[ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md) confines every
read and revoke to the route's tenant. A grant with no tenant is invisible here. Authorization is at the tenant scope
(`TenantScopeResource`): `Operations.List` lists, `Operations.Read` reads one and `Operations.Delete` revokes. The
authority itself is a role assignment, as
[ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) holds, and the route authorizes
before it reads, as [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)
holds.

The one operator-minted grant, an RFC 7591 initial access token (`POST .../grants/initial-access-tokens`), belongs to
dynamic client registration and is described in
[ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md).

## Options considered

- **Address a grant by `HashedKey`.** It loses: the value is a token-derived secret, and putting it in URLs, logs and
  responses leaks a grant identifier. An opaque `GrantId` separates the administrative identity from the runtime secret.
- **A hard delete.** It loses: revocation leaves an audit trail, and a soft revoke is what makes `DELETE` idempotent and
  safe to retry.
- **Return the payload.** It loses: the payload is sensitive, and an administrator needs only metadata to triage or
  revoke. A metadata projection is the least-privilege surface.
- **A central-admin, unscoped grant surface.** It loses: grants are tenant-owned, and a tenant-scoped surface covers the
  operator need.
- **A writable grant API.** It loses: the token pipeline mints grants, so the administrative need is to observe and
  revoke.
- **OData or JSON:API over grants.** It loses: such a protocol leaks `IQueryable`, is a denial-of-service and
  complexity surface over a security store, and is a heavyweight versioned contract. Four indexed filters meet the real
  need.
- **A `POST .../revoke` action instead of `DELETE` with filters.** It loses: `DELETE` on the collection mirrors the
  single-grant `DELETE` and the list's own query, and the required-filter guard removes the accidental revoke-all risk
  that a distinct verb would address.
- **A set-based `ExecuteUpdate` for the bulk revoke.** It loses: it bypasses the concurrency-token interceptor and the
  change tracker, so running instances would not observe the bump. Loading and stamping the rows keeps bulk revocation
  consistent with single revocation, and one subject's or client's match set is bounded.
- **An unfiltered bulk revoke.** It loses: revoking a whole tenant is too dangerous for one request.
- **Chosen: a tenant-scoped surface addressed by `GrantId`, with a metadata projection, soft revocation and a
  filter-required bulk revoke.**

## Consequences

- An operator enumerates and inspects a tenant's grants, triages by subject, client, session or type, and revokes one
  grant or every matching grant with a reported count, without any exposure of the token secret or the payload.
- Revocation is idempotent and keeps an audit trail, and the required filter prevents accidental tenant-wide revocation.
- The read surface is keyset-paged with no query language to secure or version.
- `RevokeWhereAsync` materializes the matching unrevoked grants. For one subject, client or session this is bounded, but
  a very large match set loads into memory.
- Grants without a tenant are outside this surface.
- Tests cover the opaque identifier, the metadata projection, the idempotent revoke, the filter combinations, the
  required-filter `400`, the revoked count and the tenant confinement of every route.

## References

- [ADR-1011](1011-secret-management-api.md): the metadata-only projection precedent.
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md):
  the interceptor that bumps row tokens.
- [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md): the
  opaque identifier and keyset page.
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md): the tenant
  query filter.
- [ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md): the initial access
  token.
- [ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md): a lost race on a grant.
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md): authority as a role assignment.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md): authorize before
  reading.
- [RFC 7591](https://www.rfc-editor.org/rfc/rfc7591): OAuth 2.0 Dynamic Client Registration.
- Code: `NCode.Identity.OpenId.Persistence.DataContracts.PersistedGrant.GrantId`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Entities.GrantEntity` (`GrantId`, `NormalizedGrantId`),
  `NCode.Identity.OpenId.Persistence.Stores.IGrantStore` (`GetPageAsync`),
  `NCode.Identity.OpenId.Persistence.IPersistedGrantService` (`RevokeWhereAsync`),
  `NCode.Identity.OpenId.Management.Contracts.Grants.GrantResource`, `GrantRevocationResult`,
  `NCode.Identity.OpenId.Management.Endpoints.Grants.GrantApiEndpointHandler`.

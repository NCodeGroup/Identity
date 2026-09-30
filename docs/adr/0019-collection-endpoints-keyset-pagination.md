# 19. Collection endpoints use keyset (cursor) pagination, not a query protocol

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

The management API exposes per-resource CRUD (`POST` / `GET {id}` / `PATCH {id}` / `DELETE {id}`) for clients,
servers, and tenants, but no way to **enumerate** a family — there is no `GET /clients`, `GET /servers`, or
`GET /tenants`, and the stores expose only `GetOrDefaultAsync(id)`. An administrator cannot discover what exists
without already knowing every identifier. A naive `GET /clients` that returns every row is unbounded — a latent
denial-of-service and a memory/latency cliff as tenants accumulate clients.

Two questions follow: how to paginate safely, and whether to adopt a rich query protocol (OData, JSON:API) for the
read surface.

## Decision

**Each resource family gets a collection `GET` that returns a page of resources using keyset (cursor) pagination, with
no ad-hoc query protocol.**

- **Shape.** `GET /clients | /servers | /tenants` returns a `CollectionResource<TResource>` —
  `{ "items": [ … ], "continuationToken": "…" | null }`. A non-null `continuationToken` means "more pages exist;
  pass it back as `?cursor=` to fetch the next page." The token is opaque to the caller.
- **Keyset, not offset.** The page is ordered by the resource's **normalized natural key** (the indexed
  `NormalizedClientId` / `NormalizedServerId` / `NormalizedTenantId`), fetching `limit + 1` rows where the key is
  greater than the cursor. This is stable under concurrent inserts/deletes (no offset drift or skipped/duplicated
  rows) and always seeks an index rather than scanning-and-skipping. `?limit=` is clamped (default 50, max 100).
- **The store owns the cursor.** The cursor is opaque above the persistence layer: the store produces and consumes it
  (a base64url encoding of the last key). Each store gains a `GetPageAsync(cursor, limit, ct)` returning a
  `PagedResult<T>` (`items` + `nextCursor`); the shared keyset mechanics — decode, fetch `limit + 1`, detect "more",
  encode the next cursor — live once in `BaseStore`, with each store supplying only its ordered,
  `Include`-shaped query and its sort key.
- **Tenant scoping falls out of ADR-0018.** `GET /clients` runs under `TenantScopeEndpointFilter`, so the
  `OpenIdDbContext` global query filter already scopes the page to the ambient tenant — a tenant admin lists only
  their own clients with no list-specific code. `GET /servers` and `GET /tenants` are central-admin (unscoped).
- **Authorization is coarse, per family and scope.** A new `Operations.List` requirement is authorized once per
  request, not per item: the client list against the ambient tenant (so `TenantAdminHandler` matches, and
  `GlobalAdminHandler` bypasses), the server and tenant lists against a central-admin scope (a `null` resource, which
  only `GlobalAdminHandler` can satisfy).
- **Secrets stay un-paged.** A `GET /{id}/secrets` returns the owner's whole secret set — a small, bounded collection
  — so it needs no pagination.

## Options considered

- **A query protocol — OData (`$filter`/`$orderby`/`$select`/`$expand`/`$top`/`$skip`) or JSON:API (chosen: none).**
  Rejected. (a) It needs an `IQueryable` surfaced to the web layer to translate arbitrary expressions into SQL, which
  either leaks `IQueryable` out of the persistence layer — breaking the intent-specific store boundary of
  [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — or demands a partial, leaky
  protocol-to-store translator. (b) Arbitrary filter/sort/expand over a security database is a denial-of-service and
  complexity surface (unindexed sorts, expensive predicates, N+1 expands) at odds with a correct-by-default admin API.
  (c) It is a large, versioned public contract and a heavyweight dependency, against the repository's minimal-surface
  discipline. (d) OData paginates with `$skip`/`$top` (offset), the very approach rejected below. A **specific** filter,
  if ever needed, is added later as an explicit query parameter backed by an explicit store method — a non-breaking
  addition — and a genuine ad-hoc query need can be met by a separate, opt-in read model rather than by coupling the
  core contract to a protocol.
- **Offset/limit pagination (`?page=&pageSize=`).** Rejected: it drifts under concurrent writes (rows shift between
  pages, so items are skipped or repeated) and degrades to a scan-and-discard at high offsets.
- **Per-item authorization filtering of the page.** Rejected: dropping unauthorized items makes a page return fewer
  than `limit` and breaks the "more pages" signal. The `GlobalAdmin` / `TenantAdmin` model authorizes the **ability to
  enumerate a family in a scope**, which a single coarse check expresses correctly.

## Consequences

- Every family lists through one uniform, safe shape; a future tenant-bound family inherits pagination and scoping by
  adding its `GetPageAsync` and a `List` endpoint — the keyset mechanics and tenant isolation are already centralized.
- The cursor is stable and seek-based, so paging a large, actively-changing family is correct and cheap.
- The read surface stays small and predictable; there is no expression language to secure, version, or performance-tune.
- A list reuses the full resource mapping (it loads each owner with its `Include`d children), which is more than a
  metadata-only projection strictly needs; for bounded admin pages this is an acceptable simplicity trade, and a
  lighter projection can be introduced later without changing the contract.

## References

- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — opaque server-generated identifiers, the basis for
  ordering on the normalized natural key.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — the intent-specific store boundary that a
  query protocol would violate.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the query filter that scopes the client list
  to the ambient tenant for free.
- Code: `NCode.Persistence.Stores.PagedResult<T>`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.BaseStore` (keyset mechanics),
  `NCode.Identity.OpenId.Management.Contracts.CollectionResource<T>`,
  `NCode.Identity.OpenId.Management.Operations.List`.

# 1014. Management resources carry opaque server identifiers and list one keyset page at a time

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0123](project-template/0123-resource-ids-are-server-generated-and-opaque.md), [project-template ADR-0124](project-template/0124-collection-endpoints-use-keyset-pagination.md)

## Context

The management API creates, reads and lists many kinds of resource: clients, resource servers, secrets, grants, role
assignments, local accounts and principals. Two questions recur for every one of them. Who chooses its identifier, and
what does the identifier reveal? And how does a collection `GET` return a list that can grow without bound?

The project template decides both in general: identifiers are server-generated and opaque, and collection endpoints use
keyset pagination. This record holds the family's specifics: the one generator every resource shares, the identifiers
that come from elsewhere by design, the shape of a page, the store mechanics behind it and the collections that are
returned whole.

## Decision

**The server mints every resource identifier, and a collection `GET` returns one keyset page.**

### Identifiers

The server mints the identifier of every client (`client_id`), resource server, secret (also the JWK `kid`), grant, role
assignment, local account and principal with `ICryptoService.GenerateResourceId()`: 128 CSPRNG bits, encoded as
Base64Url, with no prefix. Because the generator is a method of the one injectable crypto seam, every service that needs
an identifier asks the same place and a test replaces it in one registration.

A create request carries no identifier for the resource it creates. The `201` body and the `Location` header return it.
Validators and unique indexes still assert uniqueness as a safety net.

Some identifiers come from elsewhere by design:

- A team's and a tenant's identifier is a name its creator chooses ([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)). The
  opaque server identifiers of this record cover every other management resource.
- The platform has no identifier.
- A resource server's `Identifier` (its audience) is operator-chosen and sits beside its opaque `ResourceServerId`.
- A parent reference comes from the route, never from the body.

Internal surrogate keys, the IdGen snowflake produced through `IdValueGenerator` and `UseIdGeneratorConvention`, are never
exposed. `Guid` is not used for resource identifiers.

### Pages

A collection `GET` answers `CollectionResource<TResource>`, `{ items, continuationToken }`, and takes `?cursor=` and
`?limit=`. `ManagementApiEndpointHandler` clamps the limit: `DefaultPageSize` is 50 and `MaxPageSize` is 100.

Stores page through `GetPageAsync(PageRequest { Cursor, Limit }, ...)`, which returns `PagedResult<T>` (`Items`,
`NextCursor`). `BaseStore` owns the keyset mechanics: it orders by the surrogate `Id`, fetches `limit + 1` rows, encodes
the last id as a Base64Url cursor and treats a malformed cursor as the start of the list. Each store supplies only its
ordered query.

A list is authorized once per request, at its node or tenant scope, with `Operations.List`, never per item. Tenant-bound
lists are confined by the persistence query filter under the route's tenant. The authority itself is the caller's role
assignment ([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)).

Small collections bounded by design are returned whole: an owner's secrets, a resource server's scopes, the role lists
and the `/api/me` lists.

A specific filter is an explicit query parameter backed by an explicit store method, as the grant list's are. There is
no general query protocol.

## Options considered

- **An opaque identifier plus a required name slug on every entity.** It doubles the identifiers a caller must keep
  straight and adds a uniqueness rule per entity. It loses.
- **Caller-chosen identifiers.** They let a caller probe for and collide with existing resources, and they leak naming
  schemes. They are kept only where the name is the resource, as for a team and a tenant.
- **OData or JSON:API.** They leak `IQueryable` into the surface, open a denial-of-service surface through arbitrary
  filters and sorts, and paginate by offset. They lose.
- **Offset paging.** It skips or repeats rows when the collection changes between requests and costs more as the offset
  grows. It loses.
- **Server-minted opaque identifiers and keyset pages (chosen).** The identifier reveals nothing and cannot be chosen by
  a caller, and a page costs the same wherever it sits and stays stable under concurrent writes.

## Consequences

- Every create path calls one generator, so identifier format and entropy change in one place.
- A client never needs to supply or guess an identifier, and a response never exposes a surrogate key.
- Every list endpoint has the same shape, and a new store gets paging by supplying an ordered query.
- A malformed cursor restarts the list instead of failing, so a stale or tampered cursor cannot cause an error.
- A caller who needs a specific subset needs an explicit filter and store method added for it; the absence of a general
  query protocol is deliberate.
- Smell: a create request that accepts an identifier, a list that authorizes per item, or a store that orders by
  anything but the surrogate `Id` has drifted from this record.

## References

- `ICryptoService.GenerateResourceId()`, `IdValueGenerator`, `UseIdGeneratorConvention`
- `CollectionResource<TResource>`, `PageRequest`, `PagedResult<T>`, `BaseStore`, `ManagementApiEndpointHandler`
- [project-template ADR-0123](project-template/0123-resource-ids-are-server-generated-and-opaque.md)
- [project-template ADR-0124](project-template/0124-collection-endpoints-use-keyset-pagination.md)
- [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)

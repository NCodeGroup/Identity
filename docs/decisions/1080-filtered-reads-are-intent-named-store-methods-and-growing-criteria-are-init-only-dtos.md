# 1080. Filtered reads are intent-named store methods, and growing criteria are init-only DTOs

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0107](project-template/0107-filtered-reads-push-filtering-to-the-database.md)

## Context

[project-template ADR-0107](project-template/0107-filtered-reads-push-filtering-to-the-database.md) runs a filtered
read's filter in the database, through an intent-named store method that projects only the columns its caller needs. It
rejects both a per-request cache over full reads and expression parameters on a store interface. This record applies it
to every store in the family and fixes the shape of the criteria those methods take.

A recurring smell appears across the runtime readers: a service pages a store's entire set through `GetPageAsync` (or an
equivalent list read) and then filters the materialized items in memory. This fetch-all-then-filter shape pushes no
predicate to the store, so it cannot use an index. It transfers every row, and every expensive child collection
(settings, secrets), only to discard most of them, and when several readers in one request each re-scan the same set the
waste compounds. The resource-server runtime reads show it most clearly: discovery, the client scope service, the scope
description service and the credential resolver each need a subset of enabled resource servers selected by scope
ownership, identifier or system ownership. The same shape can arise for grants, clients, consents, sessions or any other
store, so the decision is a persistence principle for all of them.

Selection criteria also grow. `IGrantStore.GetPageAsync` and `RevokeWhereAsync` filter by subject, client, session,
grant and grant type, and further criteria are likely. A store interface is implemented by consumers, so a new
positional parameter changes the method signature and breaks source and binary compatibility for every implementer and
caller once the family ships. Pagination has the same need: a `(cursor, limit)` pair of positional parameters cannot
gain a sort direction without a signature change.

## Decision

A filtered read is a discrete, intent-named method on the store interface, never a page of the whole set filtered in
memory. An optional, growing set of selection criteria is an init-only DTO, never a list of positional parameters.

### Filtered reads

The family applies project-template ADR-0107 to every store:

- **Intent-named, ORM-agnostic contract.** The method names what to fetch, not how (`FindEnabledByScopesAsync`,
  `GetEnabledIdentifiersAsync`). The Entity Framework store translates it to LINQ that the provider pushes to SQL, and a
  non-EF backend writes the query by hand. No `IQueryable` or `Expression` crosses the abstraction.
- **Projection for a few-column reader.** A reader that needs a few columns gets a lightweight projection type, so the
  query never joins or materializes unneeded child collections. Only a reader that needs the full entity loads it.
- **`GetPageAsync` is for unfiltered pagination.** It serves genuine pagination such as the management lists, never a
  filtering mechanism.
- **A normalization-backed filter is a widening pre-filter.** A domain value compared case-sensitively, such as scope
  values and resource identifiers under RFC 6749 and RFC 8707, is stored with a case-folded `Normalized*` column for
  uniqueness and sargability. The pushed-down filter matches on the normalized column, which never drops a true match,
  and the reader re-applies the exact case-sensitive match in memory over the small returned set. Exact predicates such
  as booleans and id-set membership belong wholly to the database.
- **No cache seam.** No cache or invalidation seam sits under these methods, and correctness never depends on cache
  freshness. A cache, if ever needed, layers on top of the methods without changing the contract.

The resource-server runtime reads use these `IResourceServerStore` methods, and every one considers enabled resource
servers only:

| Method                                            | Reader                                                                                     |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `GetEnabledScopesAsync`                           | Discovery's `scopes_supported` and `DefaultScopeDescriptionService`                        |
| `GetEnabledIdentifiersAsync`                      | The audience identifiers of enabled resource servers                                       |
| `GetEnabledScopeProjectionsByScopesAsync`         | `DefaultClientScopeService`: the enabled servers that own one of a set of scopes           |
| `GetEnabledScopeProjectionsBySystemOrIdsAsync`    | `DefaultClientScopeService`: the enabled servers that are system-owned or in an id set     |
| `FindEnabledByScopesAsync`                        | `DefaultResourceServerCredentialResolver`: the full servers that own one of a set of scopes |

`ResourceServerScopeProjection` carries only the identifier, resource-server id, system flag and scope values, so those
queries never materialize settings or secrets. Only the credential resolver, which must encrypt to one audience's keys,
loads the full `PersistedResourceServer`.

### Evolving criteria

- **Optional and growing: an init-only DTO.** A set of optional selection criteria is a `readonly record struct` with
  init-only, non-`required` properties, built with object initializers or `with` and never a positional constructor, so a
  criterion is added without changing the method signature. `GrantFilter { SubjectId, ClientId, SessionId,
  WithoutSession, GrantId, GrantType }` is the example.
- **An `IsEmpty` guard where an empty filter is dangerous.** A criteria DTO carries one where an empty filter would act
  on everything, such as an unscoped bulk revoke.
- **Required key: positional.** A required identity key is the operation's subject, not a growing filter, so it stays
  positional, such as the client id a client-grant query is scoped to. The rule is "optional and growing: init-only DTO;
  required key: positional", and the production coding conventions carry it.
- **Pagination is `PageRequest`.** The shared `PageRequest { Cursor, Limit }` (`Limit` required) sits beside
  `PagedResult<T>` in `NCode.Identity.Persistence.Stores` and every store's `GetPageAsync` takes it, so pagination can
  grow, for example with a sort direction, the same way. `IGrantStore.GetPageAsync(GrantFilter, PageRequest, ct)` and
  `RevokeWhereAsync(GrantFilter, revokedWhen, ct)` show both shapes.
- **Request and command inputs follow the same shape.** `CreateSessionRequest`, `CreateSecurityTokenRequest` and the
  mediator `*Command` records are init-only records.

## Options considered

- **Page the whole set and filter in memory.** It is simple and needs no new store method, but it uses no index,
  transfers every row and child collection, and repeats the scan in each reader of a request. Rejected.
- **Expose `IQueryable` or `Expression` parameters on the store interface.** One generic method would serve every
  filter, but it ties the contract to an ORM, lets the caller shape the SQL, and forces a non-EF backend to interpret
  expression trees. Rejected, as project-template ADR-0107 rejects it.
- **Cache the full reads for the request.** It cuts the query count without a new method, but the full read still
  transfers every row and child collection, and correctness would depend on cache freshness. Rejected, as
  project-template ADR-0107 rejects it.
- **Add criteria as positional or optional parameters.** It is the shortest edit for one more filter, but each addition
  changes the method signature and breaks source and binary compatibility for implementers and callers of a shipped
  store interface. Rejected.
- **Wrap every parameter in a DTO.** It would give one uniform shape, but a required identity key is the operation's
  subject and gains nothing from an optional bag of properties. Rejected.
- The chosen design is discrete intent-named store methods that project what the caller needs, and init-only criteria
  DTOs for the optional and growing filters, with required keys positional and pagination in the shared `PageRequest`.

## Consequences

- A new filtered read adds a discrete store method by default, and `GetPageAsync` stays for unfiltered pagination.
- A resource-server runtime read transfers only the enabled servers and columns it needs, and discovery, the client scope
  service and the scope description service all ignore disabled resource servers.
- A store interface gains a criterion without breaking implementers or callers once the family ships, because the
  criteria shape leaves the method signature unchanged.
- Contributors have one memorable rule: optional and growing is an init-only DTO, and a required key is positional.
- A cache can be added later on top of these methods if profiling shows the per-request query count is the bottleneck,
  without changing the contract.

## References

- [project-template ADR-0107](project-template/0107-filtered-reads-push-filtering-to-the-database.md): the general rules
  for a filtered read.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md): the
  pre-release posture, and the public API surface that criteria DTOs keep stable.
- [ADR-1158](1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md): the credential resolver's use of one audience's
  keys.
- `NCode.Identity.Persistence.Abstractions`: `IResourceServerStore`, `ResourceServerScopeProjection`, `IGrantStore`,
  `GrantFilter`, `PageRequest` and `PagedResult<T>`.
- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md): the convention for
  day-to-day work.
- RFC 6749 and RFC 8707: the case-sensitive scope and resource identifier comparison.

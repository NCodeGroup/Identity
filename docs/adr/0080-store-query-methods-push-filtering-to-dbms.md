# 80. Store query methods push entity filtering into the DBMS

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode.Identity maintainers

## Context

A recurring smell appears across the runtime readers: a service pages a store's **entire** set via
`GetPageAsync` (or an equivalent list read) and then filters the materialized items **in memory**. This
**fetch-all-then-filter-in-memory** shape pushes no predicate to the store, so it cannot use indexes, it
transfers every row — and every expensive child collection (settings, secrets) — only to discard most of
them, and when several readers in one request each re-scan the same set the waste compounds.

The **resource-server runtime reads** were the first and clearest instance (and the impetus for this ADR):
`DefaultDiscoverScopesHandler`, `DefaultClientScopeService` (`GetAllowedScopesAsync` / `ResolveAudiencesAsync`
/ `GetEnabledResourceServerIdentifiersAsync`), `DefaultScopeDescriptionService.DescribeScopesAsync`, and
`DefaultResourceServerCredentialResolver.ResolveAsync` all paged the full resource-server set and filtered by
enabled-ness, scope ownership, identifier, or system-ownership in memory. But the same anti-pattern can arise
for grants, clients, consents, sessions, or any other store, so the decision is stated as a **general
persistence principle**, not a resource-server-specific fix.

Two alternatives were considered and rejected:

- **A cached, concurrency-token-validated collection provider** (mirroring `IReadOnlySettingCollectionProvider`
  / `ISecretKeyCollectionProvider`) that loads the set once per request and keeps in-memory filtering. It
  removes per-request redundancy but keeps the fetch-all shape, adds a stateful cache + invalidation seam, and
  still filters in memory.
- **A specification / `IQueryable`-predicate abstraction** (e.g. passing `Expression<Func<TEntity, bool>>`
  into the store). This would leak an Entity-Framework / `IQueryable` shape into the persistence abstraction
  and break the seam that lets a non-`IQueryable` backend (Dapper, a document store, hand-written SQL)
  implement `IStore`.

## Decision

When a reader needs a **filtered** subset of a store's entities, express that filter as a **discrete,
intent-named query method on the store interface** rather than paging the whole set and filtering in memory.
This is the default for all stores in the family; `GetPageAsync` remains for genuine unfiltered pagination
(for example, management list endpoints), not as a filtering mechanism.

- **Intent-named, ORM-agnostic contract.** The method names _what_ to fetch, not _how_
  (`FindEnabledByScopesAsync`, `GetEnabledIdentifiersAsync`, …). The Entity Framework store translates it to
  LINQ that the provider pushes to SQL; a non-EF backend is free to hand-write the query. No `IQueryable` /
  `Expression` ever crosses the abstraction.
- **Project to only what the caller needs.** Readers that need a few columns get a lightweight projection type
  (a narrow `SELECT`) so the query never joins or materializes unneeded child collections; only readers that
  genuinely need the full entity fetch it.
- **Normalization-backed filters are a widening pre-filter.** Where a value is compared **case-sensitively** by
  the domain (for example scope values and resource identifiers under RFC 6749 / RFC 8707) but the schema
  stores only a case-folded `Normalized*` column for uniqueness and sargability, the pushed-down filter matches
  on the normalized column — which **never drops a true match** — and the reader re-applies the exact
  case-sensitive match in memory over the (now small) returned set. Exact predicates (booleans, id-set
  membership) are fully owned by the DBMS.

### First application: resource-server reads

The six resource-server readers above were rebased onto new `IResourceServerStore` methods:

- `GetEnabledScopesAsync(scopeValues?)` → scopes of enabled resource servers, optionally pre-filtered to a
  value set (discovery's `scopes_supported` and scope-description lookups).
- `GetEnabledIdentifiersAsync()` → audience identifiers of enabled resource servers.
- `GetEnabledScopeProjectionsByScopesAsync(scopeValues)` → lightweight `{ identifier, resourceServerId,
isSystem, scopeValues }` projections of enabled servers owning one of the scopes.
- `GetEnabledScopeProjectionsBySystemOrIdsAsync(resourceServerIds)` → the same projection for enabled servers
  that are system-owned or whose id is in the set (the allowed-scopes computation).
- `FindEnabledByScopesAsync(scopeValues, identifiers?)` → full `PersistedResourceServer` (with settings and
  secrets) for enabled servers owning one of the scopes, optionally narrowed to a set of audience identifiers
  (the credential-resolver choke point).

The lightweight `ResourceServerScopeProjection` carries only the identifier/scope columns, so those queries
never materialize settings or secrets; only the credential resolver — which must encrypt to a single
audience's keys (ADR-0075) — fetches the full entity.

## Consequences

- Each reader transfers only the rows (and, for projections, only the columns) it needs; non-matching entities
  are filtered by the DBMS using indexes rather than in memory.
- The persistence contract stays ORM-agnostic: intent-named methods, no `IQueryable` / `Expression` leakage,
  so a non-EF store can satisfy the same contract.
- No cache or concurrency-token invalidation seam is introduced; correctness does not depend on cache
  freshness. If profiling later shows the per-request query **count** is the bottleneck, a cache can be layered
  **on top of** these methods without changing the contract.
- Trade-off: more, narrower store methods instead of a few broad list reads. This is the intended granularity —
  each method is a distinct, index-friendly query and documents a real access pattern.
- Applying the first instance, `DescribeScopesAsync` now consistently ignores **disabled** resource servers (it
  previously described scopes from disabled servers too), aligning it with every other runtime reader.
- Going forward, new filtered reads add a discrete store method by default; reach for `GetPageAsync` only for
  unfiltered pagination.

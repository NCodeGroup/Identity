# 80. Cached per-tenant resource-server collection provider

- **Status:** Proposed
- **Date:** 2026-10-12
- **Deciders:** NCode.Identity maintainers

## Context

Several runtime services read the tenant's resource servers by **paging the entire set and filtering in
memory**, each with a slightly different predicate:

- `DefaultClientScopeService.GetAllowedScopesAsync` (resource-server scopes ∩ client grants),
- `DefaultClientScopeService.ResolveAudiencesAsync` (scope → owning audiences),
- `DefaultClientScopeService.GetEnabledResourceServerIdentifiersAsync` (enabled identifiers, added for
  resource indicators — [ADR-0078](0078-resource-indicators-rfc-8707.md)),
- `DefaultResourceServerCredentialResolver.ResolveAsync` (scope/resource → audiences + per-audience
  secrets).

This exhibits two distinct smells. First, **fetch-all-then-filter-in-memory** pushes no predicate to the
store, so it cannot use indexes. Second, and more costly, a single token or authorization request runs
**several** of these services, each independently paging the **same** resource-server set 2–4× per
request.

Bespoke per-caller store filter methods (`GetByScopesAsync`, `GetByIdentifiersAsync`, `GetEnabledAsync`,
…) would address the first smell but not the second, and several predicates (a granted-scope ∩
still-defined-scope intersection) are not clean SQL filters. Resource servers are a **small,
slowly-changing** per-tenant set — the profile the family already serves through cached, concurrency-token
-validated collection providers (`IReadOnlySettingCollectionProvider`, `ISecretKeyCollectionProvider`, and
the `IServerStore.GetSettingsAsync`/`GetSecretsAsync(lastKnownState)` → `ConcurrentState<T>` pattern).

## Decision (proposed)

Introduce a per-tenant **`IResourceServerCollectionProvider`** that loads the tenant's resource servers
**once per request** (cached and validated by a concurrency token, mirroring the settings/secrets
providers) into an immutable projection of `{ identifier, isEnabled, isSystem, scopes, effective settings,
secrets }`. The four callers above resolve from this projection and keep their (now cheap, over a cached
handful of items) in-memory filtering. Discrete store-level filter methods are added only where a
predicate is both selective and over a genuinely large/cold set.

## Consequences (anticipated)

- Collapses N full resource-server scans per request to a single cache check; in-memory filtering becomes
  negligible over the cached set.
- Reuses the established collection-provider + concurrency-token caching seam rather than scattering many
  narrow store query methods.
- Requires a new abstraction and default implementation, a cache-invalidation story keyed on the
  resource-server concurrency token, and rebasing the four existing readers onto it. Tracked as backlog
  `D3`.

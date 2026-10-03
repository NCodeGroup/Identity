# 37. The tenant cache is probed before the store is read on the resolution hot path

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

Every OpenID request materializes its tenant through `DefaultOpenIdTenantFactory`, which composes the shared
`ITenantResolver` for selection and an `IOpenIdTenantCache` (keyed by tenant identifier) to avoid re-materializing the
heavyweight `OpenIdTenant` (issuer, base address, merged settings provider, secret-key provider, periodic refresh
wiring) on each request.

Tenant selection returns a `PersistedTenant` loaded from the store
([ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md),
[ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)). Because the factory read the store to obtain
the tenant identifier _before_ consulting the cache, every request paid a tenant-store round-trip even when the
materialized tenant was already cached — the cache saved re-materialization but never the database read. For the
static-single and dynamic-by-path strategies the tenant identifier is knowable from the request alone (configuration,
or a route value), so that round-trip was pure overhead on the hottest path in the server.

## Decision

**Tenant selection is split into a cheap, store-free identity probe and a full load, and the factory probes the tenant
cache using the identity probe before it reads the store.**

- `ITenantResolver` and `ITenantStrategy` gain
  `bool TryGetTenantId(HttpContext httpContext, out string? tenantId)`: it returns the tenant identifier when the
  strategy can determine it from the request **without** a store round-trip, and `false` otherwise.
    - **Static-single** returns the configured tenant identifier.
    - **Dynamic-by-path** returns the route value.
    - **Dynamic-by-host** returns `false`: mapping a domain to a tenant inherently requires the store, so host-based
      tenancy still performs a full resolve.
- `DefaultOpenIdTenantFactory.CreateTenantAsync` probes the cache with the identity returned by `TryGetTenantId`; only
  on a cache miss (or when the probe declines) does it call `ResolveTenantAsync` to load the `PersistedTenant` and
  materialize the tenant.
- A cache **hit** serves the cached tenant without re-reading the store. The store read — which also validates
  existence and the disabled flag with a `404` — runs only on the cache-miss path that first materializes the tenant,
  so a disabled tenant is never cached in the first place.

## Options considered

- **Keep loading the `PersistedTenant` before the cache probe (status quo).** Rejected: it defeats the cache's purpose
  on the request hot path, charging a database round-trip per request for an already-materialized tenant.
- **Cache a domain-to-identifier map so dynamic-by-host can also probe store-free.** Rejected as premature: it adds a
  second cache with its own invalidation and staleness surface for one strategy; host-based tenancy keeps its single
  store read. This remains available as a future optimization.
- **Re-validate the disabled flag on every request even on a cache hit.** Rejected: it reintroduces the per-request
  store read this ADR removes. Bounding disabled-tenant staleness to the cache's sliding expiration is the accepted
  trade-off of caching a tenant at all.

## Consequences

- Static-single and dynamic-by-path requests serve a warm tenant with no tenant-store round-trip; dynamic-by-host is
  unchanged.
- The static-single strategy no longer re-ensures (and lazily provisions) the root tenant on warm requests; that work
  now runs only on the cache-miss path, which is the correct place for one-time provisioning.
- A tenant disabled _after_ it was cached continues to serve until its cache entry expires
  (`OpenIdTenantOptions.TenantCacheExpiration`). Operators requiring immediate cut-off must shorten the expiration or
  evict the entry; this is the inherent staleness window of the tenant cache.
- `TryGetTenantId` is a store-free, non-throwing probe; the authoritative validation (missing or disabled tenant → a
  `404`) stays in `ResolveTenantAsync` on the load path.

## References

- Code: `NCode.Identity.OpenId.Tenants.ITenantResolver`, `ITenantStrategy`, `DefaultTenantResolver`, `TenantStrategy`,
  `StaticSingleTenantStrategy`, `DynamicByPathTenantStrategy`, `DynamicByHostTenantStrategy`,
  `DefaultOpenIdTenantFactory` (`NCode.Identity.OpenId.Core`).
- Prior art: [ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md),
  [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md),
  [ADR-0024](0024-control-plane-and-per-tenant-planes.md).

# 18. Tenant selection is shared; tenant-bound data access is scoped at the persistence layer

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group
- **Supersedes:** [ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md)

## Context

The OpenID runtime resolves the ambient tenant for a request as a first-class, pluggable concern: a static single
tenant, or a tenant derived from the request host or path. This request-derived tenancy doubles as a security /
deployment / infrastructure boundary — a deployment reached at `tenant1.example.com` (or `/tenant1/…`) serves exactly
one tenant. The management API administers those same tenants and their children (clients and, in the future, other
tenant-bound resources).

[ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md) separated tenant _selection_ from
tenant _materialization_ and enforced the management scope with an _optional post-fetch boundary_ (`ITenantBoundary`):
each endpoint loaded a resource by its globally-unique opaque id ([ADR-0014](0014-server-generated-opaque-resource-ids.md)),
then compared the resource's `TenantId` to the request's resolved tenant and returned `404` on a mismatch. That
boundary has a data-sensitivity flaw: the resource — including sensitive material — is **materialized from the store
before** the scope decision. A logging statement, an early return, or a future refactor in that window can leak another
tenant's data. Because the resource's tenant is only knowable by reading the row, no pre-fetch filter can gate the
_specific_ resource; the fetch itself must be scoped.

## Decision

**Tenant _selection_ remains a shared abstraction, and tenant-bound _data access_ is scoped at the persistence layer
so a cross-tenant row is never materialized.** "Never fetch what you cannot see" replaces "fetch, then hide."

- **Selection (shared, unchanged from ADR-0017).** `ITenantResolver` (plus the pluggable `ITenantStrategy` for
  static-single / dynamic-by-host / dynamic-by-path, and `TenantResolutionOptions`) resolves a request to a
  `PersistedTenant`. Contracts live in `NCode.Identity.OpenId.Abstractions`; the default implementation lives in
  `NCode.Identity.OpenId.Core`. Both the runtime and the management API depend only on the abstraction and receive the
  implementation through dependency injection ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)).
- **Ambient scope (shared).** `IAmbientTenantAccessor` (in `NCode.Identity.OpenId.Persistence.Abstractions`) holds the
  request's tenant scope. The default implementation stores it in an `AsyncLocal<T>` so the scope flows across
  asynchronous calls — including data-access components created outside the current dependency-injection scope (the
  `OpenIdDbContext` is created by an `IDbContextFactory` from the root provider). When no scope is active the accessor
  is unscoped.
- **Enforcement (persistence).** `OpenIdDbContext` applies a global query filter to each tenant-child entity
  (`ClientEntity`, `ClientSecretEntity`) of the form _"ambient tenant is unset **or** the row's tenant equals the
  ambient tenant."_ When a scope is active, a row owned by another tenant is never returned by any query, so it is
  never materialized (defense against cross-tenant leaks). When unset — the runtime, and central-admin surfaces — the
  filter is a no-op. Tenant-owned families (the tenant itself and its secrets) are intentionally **not** scoped, so
  tenant and server administration stay central-admin.
- **Establishing the scope (management).** A reusable `TenantScopeEndpointFilter` on the tenant-bound endpoint group
  ([ADR-0009](0009-endpoint-families-own-their-route-group.md)) resolves the ambient tenant via `ITenantResolver` and
  opens an `IAmbientTenantAccessor` scope for the duration of the request; an unresolvable tenant short-circuits with
  its mapped result (typically `404`). For static-single (central-admin) deployments the filter is a no-op.
- **Writes.** A create carries a target `TenantId` in its body, which no read filter can constrain, so the
  create validator rejects a resource whose tenant differs from the ambient scope with `404` (never `403`, so a scoped
  surface cannot probe another tenant's existence). Reads and deletes need no such check: the query filter already
  makes a cross-tenant resource unreachable.

## Options considered

- **Keep the post-fetch `ITenantBoundary` (ADR-0017).** Rejected: it materializes the resource before the scope
  decision, leaving a cross-tenant data-leak window that repeats at every call site.
- **Tenant-scoped getters at each call site** (e.g. `TryGetByClientIdAsync(ambientTenant, id)`). Rejected as the
  primary mechanism: correct only if every call site remembers to pass the scope; a single omission silently leaks.
- **A pre-fetch endpoint filter that checks the resource's tenant.** Impossible: with globally-unique opaque ids the
  resource's tenant is unknown until the row is read.
- **An ambient scope plus an EF global query filter (chosen).** Leak-proof by construction — no query can return a
  cross-tenant row, and no call site can forget it — while remaining a no-op for the runtime and central-admin
  surfaces.

## Consequences

- Tenant-bound reads cannot leak: a cross-tenant resource is not fetched at all, so it cannot be logged, inspected, or
  returned. The management endpoints drop their per-call boundary checks; a cross-tenant resource is simply "not
  found."
- The scope is established once per request by a reusable filter, and the persistence layer enforces it uniformly, so
  new tenant-bound resource families inherit isolation by adding the entity to the scoped set — no new per-endpoint
  logic.
- The `OpenIdDbContext` takes an optional `IAmbientTenantAccessor` (defaulting to unscoped), so direct construction
  (tests, tooling) and consumers without the ambient accessor keep working unchanged.
- The tenant selection rules still have a single source of truth shared by the runtime and the management API, so the
  two can never diverge on "which tenant is this request?"
- Extracting the scope and removing `ITenantBoundary` are public-API changes on pre-1.0 unshipped surface, so no
  shipped contract is broken.

## References

- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoint families own their route group and
  cross-cutting filters, the seam `TenantScopeEndpointFilter` uses.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — globally-unique opaque identifiers, why a resource's
  tenant is not derivable from the request.
- [ADR-0015](0015-management-core-validation-via-validators.md) — the management validator seam that enforces the
  create-time tenant scope.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — the reference-direction rule the
  abstraction/implementation split honors.
- [ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md) — the superseded shared-selection +
  post-fetch-boundary decision.
- Code: `NCode.Identity.OpenId.Persistence.Tenants.IAmbientTenantAccessor`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.OpenIdDbContext` (global query filters),
  `NCode.Identity.OpenId.Management.Endpoints.TenantScopeEndpointFilter`.

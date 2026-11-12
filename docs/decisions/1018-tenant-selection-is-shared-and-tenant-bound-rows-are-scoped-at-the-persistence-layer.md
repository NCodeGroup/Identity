# 1018. Tenant selection is shared and tenant-bound rows are scoped at the persistence layer

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)

## Context

The OpenID runtime resolves the tenant of a request as a first-class, pluggable concern: a static single tenant, or a
tenant derived from the request host or path. Request-derived tenancy doubles as a security, deployment and
infrastructure boundary, because a deployment reached at `tenant1.example.com` or `/tenant1/…` serves exactly one
tenant. The management API administers those same tenants and their children: clients and every other tenant-bound
resource.

Two forces shape the design.

- The protocol runtime and the management API must never disagree on which tenant a request is, so selection is one
  shared concern, separate from materializing the tenant's contexts
  ([ADR-1036](1036-unified-openid-request-environment.md) holds materialization).
- A scope decision made after a row has been read is too late. A resource is addressed by a globally unique opaque
  identifier ([ADR-1014](1014-server-generated-opaque-resource-ids.md)), so its tenant is knowable only by reading the
  row, and no check before the fetch can gate the specific resource. A logging statement, an early return or a later
  refactor between the read and the comparison can leak another tenant's data, and every call site repeats the risk.
  The fetch itself has to be scoped: "never fetch what you cannot see", not "fetch, then hide".

The management API's answer to a caller without authority is a separate rule,
[ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md). This record holds
persistence scoping only.

## Decision

**Tenant selection is one shared abstraction, and tenant-bound data access is scoped at the persistence layer so that
a cross-tenant row is never materialized.**

### Selection

`ITenantResolver` resolves a request to a `PersistedTenant`. It works over the pluggable `ITenantStrategy`
(static-single, dynamic-by-host, dynamic-by-path) and `TenantResolutionOptions`. Its contracts live in
`NCode.Identity.OpenId.Abstractions` and its default implementation in `NCode.Identity.OpenId.Core`. The protocol
runtime and the management API depend only on the abstraction and receive the implementation through dependency
injection
([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
so they cannot disagree on the tenant of a request.

### The ambient scope

`IAmbientTenantAccessor` (in `NCode.Identity.OpenId.Persistence.Abstractions`; its default,
`DefaultAmbientTenantAccessor`, in `NCode.Identity.OpenId.Core`) holds the tenant scope of the current flow.
`BeginScope(tenantId)` stores the identifier in an `AsyncLocal<T>`, so the scope flows across asynchronous calls and
reaches the `OpenIdDbContext` that an `IDbContextFactory` creates from the root provider, outside the current
dependency-injection scope. With no scope open, `TenantId` is `null`: the accessor is unscoped.

### The query filter

`OpenIdDbContext` applies, by interface, a global query filter to every `ISupportTenantEntity`. The filter reads
"the ambient tenant is unset, or the row's denormalized, indexed `NormalizedTenantId` equals it". It is one
indexed-column predicate, and because it is applied by interface, a newly added tenant-child entity is scoped
automatically and the framework stays decoupled from the concrete tenant entity
([ADR-1052](1052-entity-framework-persistence-is-a-reusable-framework-that-domain-slices-plug-into.md)).

With a scope open, no query returns a row of another tenant, so the row is never materialized. With no scope open, the
filter is a no-op, which is how the runtime and central tenant administration read.

`TenantEntity`, `TeamEntity` and `PlatformEntity` are not `ISupportTenantEntity`, so they stay unscoped and resolvable
by identifier. A tenant's child rows, its secrets included, are scoped like every other tenant child; tenant
resolution and tenant management read them with no scope open. The platform has one settings row and no secrets
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)).

### Opening the scope in the management API

The management API's tenant-plane group (`/api/tenants/{tenantId}`) opens the scope from the route's `tenantId` value
through the internal `TenantScopeEndpointFilter`, independent of the request's resolved tenant (route-driven scope,
[ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md) and
[ADR-1042](1042-management-api-route-driven-tenant-scope.md)). The filter answers `404` when the segment is missing.
It reads only the route value and makes no claim that the tenant exists. Control-plane routes carry no `{tenantId}` and
run unscoped.

### Writes and reads of an existing resource

- A tenant-bound create takes its owning tenant from the scope, which is the route, never from the request body. The
  request carries no tenant identifier at all.
- Every read, update or delete of an existing tenant-bound resource loads it by identifier through the filter. The
  management API authorizes the caller at the route's node first
  ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)); to an authorized
  caller, a resource of another tenant is `404`, because the filter never loads it. The answer is given before
  validation runs, so the existence of a resource in another tenant cannot be probed.

### Failing closed

`OpenIdDbContext` requires an `IAmbientTenantAccessor`, so a missing registration fails at startup instead of running
unscoped. Direct construction in tests and tooling passes an explicit unscoped accessor.

## Options considered

- **A post-fetch boundary that compares a loaded resource's tenant to the request's tenant.** Rejected: it materializes
  the row, sensitive material included, before the scope decision, and the comparison repeats at every call site.
- **Tenant-scoped getters at each call site**, such as a lookup that takes the ambient tenant and the identifier.
  Rejected as the mechanism: it is correct only while every call site remembers the scope, and a single omission leaks.
- **A pre-fetch endpoint check on the resource's tenant.** Rejected: an opaque identifier does not reveal its tenant, so
  there is nothing to check before the row is read.
- **A blanket authorization handler over tenant-owned resources.** Rejected: it runs after the row is loaded, and the
  `403` it would produce for a resource of another tenant would reveal that the resource exists.
- **The management API reusing the runtime's tenant provider.** Rejected: it drags the protocol runtime into the
  management API, against the one-capability package boundary of
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md).
- **An ambient scope plus a global query filter by interface (chosen).** No query can return a cross-tenant row and no
  call site can forget the scope, while the runtime and central administration stay unscoped.

## Consequences

- A new tenant-bound resource family is isolated by making its entity an `ISupportTenantEntity`, with no per-endpoint
  check.
- A cross-tenant row is never read, so a later logging statement, early return or refactor has nothing to leak.
- The runtime and the management API share one selection path and cannot resolve different tenants for a request.
- A missing `IAmbientTenantAccessor` registration is a startup failure, never a silent loss of isolation.
- The filter needs the denormalized `NormalizedTenantId` column and an index on every tenant-child table.
- Code that reads tenant-bound rows without a scope sees every tenant's rows; the unscoped paths are the runtime's
  resolution, tenant management and tooling, and each is explicit.

## References

- `NCode.Identity.OpenId.Abstractions`: `ITenantResolver`, `ITenantStrategy`, `TenantResolutionOptions`
- `NCode.Identity.OpenId.Persistence.Abstractions`: `IAmbientTenantAccessor`, `ISupportTenantEntity`
- `NCode.Identity.OpenId.Core`: `DefaultAmbientTenantAccessor`
- `NCode.Identity.OpenId.Persistence.EntityFramework`: `OpenIdDbContext`
- `NCode.Identity.OpenId.Management`: `TenantScopeEndpointFilter`
- [ADR-1036](1036-unified-openid-request-environment.md), [ADR-1042](1042-management-api-route-driven-tenant-scope.md),
  [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md),
  [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)
- [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md), decision 5:
  the owning tenant comes from the context, never the request

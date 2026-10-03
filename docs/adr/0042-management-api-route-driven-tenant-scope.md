# 42. The management API is plane-aware; tenant-bound resources nest under the tenant and drive the ambient scope from the route

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

The management API is a single, central-admin surface mounted at a fixed `/api` group
([ADR-0024](0024-control-plane-and-per-tenant-planes.md)), distinct from the OpenID protocol endpoints, which the
runtime mounts under the tenant route ([ADR-0009](0009-endpoint-families-own-their-route-group.md)). Tenant-bound data
access is kept leak-proof by a persistence-layer global query filter keyed on an ambient tenant scope
([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)): every `ISupportTenantEntity` row is visible
only when the ambient scope is unset (central-admin) or equals the row's tenant, so a cross-tenant row is never
materialized. The tenant row itself is not scoped, so tenants and servers stay globally addressable.

The ambient scope for an `/api` request is opened by the shared environment filter from the **protocol-resolved**
tenant — the tenant the request's host/path/static strategy resolves to. On a central-admin surface that is the wrong
source, and it produces three defects:

- **The scope's meaning changes with the hosting strategy.** Under static-single it is always the one workload tenant;
  under dynamic-by-host it is the host's tenant; under dynamic-by-path it is read from a route value named `tenantId` —
  which is the same name the management resource uses. So "the current tenant" on an `/api` request is strategy-
  dependent, and under dynamic-by-path it collides, by name, with the resource being addressed.
- **A tenant-bound family can only ever reach the request's resolved tenant.** Clients are mounted at
  `/api/clients/{clientId}` and read under the protocol-resolved scope, so a `GlobalAdmin` cannot administer another
  tenant's client without changing which tenant the request resolves to — there is no route by which to say _which_
  tenant's client.
- **A tenant's own merged ("effective") settings are not addressable by id.** Reconstructing a tenant's effective
  settings view requires the server baseline merged with that tenant's persisted overrides; reading the ambient
  `OpenIdContext.Tenant` returns the resolved tenant, not the one named in the route, so the central-admin surface
  cannot preview root vs. workload tenant settings.

[ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md) (superseded by
[ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)) considered nesting management resources under a
tenant route segment and rejected it, on the grounds that globally-unique opaque ids
([ADR-0014](0014-server-generated-opaque-resource-ids.md)) make the segment redundant for _addressing_. That reasoning
predates the persistence query filter: with ADR-0018 the route segment is no longer about addressing — it is the
**fail-closed source of the ambient scope**, and the only source that is deterministic across every tenant strategy.

## Decision

**The management surface is plane-aware. Tenant-bound resources nest under their tenant, and the ambient tenant scope
is driven by the route — not by protocol resolution. Control-plane resources carry no tenant segment and run unscoped.**

The rule, applied by a single reusable filter on the `/api` group:

> Materialize the OpenID environment and server for every management request, but **do not resolve a protocol tenant**.
> Open an `IAmbientTenantAccessor` scope from the route's `{tenantId}` value **iff the matched route carries one**;
> otherwise leave the request unscoped.

Everything follows from that rule:

- **Tenant-bound families nest under `/api/tenants/{tenantId}/…`.** A tenant's own settings and secrets already nest
  there; clients (and their settings, secrets, effective-settings, and owners) move to
  `/api/tenants/{tenantId}/clients/{clientId}/…` to match. The `{tenantId}` segment is what feeds the ambient scope, so
  nesting is a security mechanism, not a cosmetic hierarchy.
- **The route id drives scope _and_ authorization together.** The same `{tenantId}` opens the `BeginScope` and is the
  `(Tenant, tenantId)` node the ownership handler evaluates ([ADR-0034](0034-persisted-role-assignments-and-ownership.md)),
  so the data scope and the authority check can never diverge. A caller reaching `/api/tenants/{t}/clients/{c}` must
  hold authority at `(Tenant, t)` (or `GlobalAdmin` at the server root), and the query filter confines the read to `t`
  even if a handler forgets to filter — fail-closed by construction.
- **Control-plane endpoints carry no `{tenantId}` and run unscoped.** `GET/POST /api/tenants` and all `/api/servers/…`
  endpoints have no tenant segment, so the filter opens no scope and they operate globally, as `GlobalAdmin` at
  `(Server, rootTenantId)` intends. A server id is never a tenant id, so server endpoints never open a tenant scope.
- **Effective settings are a by-id merge hierarchy.** Each tier reconstructs the resolved view from the route id and
  the persisted rows, mirroring exactly what the runtime resolves ([ADR-0038](0038-setting-merge-lattice-duals.md)):
  server = the server's own resolved collection; tenant = `server.Merge(tenantPersisted)`; client =
  `tenantEffective.Merge(clientPersisted)`. No tier reads the ambient `OpenIdContext.Tenant`.
- **The control plane / workload tenant split is unchanged.** The root (control-plane) tenant remains the issuer of
  control-plane tokens and the `(Server, rootTenantId)` authorization anchor
  ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md),
  [ADR-0034](0034-persisted-role-assignments-and-ownership.md)); `default` remains the workload tenant. This ADR changes
  how the management surface is routed and scoped, not the tenancy model.
- **Route-group composition is declarative and unified across both planes.** Groups are named `IEndpointGroup` nodes
  (`Name`, `ParentName`, `Prefix`, and a `Configure` that attaches filters); endpoint providers register **keyed by
  group name**, and the route builder materializes each group parent-first (memoized) and maps that group's keyed
  `IEndpointProvider` children into it. Both the management hierarchy above and the OpenID protocol surface (a single
  `openid` group with an empty prefix that installs the environment and exception filters) compose through this one
  mechanism, replacing the imperative `IEndpointGroupProvider` tier of [ADR-0009](0009-endpoint-families-own-their-route-group.md).

## Options considered

- **Keep protocol-resolved scope on `/api` (status quo).** Rejected: strategy-dependent scope, the dynamic-by-path
  param-name collision, and no way to administer or preview a tenant other than the one the request resolves to.
- **Rename the management resource parameter** (e.g. `/api/tenants/{id}`) to dodge the dynamic-by-path collision.
  Rejected: it treats the symptom, is inconsistent with the already-nested `/api/tenants/{tenantId}/secrets/{secretId}`,
  and leaves the scope sourced from protocol resolution.
- **Nest tenant-bound families and drive the scope from the route (chosen).** Deterministic across every strategy,
  fail-closed via the existing query filter, consistent with the resource-ownership hierarchy and the already-nested
  secrets/settings, and it reconciles ADR-0017's rejected nesting with ADR-0018's persistence-layer scope: the segment
  is no longer about addressing, it is the scope source.

## Consequences

- A `GlobalAdmin` can administer any tenant's clients (and preview any tenant's effective settings) by route, while the
  persistence query filter guarantees no cross-tenant row is materialized — the route id is both the scope and the
  authority check.
- Client management routes change from `/api/clients/{clientId}` to `/api/tenants/{tenantId}/clients/{clientId}`; this
  is a breaking change to the pre-release management surface ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- The management `/api` group runs a management-specific filter (environment + server, route-driven scope) rather than
  the protocol environment filter, so management requests no longer resolve or depend on a protocol tenant; the
  dynamic-by-path param-name coupling is gone.
- A tenant-bound create derives its owning tenant from the ambient scope (the route `{tenantId}`), keeping ADR-0018's
  "the request carries no tenant identifier in the body" property while making the owning tenant explicit in the path.
- [ADR-0040](0040-effective-settings-preview-endpoints.md)'s rationale for the tenant effective-settings endpoint being
  "deliberately not by-id" is superseded: all three effective-settings endpoints are now by-id and form one hierarchy.
- The server/root-tenant identity duplication (the `(Server, rootTenantId)` node is typed `Server` but keyed by the
  root _tenant_ id) is left intact; unifying it is a separate, optional follow-up.
- The declarative named-group mechanism (`IEndpointGroup` + keyed providers) replaces the imperative
  `IEndpointGroupProvider` tier across **both** the management and protocol surfaces, so route prefixes and shared
  filters are declared once per group and there is a single composition mechanism rather than two.

## References

- Code: `ManagementEndpointGroupProvider`, the management environment/scope filter, `OpenIdDbContext.ApplyTenantScopeFilter`,
  `IAmbientTenantAccessor` / `DefaultAmbientTenantAccessor`, `ClientApiEndpointHandler`, `TenantApiEndpointHandler`,
  `ServerApiEndpointHandler`, `OwnershipHandler`.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the persistence-layer ambient-scope query
  filter this builds on.
- [ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md) — the superseded boundary that
  rejected nesting; this ADR revisits that rejection under ADR-0018's mechanism.
- [ADR-0024](0024-control-plane-and-per-tenant-planes.md),
  [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md),
  [ADR-0034](0034-persisted-role-assignments-and-ownership.md) — the control-plane / workload split and the authorization
  anchor the plane-aware routing follows.
- [ADR-0040](0040-effective-settings-preview-endpoints.md) — the effective-settings endpoints, whose tenant "not by-id"
  rationale this supersedes.
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoint families own their route group.

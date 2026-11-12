# 1191. A management request is authenticated by the control-plane tenant and scoped by its route's tenant

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The management API is the `/api` route group (`ManagementRootGroup`, `RoutePrefix = "/api"`), separate from the
protocol endpoints, which the runtime mounts under the tenant route
([ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)).
It belongs to the control plane
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)). Two
questions decide how a call is handled, and the answers must not depend on the hosting strategy:

- **Which tenant authenticates the caller?** The access token is validated through `IIssuedTokenValidator` against the
  `OpenIdContext` it is given
  ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)), so the tenant that context carries
  must be the one that issued the token. A tenant resolved from the request's host or path is not: under
  `DynamicByPath`, `/api` is beneath no tenant route and a collection route has no `tenantId` to resolve, and under
  `DynamicByHost`, the management host `manage.<apex>`
  ([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md))
  is no tenant's host.
- **Which tenant's data does a call act on?** Tenant-bound data is kept leak-proof by a persistence-layer query filter
  keyed on an ambient tenant scope
  ([ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md)): a
  tenant-bound row is visible only when the scope is unset or equals the row's tenant. The scope's source must be
  deterministic across every strategy. A scope taken from the protocol-resolved tenant means "the current tenant" varies
  by strategy, collides by name with the resource's own `tenantId` under `DynamicByPath`, and offers no route by which
  to administer another tenant's client.

## Decision

A management request is authenticated by the control-plane tenant and scoped by the tenant its route names. The two
are independent: the first is the security domain that issued the caller's token, the second selects the data.

1. **The control-plane tenant authenticates every management request.** `ManagementEnvironmentEndpointFilter`, the
   outermost filter of the `/api` group, creates the request's `OpenIdContext` for the control-plane tenant through
   `IOpenIdContextFactory.CreateForControlPlaneAsync`, whatever tenant the host or path addresses. The authentication
   filter, the endpoints and `OwnershipHandler` read that context, so a caller is authenticated, and its principal
   resolved, by the tenant that issued its token. A workload tenant's token never authenticates the API; control-plane
   principals administer a workload tenant through role assignments at that tenant's node
   ([ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md)).
2. **The strategy names the control-plane tenant.** `ITenantStrategy.GetControlPlaneTenantId` names it from
   configuration alone, without reading the store, and `ITenantStrategy.ResolveControlPlaneTenantAsync` resolves it.
   `ITenantResolver` delegates both to the selected strategy. A dynamic strategy (`DynamicByPath`, `DynamicByHost`)
   answers the root tenant (`OpenId:TenantResolution:Root:TenantId`), creating it on demand like a request that reaches
   the root tenant's address, and answers `404` when it is disabled. `StaticSingle` answers its own tenant, because it
   serves no other and no other tenant can issue the caller's token. A custom strategy overrides both members together.
3. **The control-plane tenant is materialized like a request's tenant.**
   `IOpenIdTenantFactory.CreateControlPlaneTenantAsync` probes the shared tenant cache with `GetControlPlaneTenantId`
   before any store read and, on a miss, seeds and materializes the tenant as `CreateTenantForRequestAsync` does
   ([ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md)), so a
   tenant reached through the management API and through its own address shares one cache entry.
4. **Tenant-bound families nest under `/api/tenants/{tenantId}/…`.** `TenantManagementGroup` holds clients, grants,
   audit records, resource servers, sessions and principals, and its child `ClientManagementGroup` holds
   `/clients/{clientId}` and the client grants.
5. **The route's tenant opens the data scope.** `TenantScopeEndpointFilter` on `TenantManagementGroup` opens
   `IAmbientTenantAccessor.BeginScope(tenantId)` from the route value and answers `ManagementProblems.ResourceNotFound()`
   when the route has no `tenantId`.
6. **The route's tenant is also the node the caller is authorized at.** The same `tenantId` is the scope of the
   persistence query filter and the tenant node that authorization evaluates
   ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)), so data scope
   and authority cannot diverge, and a handler that forgets to filter still reads only that tenant.
7. **Routes on the root group open no tenant scope.** They act on unscoped platform, team and tenant rows:
   `/api/platform/…`, `/api/teams/…` (tenant create and list under `/api/teams/{teamId}/tenants`), the machine-client
   routes, `GET /api/tenants`, the tenant row's own routes under `/api/tenants/{tenantId}` (settings, secrets,
   encryption keys, owners, admins), `/api/me` and `/api/capabilities`.
8. **A tenant-bound create takes its owning tenant from the route, never the body.** An operation that runs protocol
   machinery in the addressed tenant creates that tenant's own context with `CreateForTenantAsync`.

## Options considered

- **Scope `/api` from the protocol-resolved tenant.** Rejected: the scope depends on the strategy, its `tenantId`
  collides with the resource's under `DynamicByPath`, and no other tenant is addressable.
- **Rename the route parameter to avoid the collision.** Rejected: it treats a symptom, and the scope would still come
  from protocol resolution.
- **A management option naming the authenticating tenant.** Rejected: the root tenant's identifier is already
  configuration, and under `StaticSingle` a tenant other than the served one cannot issue a token, so a second setting
  is redundant or wrong. A strategy that needs another answer overrides both control-plane members.
- **Serve the API beneath the root tenant's route or host.** Rejected: it ties the API's address to the strategy and
  leaves the `manage.<apex>` host unserved.
- **Require the caller to use the issuing tenant's address.** Rejected: the routes already carry the tenant they act
  on, so the address would encode the tenant twice, and a collection route has no tenant to address.
- **Authenticate by the control-plane tenant and scope by the route (chosen).** Deterministic across every strategy,
  fail-closed through the existing query filter, and consistent with the ownership hierarchy.

## Consequences

- The management API answers under every strategy: on any host under `DynamicByHost` (including `manage.<apex>`) and
  at `/api` under `DynamicByPath`, with tokens the root tenant issues. Integration tests drive both dynamic strategies;
  every other management integration test drives `StaticSingle`.
- A token a workload tenant issues does not authenticate the management API. Administration of a workload tenant is
  delegated to control-plane principals through role assignments.
- An administrator can act on any tenant's clients by route, while the persistence query filter guarantees that no
  cross-tenant row is materialized.
- A custom `ITenantStrategy` implements both control-plane members.

## References

- Code: `ManagementRootGroup`, `TenantManagementGroup`, `ClientManagementGroup`, `ManagementEnvironmentEndpointFilter`,
  `TenantScopeEndpointFilter`, `ITenantStrategy`, `ITenantResolver`, `IOpenIdTenantFactory`, `IOpenIdContextFactory`,
  `IAmbientTenantAccessor`, `OwnershipHandler`.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — the
  control plane and the platform root.
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md) — the
  ambient-scope query filter.
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)
  — the management host.
- [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) — one
  provisioning pipeline.
- [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md) — issued-token validation.
- [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) — authorization at
  the named node.
- [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) — role assignments at a node.

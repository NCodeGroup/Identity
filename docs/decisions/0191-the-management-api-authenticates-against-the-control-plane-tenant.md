# 191. The management API authenticates against the control-plane tenant

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The management `/api` surface materialized its OpenID context through `IOpenIdContextFactory.CreateForRequestAsync`,
so the tenant that validated the caller's access token was whichever tenant the protocol strategy resolved from the
request. Under `StaticSingle` that is the only tenant there is. Under the dynamic strategies it is wrong:

- under `DynamicByPath`, `/api` is not beneath any tenant route, so a collection route such as `POST /api/tenants` has
  no `tenantId` route value to resolve, and `/api/tenants/{tenantId}/…` resolved the **target** tenant — the caller's
  token, issued by the control plane, was validated by a tenant that never issued it;
- under `DynamicByHost`, the management host `manage.<apex>`
  ([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)) is no tenant's host, so every call
  returned `404`, and reaching the API on a workload tenant's host made that tenant authenticate the caller.

The management plane belongs to the control plane ([ADR-0024](0024-control-plane-and-per-tenant-planes.md)), and the
route's `tenantId` selects the data a call acts on ([ADR-0042](0042-management-api-route-driven-tenant-scope.md)), not
the security domain that authenticates it. The access token is validated through `IIssuedTokenValidator` against the
`OpenIdContext` it is given ([ADR-0167](0167-the-server-validates-the-tokens-it-issued-under-one-policy.md)), so the
fix is only which tenant that context carries.

## Decision

1. **The strategy names the control-plane tenant.** `ITenantStrategy.ResolveControlPlaneTenantAsync` resolves the
   tenant that authenticates the management API whatever tenant the request addresses. A dynamic strategy returns the
   root tenant (`OpenId:TenantResolution:Root:TenantId`), creating it on demand like a request that reaches the root
   tenant's address, and `404` when it is disabled. `StaticSingle` returns its own tenant: it serves no other, so no
   other tenant can issue the caller's token. `ITenantStrategy.GetControlPlaneTenantId` names the same tenant from
   configuration alone, without reading the store. A custom strategy decides for itself, overriding both together;
   `ITenantResolver` delegates to the selected strategy.
2. **The control-plane tenant is materialized like a request's tenant.**
   `IOpenIdTenantFactory.CreateControlPlaneTenantAsync` probes the shared tenant cache with `GetControlPlaneTenantId`
   before any store round-trip, and only on a miss resolves the tenant and then seeds and materializes it through the
   same path as `CreateTenantForRequestAsync`
   ([ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)), so a tenant reached through
   the management API and through its own address shares one cache entry.
3. **The management API publishes the control-plane context.** `IOpenIdContextFactory.CreateForControlPlaneAsync`
   creates and publishes the request's `OpenIdContext` for that tenant, and `ManagementEnvironmentEndpointFilter` calls
   it in place of `CreateForRequestAsync`. The authentication filter, the endpoints, and `OwnershipHandler` read that
   context, so a caller is authenticated, and its principal resolved, by the tenant that issued its token. The route's
   `tenantId` still opens the ambient tenant scope through `TenantScopeEndpointFilter` and only scopes data; an
   operation that must run protocol machinery in the addressed tenant keeps creating that tenant's own context with
   `CreateForTenantAsync`.

## Options considered

- **A management option naming the authenticating tenant.** Rejected: the root tenant's identifier is already
  configuration, and under `StaticSingle` any tenant other than the served one could not issue a token, so a second
  setting could only be redundant or wrong. A strategy that needs another answer overrides
  `GetControlPlaneTenantId` and `ResolveControlPlaneTenantAsync`.
- **Serve the management API beneath the root tenant's route or host.** Rejected: it would tie the API's address to
  the strategy (a path prefix under `DynamicByPath`, the root host under `DynamicByHost`) and still leave the
  `manage.<apex>` host of ADR-0131 unserved.
- **Keep resolving from the request and require the caller to call the API on the issuing tenant's address.**
  Rejected: the API's routes already carry the tenant they act on, so the address would encode the tenant twice, and
  a collection route has no tenant to address.

## Consequences

- The management API answers under every strategy: on any host under `DynamicByHost` (including `manage.<apex>`) and
  at `/api` under `DynamicByPath`, with tokens the root tenant issues. Integration tests drive both dynamic strategies;
  every other management integration test drives `StaticSingle`.
- A token a workload tenant issues no longer authenticates the management API under a dynamic strategy. Administration
  of a workload tenant is delegated through role assignments at that tenant's node
  ([ADR-0034](0034-persisted-role-assignments-and-ownership.md)) to principals of the control-plane tenant.
- `ITenantStrategy`, `ITenantResolver`, `IOpenIdTenantFactory`, and `IOpenIdContextFactory` each gain a member, a
  breaking change for a custom implementation, accepted under the pre-release posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## References

- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the control plane and the per-tenant planes
- [ADR-0042](0042-management-api-route-driven-tenant-scope.md) — the route drives the management API's tenant scope
- [ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md) — the management host
- [ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md) — one provisioning pipeline
- [ADR-0167](0167-the-server-validates-the-tokens-it-issued-under-one-policy.md) — issued-token validation

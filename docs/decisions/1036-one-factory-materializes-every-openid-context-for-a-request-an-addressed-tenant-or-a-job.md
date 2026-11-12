# 1036. One factory materializes every OpenID context, for a request, an addressed tenant or a job

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

An OpenID request is served against an environment: the platform, the tenant the request addresses (with its effective
settings, merged from the platform, its team or teams and the tenant, and its secrets), and the mediator through which
handlers run. Protocol endpoints (authorize, token, userinfo, introspection, discovery) and the management API both need
that environment. Per-tenant resolution knobs, setting validation and an "effective settings at this level" view
([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md),
[ADR-1035](1035-federated-principals-and-identity-resolution.md)) need it on both surfaces.

Three forces shape how it is built:

- Tenant selection, tenant materialization (the settings-bearing `OpenIdTenant`) and the ambient tenant scope that the
  persistence query filter reads ([ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md))
  are one concern. They must be one flow, or the surfaces drift apart.
- Tenant materialization is heavyweight: issuer, base address, merged settings provider, secret-key provider and
  periodic refresh wiring. It is cached by tenant identifier, so the warm path must not pay a tenant-store round-trip
  just to learn the identifier it already knows from the request.
- Some callers act on a tenant the request does not address: the management API authenticates against the control-plane
  tenant whichever tenant its route names, an administrative operation drives protocol machinery for a route-addressed
  tenant, and a background job runs outside any request. The session revoke at `/api/tenants/{tenantId}/sessions`
  delegates to `ISessionService`, which is bound to an `OpenIdContext`: it keys the session grant on the context's tenant
  and dispatches the termination seam ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)) through its mediator, and
  the back-channel logout handler signs the logout token with that tenant's issuer and keys. The request's own context
  carries the management host's tenant, so using it would key grants and sign logout tokens in the wrong tenant.

## Decision

**One factory, `IOpenIdContextFactory`, materializes every `OpenIdContext`, and it has one path for each situation: the
request's tenant, the control-plane tenant, an explicitly addressed tenant and a background job. The environment is
shared infrastructure of the core OpenID slice that every HTTP surface runs the same way.**

### The context and where it lives

An OpenID request runs against an `OpenIdContext` that exposes `Http`, `Environment`, `Platform` (`OpenIdPlatform`),
`Tenant` (`OpenIdTenant`, whose settings merge platform, team or teams, and tenant), `Mediator`, `PropertyBag` and
`EndpointName`. The abstractions (`OpenIdContext`, `OpenIdPlatform`, `OpenIdTeam`, `OpenIdTenant`,
`IOpenIdContextFactory`, `IOpenIdTenantFactory`) live in `NCode.Identity.OpenId.Abstractions`. Their defaults live in
`NCode.Identity.OpenId.Core` and are registered by `AddOpenIdCoreLibrary`. The platform and team rows are not
tenant-scoped ([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)).

`IOpenIdContextFactory` builds from an `HttpContext`, so the core slice takes the ASP.NET Core HTTP dependency. Every
consumer of the environment is an HTTP surface, so the dependency belongs there, and the authentication slice does not
have to host the environment for the management slice to reach it.

The setting catalog (`OpenIdSettingNames`, `OpenIdSettingKeys`) is shared in the abstractions. The core slice registers
the surface-neutral descriptors, and the authentication slice contributes its protocol descriptors (the
client-authentication methods a tenant supports, token formats) as an additional source. No descriptor is duplicated.

Per-tenant behavioral knobs are settings on the resolved tenant, overridable below the platform, rather than
process-wide start-up options.

### Four creation paths

| Method | Tenant | Ownership |
| --- | --- | --- |
| `CreateForRequestAsync` | the request's resolved tenant | published on the request |
| `CreateForControlPlaneAsync` | the control-plane tenant, which authenticates the management API whatever tenant it addresses | published on the request |
| `CreateForTenantAsync` | an explicit tenant id | caller-owned and disposable, never published on the request |
| `CreateForBackgroundAsync(serviceProvider, tenantId, …)` | an explicit tenant id, outside any request, over an `HttpContext` that carries only that provider's services | caller-owned |

`OpenIdEnvironmentEndpointFilter` on the protocol group and `ManagementEnvironmentEndpointFilter` on the `/api` group
build the context once per request and publish it as an `IOpenIdContextFeature`. Endpoints and services read it through
`HttpContext.GetOpenIdContext()` and never build it themselves, so a new endpoint needs no context plumbing.

### The request path probes the tenant cache before the store

`ITenantResolver.TryGetTenantId(HttpContext, out tenantId)`, and the same member on `ITenantStrategy`, is a store-free,
non-throwing probe that returns the tenant identifier when the strategy can know it from the request alone:

- static-single returns its configured tenant;
- dynamic-by-path returns the route value;
- dynamic-by-host returns `false`, because mapping a host to a tenant needs the store.

`DefaultOpenIdTenantFactory.CreateTenantForRequestAsync` probes `IOpenIdTenantCache` with that identifier before any
store read. A hit serves the cached tenant. Only a miss, or a probe that declines, calls `ResolveTenantAsync`, which
answers `404` for a missing or disabled tenant, and a disabled team answers `404` before the seed. A disabled tenant is
therefore never cached. A miss seeds the tenant ([ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md))
and caches it. `CreateControlPlaneTenantAsync` serves the control-plane tenant through the same cache.

A cached tenant lives at most `OpenIdTenantOptions.TenantCacheLifetime` (15 minutes, absolute). A committed change
evicts it on every replica through the `tenant-changed` announcement
([ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md),
[ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)).

### The explicit path acts on the tenant as stored

`CreateTenantByIdAsync` loads the addressed tenant straight from `ITenantStore` (`404` when it is missing or disabled, or
its team is disabled) and materializes it under its own stored issuer. It does not seed the tenant, and it neither reads
nor writes the tenant cache, so an administrative operation acts on the tenant as stored at that moment and an
issuer-derived materialization never aliases a request-derived one.

Management operations that drive protocol machinery for a route-addressed tenant use `CreateForTenantAsync`. The session
revoke delegates to the context-bound `ISessionService`, so grant revocation, back-channel logout signed by that tenant
and session audit run in its security domain. The principals and local-account endpoints do the same. A session list
runs on the request's context under the route's tenant scope.

## Options considered

- **Materialize one shared environment for every surface through one factory (chosen).** Management and protocol
  endpoints are peers over the same environment, settings reach both, and the explicit, control-plane and background
  paths reuse the same materialization helpers.
- **Keep the environment in the authentication slice.** Rejected: it points the management slice at a protocol slice,
  which inverts the layering, and leaves tenant resolution, materialization and scoping as separate mechanisms.
- **Provide only a headless effective-settings provider by tenant id.** Rejected: management still has no materialized
  tenant, so the split stays and the provider is a band-aid over it.
- **Give `ISessionService` tenant-parameterized overloads.** Rejected: the termination seam still needs a materialized
  tenant to sign the logout token, so the overloads collapse into a materialized context while forking the service
  surface. `ISessionService` stays context-bound.
- **Re-validate the disabled flag on every cache hit.** Rejected: it restores the per-request store read. Bounding
  staleness by the announcement and the cache lifetime is the accepted trade-off of caching a tenant at all.
- **Cache a host-to-identifier map so dynamic-by-host can probe store-free.** Rejected: it adds a second cache with its
  own invalidation and staleness surface for one strategy. Host-based tenancy keeps its single store read.

## Consequences

- The core OpenID slice owns the environment and is web-aware. The authentication slice keeps the protocol endpoints and
  its protocol descriptors, and builds on the shared environment.
- Management handlers read the resolved tenant's merged settings and secrets the same way protocol handlers do.
- Static-single and dynamic-by-path requests serve a warm tenant with no tenant-store round-trip. Dynamic-by-host pays
  one store read per request.
- A tenant disabled after it was cached keeps serving on a replica until the announcement evicts it, and for at most the
  cache lifetime on a replica that missed it. `ResolveTenantAsync` stays the authoritative validation on the load path.
- An administrative session revoke notifies relying parties and audits per session, as a protocol logout does. Any
  management operation that must drive protocol machinery for a route-addressed tenant materializes that tenant's
  context the same way.
- Every explicit materialization loads the tenant fresh, which suits the infrequent administrative operations that use
  it.
- A tenant needs an issuer to be materialized under its own identity ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)).
- Smell: an endpoint that builds its own `OpenIdContext`, or a cache read or write on the explicit path, has stepped
  outside this decision.

## References

- Code: `NCode.Identity.OpenId.Abstractions` (`OpenIdContext`, `OpenIdPlatform`, `OpenIdTeam`, `OpenIdTenant`,
  `IOpenIdContextFactory`, `IOpenIdTenantFactory`, `OpenIdSettingNames`, `OpenIdSettingKeys`);
  `NCode.Identity.OpenId.Core` (`DefaultOpenIdTenantFactory`, `OpenIdEnvironmentEndpointFilter`,
  `ManagementEnvironmentEndpointFilter`, `OpenIdTenantOptions`, `ITenantResolver`, `ITenantStrategy`,
  `StaticSingleTenantStrategy`, `DynamicByPathTenantStrategy`, `DynamicByHostTenantStrategy`).
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md): the ambient tenant scope
  the persistence filter reads.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the control plane and the per-tenant planes.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md): the data-driven,
  per-tenant control philosophy.
- [ADR-1035](1035-federated-principals-and-identity-resolution.md): the resolution and linking knobs that are tenant
  settings.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md): the session-termination seam the administrative surface drives.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the service,
  strategy and mediator model the factory follows.
- [#63](https://github.com/NCodeGroup/identity/issues/63): the administrative sessions surface.

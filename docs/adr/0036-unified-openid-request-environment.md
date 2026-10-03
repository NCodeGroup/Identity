# 36. A single OpenID request environment for every HTTP surface

- **Status:** Accepted
- **Date:** 2026-10-02
- **Deciders:** NCode Group

## Context

An OpenID request is served against an **environment**: the server, the tenant resolved for the request (with its
effective, server-merged settings and secrets), and the mediator through which handlers run. The protocol endpoints
(authorize, token, userinfo, introspection, discovery, …) materialize that environment at the top of every request
through `IOpenIdContextFactory`, which resolves the tenant and builds an `OpenIdContext` exposing `Server`, `Tenant`
(an `OpenIdTenant` whose `SettingsProvider` is the server-defaults-merged-with-tenant-overrides view), `Environment`,
and `Mediator`.

The management API does not share that environment. It runs a thinner, separate mechanism — a `TenantScopeEndpointFilter`
that opens an ambient **tenant-id scope** for the persistence query filter ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md))
— and never materializes an `OpenIdContext` or a settings-bearing `OpenIdTenant`. The result is three overlapping
tenant mechanisms grown apart over time:

- **tenant resolution** (which persisted tenant a request belongs to) lives in the core slice as `ITenantResolver` and
  its strategies;
- **tenant materialization** (the rich `OpenIdTenant` with merged settings and secrets) lives in the authentication
  slice as `IOpenIdTenantFactory`;
- **ambient tenant scoping** (the id used by the persistence filter) is a third path used by the management filter.

Two costs follow. First, the environment abstractions (`OpenIdContext`, `OpenIdServer`, `OpenIdTenant`, the context and
tenant factories, the settings catalog and its descriptor/merge machinery) sit in the **authentication** slice, above
the management slice, so management cannot reach them — a capability that the data-driven, per-tenant posture
([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) and work such as federated-principal resolution
([ADR-0035](0035-federated-principals-and-identity-resolution.md)) increasingly need on both surfaces (per-tenant
resolution knobs, setting validation, an "effective settings at this level" view). Second, the same concept is realized
three different ways, which is a maintenance and correctness hazard.

## Decision

**The per-request OpenID environment is shared infrastructure that every HTTP surface materializes the same way. The
environment abstractions and their defaults move down into the core OpenID slice, and the three tenant mechanisms are
consolidated into one environment pipeline that both the protocol endpoints and the management API run.**

### What moves down

The environment abstractions — `OpenIdContext`, `OpenIdServer`, `OpenIdTenant`, `IOpenIdContextFactory`,
`IOpenIdTenantFactory` — and the OpenID settings catalog (`OpenIdSettingNames`, `OpenIdSettingKeys`) move into
`NCode.Identity.OpenId.Abstractions`. Their default implementations — the context and tenant factories, the settings
descriptor source, the server/root settings sources, and the server↔tenant merge wiring — move into
`NCode.Identity.OpenId.Core` and are registered by `AddOpenIdCoreLibrary`.

The environment is intrinsically an HTTP-transaction concept: `IOpenIdContextFactory` builds from an `HttpContext`, so
the core slice takes an ASP.NET Core HTTP dependency. This is accepted — every consumer of the environment is an HTTP
surface, and the authentication slice already carried that dependency.

### One environment pipeline

Tenant resolution, tenant materialization, and ambient tenant scoping are consolidated behind the single context/tenant
factory in the core slice: resolving a request's tenant, materializing its `OpenIdTenant` (settings and secrets merged
server→tenant), opening the ambient scope the persistence filter reads ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)),
and producing the `OpenIdContext` are one flow. The management API runs that same flow; the former
`TenantScopeEndpointFilter` is folded into it rather than kept as a parallel mechanism, so a management endpoint sees
the same `OpenIdContext` — down to the settings-bearing tenant — that a protocol endpoint sees.

### The settings catalog is shared; its contributions stay where they belong

The setting-key catalog moves down so both surfaces reference it. The merge primitive already lives in the foundational
settings library (a per-descriptor merge invoked when a setting is read), so nothing about merge semantics moves. The
descriptor registry is an additive, composable collection: the surface-neutral descriptors are registered by the core
slice, while descriptors that depend on protocol concerns (the client-authentication methods a tenant supports, token
formats) continue to be contributed by the authentication slice as an additional source. No descriptor is duplicated,
and no protocol logic is dragged downward.

### Settings over start-up options for per-request knobs

With a settings-bearing tenant available on every surface, per-tenant behavioral knobs are modeled as settings read
from the resolved tenant rather than as process-global start-up options. The principal-resolution and
federated-identity linking knobs introduced in [ADR-0035](0035-federated-principals-and-identity-resolution.md) are
expressed this way, so a tenant can override the server default at runtime.

## Options considered

- **Share one environment pipeline across all HTTP surfaces (chosen).** One context/tenant factory, one tenant concept,
  one place that merges settings and opens the ambient scope; management and the protocol endpoints are peers over the
  same environment. Cohesive, and it unblocks per-tenant settings on the management surface.
- **Promote only the settings catalog and a headless "effective settings by tenant id" provider.** Rejected: it solves
  the immediate settings need but leaves the environment split in place — management still lacks a materialized tenant
  and the three tenant mechanisms persist, so the band-aid remains.
- **Move `OpenIdContext` down but leave management on its own thin filter.** Rejected: relocating the type without
  running the materialization pipeline on the management surface yields no instance there, so the capability is not
  actually delivered.
- **Keep the environment in the authentication slice and have management depend on authentication.** Rejected: it points
  a sibling consumer slice at a protocol slice, inverting the intended layering, and still leaves three tenant
  mechanisms.

## Consequences

- The core OpenID slice (`OpenId.Abstractions` / `OpenId.Core`) becomes web-aware (an ASP.NET Core HTTP dependency) and
  owns the environment; the authentication slice keeps the protocol endpoints and its protocol-specific setting
  descriptors and builds on the shared environment.
- Management requests materialize the full `OpenIdContext`, so management handlers read the resolved tenant's merged
  settings and secrets the same way protocol handlers do; the standalone `TenantScopeEndpointFilter` and the parallel
  ambient-scope path are absorbed into the shared pipeline.
- Tenant resolution, materialization, and scoping are one flow with one extension surface, removing the historical
  three-way split.
- Per-tenant behavioral knobs (including the [ADR-0035](0035-federated-principals-and-identity-resolution.md) resolution
  and linking knobs) become tenant settings with server defaults, overridable at runtime.
- The `OpenIdContext` is threaded explicitly from the pipeline entry to every call site rather than fetched from an
  async-local accessor. It is a bindable minimal-API parameter (a static `BindAsync` reading the request feature the
  context factory publishes) and is exposed via `HttpContext.GetOpenIdContext()`, so a new OpenID endpoint receives the
  context with no extra plumbing. The one exception is the ASP.NET Core ownership authorization handler, which the
  framework invokes without the context in hand and which therefore reads it from the request (the single blessed
  async-local seam).
- Materializing the environment and opening the tenant data-scope are separate concerns: the management `/api` group
  builds the `OpenIdContext` for every endpoint, while the ambient tenant scope is opened only on the tenant-scoped
  subgroups, so cross-tenant endpoints (tenants, servers) run the environment without being restricted to one tenant's
  rows.
- The move is broad — namespaces, `using` directives across the authentication slice, and the published API surface of
  several packages shift. This lands during the pre-release window, where a clean cutover is preferred over compatibility
  shims ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## References

- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the ambient tenant scope the persistence
  filter reads, now opened by the shared environment pipeline.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release posture and data-driven, per-tenant
  control philosophy this consolidation serves.
- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the principal-resolution and linking knobs that
  become tenant settings on the shared environment.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy / mediator extensibility
  model the consolidated pipeline follows.

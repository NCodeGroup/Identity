# 17. Tenant resolution is a shared abstraction; the management API has an optional tenant boundary

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** NCode Group

## Context

The OpenID runtime resolves the ambient tenant for a request as a first-class, pluggable concern: a static single
tenant, or a tenant derived from the request host or path. This request-derived tenancy doubles as a security /
deployment / infrastructure boundary — a deployment reached at `tenant1.example.com` (or `/tenant1/…`) serves exactly
one tenant.

The management API administers those same tenants and their children (clients and, in the future, other tenant-bound
resources), but historically had no equivalent boundary: with globally-unique, server-generated resource identifiers
([ADR-0014](0014-server-generated-opaque-resource-ids.md)) a resource is addressable by id alone, so a request scoped
to one tenant's management surface could reach another tenant's resources. There was also a structural problem: the
tenant-selection logic (host / path / static) lived entirely inside the authentication runtime, welded to the
heavyweight materialization of a runtime tenant (issuer, secret keys, settings providers, caching), so the management
API could not reuse it without either duplicating the selection rules — inviting divergence — or taking a dependency
on the runtime.

## Decision

**Tenant _selection_ is separated from tenant _materialization_ and promoted to a shared abstraction that both the
runtime and the management API compose via dependency injection; the management API layers an _optional_ tenant
boundary on top of it.**

- **Selection (shared).** `ITenantResolver` (plus the pluggable `ITenantResolverStrategy` for static-single /
  dynamic-by-host / dynamic-by-path, and `TenantResolutionOptions`) resolves a request to a `TenantDescriptor`. The
  contracts live in `NCode.Identity.OpenId.Abstractions` (namespace `NCode.Identity.OpenId.Tenants`); the default
  implementation lives in `NCode.Identity.OpenId.Core`, mirroring the `OpenIdEnvironment` → `DefaultOpenIdEnvironment`
  precedent. Both consumers depend only on the abstraction and receive the concrete resolver through DI, never through
  a cross-implementation reference ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)).
- **Materialization (runtime only).** A single `DefaultOpenIdTenantProvider` composes `ITenantResolver` for selection
  and builds the runtime `OpenIdTenant` (issuer, base address, settings/secrets providers, caching). It replaces the
  former per-strategy provider classes and their selector.
- **Boundary (management only).** `ITenantBoundary` enforces the optional, per-request isolation for tenant-scoped
  resource families. The rule: when tenancy is request-derived (dynamic-by-host or dynamic-by-path), a resource whose
  owning tenant differs from the request's resolved tenant is reported as **`404 Not Found`** — never `403` — so a
  scoped surface cannot probe the existence of another tenant's resources. When tenancy is not request-derived
  (static-single), the surface is unscoped (central admin) and the check is a no-op. **Tenant and server administration
  is always central-admin (unscoped); only tenant-child families (clients, and future tenant-bound resources) call the
  boundary.**

## Options considered

- **Nest management resources under a tenant route segment** (`/tenants/{tenantId}/clients/{clientId}`). Rejected:
  identifiers are already globally unique, so the tenant segment adds nothing to addressing; the desired capability is
  an _optional_ boundary, not a mandatory routing change, and this would not mirror the runtime's host/path/none model.
- **Reuse the runtime tenant provider directly from the management API.** Rejected: it is welded to runtime
  materialization and would drag the authentication runtime into the management surface — and an implementation
  referencing another implementation violates [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md).
- **A blanket authorization handler over every `ISupportTenantId` resource.** Rejected: tenant secrets are also
  `ISupportTenantId`, yet tenant administration is central-admin; a blanket handler would wrongly scope them, and an
  authorization failure yields `403` (revealing existence) rather than the `404` this boundary requires.
- **An explicit, reusable boundary invoked by tenant-scoped endpoint families (chosen).** A new tenant-bound family
  reuses `ITenantBoundary`; central-admin families simply do not call it. The check returns `404` and reuses the
  shared `ITenantResolver`, giving true symmetry with the runtime while respecting the central-admin surfaces.

## Consequences

- The tenant-selection rules have a single source of truth, so the runtime and the management boundary can never
  diverge on "which tenant is this request?"
- Enabling the management boundary is a configuration choice (`TenantResolutionOptions.ProviderCode` =
  dynamic-by-host / dynamic-by-path), matching how the runtime opts into multi-tenancy; static-single deployments are
  unaffected.
- A future tenant-bound management resource family gets isolation by injecting `ITenantBoundary` and calling it after
  loading the resource — no new mechanism, no changes to central-admin families.
- Extracting selection is a public-API relocation (the `Tenant*` selection types moved packages); all of it is
  pre-1.0 unshipped surface, so no shipped contract is broken.

## References

- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §2, §4.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — globally-unique identifiers, why the tenant is not needed
  for addressing.
- [ADR-0015](0015-management-core-validation-via-validators.md) — the management validator seam the boundary sits
  alongside.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — the reference-direction rule the
  abstraction/implementation split honors.
- Code: `NCode.Identity.OpenId.Tenants.ITenantResolver`, `NCode.Identity.OpenId.Core` tenant strategies,
  `NCode.Identity.OpenId.Authentication.Tenants.Providers.DefaultOpenIdTenantProvider`,
  `NCode.Identity.OpenId.Management.Endpoints.ITenantBoundary`.

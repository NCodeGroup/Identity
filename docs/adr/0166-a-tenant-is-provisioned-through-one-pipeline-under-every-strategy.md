# 166. A tenant is provisioned through one pipeline under every strategy

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Provisioning brings a tenant into existence and into a seeded state: its row, then the data every seed contributor
reserves in it — the system resource servers, a development signing key, configured clients
([ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md)). ADR-0045 made it one coordinator for every strategy, but
only the static strategy called it:

- **A tenant created through the management API was never seeded.** It existed without the OpenID identity resource
  server or the tenant-plane management resource server until something else provisioned it, and nothing did.
- **The root tenant was never provisioned under the dynamic strategies**, which ADR-0031 left as future work. A fresh
  host-routed or path-routed deployment had no control plane, so no bootstrap administrator could obtain the first
  management token.
- **A dynamic tenant never reconciled.** The static strategy seeded on every cache miss, so a contributor added by a
  deploy, or a bootstrap secret healed from configuration ([ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md)),
  reached its tenant; a dynamic tenant was never seeded again.

Three seeders — the bootstrap administrator, the development interactive seeder, and the conformance seeder — target a
configured tenant and, when none is configured, every workload tenant. With one tenant that was the same thing. Once
tenants created at runtime are seeded, it would hand each of them a client whose tokens carry `GlobalAdmin`, a
well-known administrator, or the conformance suite's credentials.

## Decision

**Every tenant reaches a seeded state through `ITenantProvisioner`, under every strategy, and a seeder that names a
tenant seeds exactly that tenant.**

1. **Two operations.** `EnsureCreatedAsync(tenantId, displayName)` creates a tenant configuration defines — the root
   tenant, or a static strategy's own tenant — when it is missing, at the address its strategy derives, judged like any
   other write ([ADR-0165](0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)); it tolerates
   a concurrent creator by re-reading after a failed insert. `SeedAsync(tenantId)` ensures and seeds the root tenant
   first, then fans `SeedTenantCommand` out for the tenant within one unit of work.
2. **Strategies create, the materializer seeds.** A strategy resolves a request to its tenant and creates a
   configuration-defined tenant on demand: the static strategy its own tenant, and the dynamic strategies the root tenant
   when a request reaches the root tenant's address. Every other tenant is created through the management API, and a
   request for a missing one is `404`. The tenant factory seeds the resolved tenant on each cache miss, so every
   strategy reconciles its tenants as the static strategy always did, and the host strategy — which resolves before it
   can probe the cache — still seeds only on a miss.
3. **Management create seeds.** `POST /api/tenants` commits the tenant row, records its creator as owner, and seeds the
   tenant before it answers, so the tenant's system resource servers exist before its first request. The response
   carries the tenant as it stands after seeding, since a contributor may change its row.
4. **Root options are grouped.** `TenantResolution:Root` holds the root tenant's `TenantId`, `DisplayName`, and `Issuer`,
   replacing `TenantResolution:RootTenantId`.
5. **A seeder names its tenant.** `BootstrapAdmin:TenantId`, `DeveloperInteractiveSeed:TenantId`, and
   `ConformanceSeed:TenantId` are required whenever the seeder has something to seed, and the host refuses to start
   without them. Each seeds exactly the named tenant, in either plane: a multi-tenant platform's operators belong in the
   root tenant. The `GlobalAdmin` role is conferred only on the bootstrap client in the bootstrap tenant.
6. **`TenantSeedContext.IsNewlyProvisioned` is removed.** No contributor read it, and seeding is now separate from
   creation, so it can no longer be known.

## Options considered

- **One `ProvisionAsync` that creates and seeds, called by every strategy's resolution.** Rejected: the host strategy
  resolves on every request before it can probe the cache, so it would seed on every request; and a path-strategy request
  for an unknown tenant would create it.
- **Provision the root tenant at startup.** Rejected for the reason ADR-0031 gave: the lazy path needs no hosted
  service, and it is where a configuration-only cold start (ADR-0044) already happens.
- **Default a seeder's tenant to the root tenant, or to the static tenant.** Rejected: the right tenant depends on the
  strategy, and a default that silently varies is the kind of guess that seeded every tenant. Naming it is one setting.
- **Leave management-created tenants unseeded until their first request.** Rejected: the tenant's resource servers would
  be missing from the management API until then, and S11's production signing keys would have nowhere to be provisioned.

## Consequences

- A dynamic deployment boots from configuration alone: the first request to the root tenant's address creates and seeds
  it, including the bootstrap administrator named into it, and that administrator creates every other tenant.
- Every tenant created at runtime starts seeded; production key provisioning (backlog `S11`) can seed keys into each.
- Each cache miss runs the seed contributors once for the tenant and once for the root tenant, as the static strategy did
  before; contributors stay idempotent.
- **Breaking:** `ITenantProvisioner.ProvisionAsync` is replaced; `TenantResolutionOptions.RootTenantId` moves to
  `Root.TenantId`; the three seeder `TenantId` settings are required; `TenantSeedContext.IsNewlyProvisioned` is gone.

## References

- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the control plane and the root tenant.
- [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md) — root-tenant provisioning under
  the dynamic strategies, left as future work there.
- [ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md) — the bootstrap administrator, now seeded
  into exactly its named tenant.
- [ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md) — the pipeline this decision applies to every strategy.
- Code: `NCode.Identity.OpenId.Tenants.ITenantProvisioner`, `DefaultTenantProvisioner`, `TenantStrategy`,
  `DefaultOpenIdTenantFactory`, `RootTenantOptions`,
  `NCode.Identity.OpenId.Management.Endpoints.Tenants.TenantApiEndpointHandler.CreateTenantAsync`,
  `NCode.Identity.Server.BootstrapAdminOptionsValidator`.

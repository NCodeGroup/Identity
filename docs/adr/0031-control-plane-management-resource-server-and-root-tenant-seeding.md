# 31. The control-plane management resource server is a planed system resource server seeded only into the root tenant

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

[ADR-0024](0024-control-plane-and-per-tenant-planes.md) models the system as a control plane plus N per-tenant planes:
the control plane administers servers and tenants (the `GlobalAdmin` realm) and _is_ the root tenant, while a workload
tenant's plane holds its clients, resource servers, scopes, and grants (the `TenantAdmin` realm). It also placed the
control-plane management resource server in the root tenant and the tenant-plane management resource server in every
workload tenant.

The seeding machinery, however, could not express that split. `ISystemResourceServerProvider` contributes a
`SystemResourceServerDescriptor`, and `ISystemResourceServerSeeder` seeded **every** registered provider into **every**
tenant it was asked to provision. So the `ManagementResourceServerProvider` (`urn:ncode:management`) deliberately
excluded the `servers` and `tenants` families — there was nowhere to put them that would not leak control-plane scopes
into every workload tenant. There was also no notion of a root tenant in code: the `StaticSingleTenantStrategy`
auto-provisioned a single workload tenant, and nothing ensured the control plane's own tenant existed.

## Decision

**A system resource server declares the deployment _plane_ it belongs to, and the seeder honors it; the control-plane
management resource server is seeded only into the root tenant, whose existence the static single-tenant strategy now
guarantees.**

- **`SystemResourceServerDescriptor` gains a `Plane`** (`SystemResourceServerPlane` — `Tenant` (default) or `Control`).
  `Tenant`-plane providers are seeded into every tenant; `Control`-plane providers are seeded only into the root tenant.
  The default is `Tenant`, so every existing provider keeps its behavior with no change.
- **A new `ControlManagementResourceServerProvider`** contributes the reserved control-plane resource server
  `urn:ncode:control-management` on the `Control` plane, owning full CRUD scopes for the `servers` and `tenants`
  families (`read`/`create`/`update`/`delete`). The tenant-plane `ManagementResourceServerProvider` keeps the clients,
  resource servers, client grants, and grants families; the two providers partition the management surface along the
  ADR-0024 boundary.
- **`DefaultSystemResourceServerSeeder` is plane-aware.** Given a tenant id, it compares it to the configured root
  tenant id (`TenantResolutionOptions.RootTenantId`, defaulting to the `"root"` constant) and seeds a `Control`-plane
  provider only when seeding the root tenant. If no provider applies, it opens no unit of work.
- **The root tenant is provisioned lazily, on the same path as workload tenants.** When the `StaticSingleTenantStrategy`
  resolves (and lazily provisions) its workload tenant, it first ensures the root tenant exists and is seeded. A freshly
  provisioned tenant — root or workload — is seeded with the system resource servers whose plane applies to it, so the
  control-plane resource server lands only in the root tenant and the tenant-plane resource servers land in every
  tenant.
- **The root tenant id is a constant that is configurable.** `TenantResolutionOptions.RootTenantId` defaults to
  `TenantResolutionOptions.DefaultRootTenantId` (`"root"`) and may be overridden per deployment.

## Options considered

- **A plane flag on the descriptor, with lazy root-tenant provisioning (chosen).** Keeps the one seeding seam, needs no
  new abstraction, and is purely additive (the default plane preserves every existing provider). The root tenant is
  provisioned by the existing auto-provisioning strategy, so a single-tenant deployment gains its control plane with no
  extra configuration.
- **A separate `IControlPlaneResourceServerProvider` interface and a second seeder.** Rejected as redundant: it
  duplicates the provider/seeder shape to encode a single boolean, and forces every composition root to wire a parallel
  pipeline.
- **Merge the control-plane scopes into the existing `urn:ncode:management` resource server.** Rejected: it would seed
  `servers`/`tenants` scopes into every workload tenant, erasing the control/tenant boundary that ADR-0024 exists to
  draw.
- **Seed the root tenant through an explicit startup initializer.** Rejected for now as heavier than needed: the lazy,
  resolve-time path mirrors how workload tenants are already provisioned and needs no hosted service. Admin-provisioned
  (dynamic) deployments, which require a pre-existing tenant, will seed the root tenant through their provisioning
  tooling — a future seam, not this change.

## Consequences

- The management surface is partitioned exactly along the ADR-0024 boundary: `urn:ncode:control-management` (servers,
  tenants) in the root tenant, `urn:ncode:management` (clients, resource servers, client grants, grants) in every
  tenant. `GlobalAdminHandler` continues to grant the control-plane scopes for the `GlobalAdmin` realm.
- A single-tenant (static) deployment now materializes two tenants on first use — the workload tenant and the root
  (control-plane) tenant — each self-contained with the system resource servers its plane requires.
- The change is additive and backward compatible for providers: the `Tenant` plane is the default, so the OpenID
  identity provider and the tenant-plane management provider are unaffected.
- Dynamic (admin-provisioned) deployments do not yet auto-provision the root tenant; that remains future work tied to
  the ADR-0024 connection-routing seam. The seeder and plane model are already in place to support it.

## References

- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the control-plane / per-tenant-plane deployment model this
  change realizes for system-resource-server seeding.
- [ADR-0023](0023-scopes-and-resources-management-model.md) — the resource server / scope / client grant model and the
  root/system tenant that owns the control plane.

# 31. The control-plane management resource server is a planed system resource server seeded only into the root tenant

- **Status:** Accepted (the two-audience split was later collapsed to a single `urn:ncode:management` audience by
  [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md); the plane model, plane-aware seeder, and
  root-tenant provisioning decided here remain in force)
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

[ADR-0024](0024-control-plane-and-per-tenant-planes.md) models the system as a single control plane with logical tenant
isolation: the control plane administers servers and tenants (the `GlobalAdmin` realm) and _is_ the root tenant, while a
workload tenant holds its clients, resource servers, scopes, and grants (the `TenantAdmin` realm). It also placed the
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
- **A new `ControlManagementResourceServerProvider`** contributes the control-plane scope families of the reserved
  management resource server `urn:ncode:management` on the `Control` plane, owning the `servers` family (full CRUD),
  `server_settings` (read/update), `server_secrets` (full CRUD), and the `tenants` provisioning family (full CRUD). The
  tenant-plane `ManagementResourceServerProvider` contributes the same audience's tenant-scoped families (a tenant's own
  settings and secrets, its clients, client secrets, resource servers, client grants, and grants). Both providers name
  the same audience; the seeder merges them (see below). _Originally these were two distinct audiences
  (`urn:ncode:control-management` and `urn:ncode:management`); [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md)
  unified them into one so a management client never juggles two audiences._
- **`DefaultSystemResourceServerSeeder` is plane-aware and merges by audience.** Given a tenant id, it compares it to
  the configured root tenant id (`TenantResolutionOptions.RootTenantId`, defaulting to the `"root"` constant) and seeds
  a `Control`-plane provider only when seeding the root tenant. It then groups the applicable descriptors by identifier
  and unions their scopes (deduplicating by value), so providers that contribute to the same audience become one
  resource-server row per tenant. In the root tenant the management audience therefore carries both its control-plane
  and tenant-plane families; in a workload tenant it carries only its tenant-plane families. If no provider applies, it
  opens no unit of work.
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
- **Merge the control-plane scopes into one `urn:ncode:management` resource server, seeded root-only.** Rejected _at the
  time_ because the seeder could not seed a subset of an audience's scopes per plane. Once the seeder became plane-aware
  and merge-by-audience (this ADR plus [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md)), this became
  the chosen shape: the control-plane families are seeded only into the root tenant, so a workload tenant never receives
  `servers`/`tenants` scopes even though it shares the audience.
- **Seed the root tenant through an explicit startup initializer.** Rejected for now as heavier than needed: the lazy,
  resolve-time path mirrors how workload tenants are already provisioned and needs no hosted service. Admin-provisioned
  (dynamic) deployments, which require a pre-existing tenant, will seed the root tenant through their provisioning
  tooling — a future seam, not this change.

## Consequences

- The management surface is a single audience `urn:ncode:management` whose scope families are partitioned by plane: the
  control-plane families (`servers`, `server_settings`, `server_secrets`, `tenants`) are seeded only into the root
  tenant, and the tenant-plane families are seeded into every tenant. `GlobalAdminHandler` continues to grant the
  control-plane scopes for the `GlobalAdmin` realm.
- A single-tenant (static) deployment now materializes two tenants on first use — the workload tenant and the root
  (control-plane) tenant — each self-contained with the system resource servers its plane requires.
- The change is additive and backward compatible for providers: the `Tenant` plane is the default, so the OpenID
  identity provider and the tenant-plane management provider are unaffected.
- Dynamic (admin-provisioned) deployments do not yet auto-provision the root tenant; that remains future work. The
  seeder and plane model are already in place to support it.

## References

- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the control-plane / per-tenant-plane deployment model this
  change realizes for system-resource-server seeding.
- [ADR-0023](0023-scopes-and-resources-management-model.md) — the resource server / scope / client grant model and the
  root/system tenant that owns the control plane.

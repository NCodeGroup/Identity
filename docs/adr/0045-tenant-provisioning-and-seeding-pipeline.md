# 45. Tenant provisioning and seeding are one mediator-driven pipeline

- **Status:** Accepted (supersedes the seeding mechanics of
  [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md) and
  [ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md), and changes how the developer signing
  key of [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) is realized; the _policies_ those ADRs
  decided — plane partitioning, the GlobalAdmin bootstrap, a single developer-key source of truth — remain in force)
- **Date:** 2026-10-04
- **Deciders:** NCode Group

## Context

Seeding accreted one patch at a time, leaving three structurally different mechanisms doing the same conceptual job —
"ensure some reserved data exists in a tenant":

- **System resource servers** used a provider/descriptor/seeder trio: `ISystemResourceServerProvider` contributed a
  `SystemResourceServerDescriptor` (carrying a `SystemResourceServerPlane`), and `ISystemResourceServerSeeder` merged
  and wrote them — but **only on fresh tenant provisioning**.
- **Arbitrary tenant data** (the bootstrap administrator client) used a second seam, `ISystemTenantSeeder`, invoked
  imperatively on **every** tenant resolution, each implementation re-deriving its own "skip the root tenant" rule.
- **The developer signing key** used a third mechanism entirely — a `DeveloperSigningKeyOpenIdTenantFactory` subclass
  that overrode the tenant **read path** to seed and heal a key at materialization.

This was incohesive and had real defects:

- **Inconsistent lifecycle.** Resource-server seeding ran once (fresh provisioning only), so a resource server added to
  the code after a tenant already existed was **never** seeded into it. The bootstrap seeder dodged this by running
  every time — the same concept, opposite rules.
- **Coupled to one strategy.** Only `StaticSingleTenantStrategy` seeded; the dynamic strategies resolved-or-404 and
  seeded nothing, so seeding behavior silently depended on the tenancy strategy.
- **No shared unit of work.** Provisioning saved the tenant, the resource-server seeder opened its own unit of work,
  each tenant seeder opened another, and the key factory opened yet another — a single logical "provision and seed"
  spanned four-plus transactions, so a partial failure left a half-seeded tenant.
- **Scattered plane and root-tenant logic.** The plane split lived in the resource-server seeder; "skip root" and
  "ensure root first" were re-implemented imperatively elsewhere.

## Decision

**Provisioning is a single scoped service, and seeding is a single mediator fan-out. One pipeline provisions the root
tenant and the target tenant identically, classifies the plane once, and runs every seed contributor within one shared
unit of work, for every tenancy strategy.**

- **`ITenantProvisioner` (the coordinator).** A scoped service with
  `ProvisionAsync(tenantId, displayName, cancellationToken)`. The default `DefaultTenantProvisioner`:
    1. provisions the **root tenant first** on the same path when the target tenant is not itself the root tenant
       (recursively, so root and workload tenants are provisioned identically);
    2. opens an ambient tenant scope and **one** `IStoreManager`, and ensures the tenant row exists, committing a newly
       created row so seed contributors can attach child rows to it;
    3. classifies the **plane** once (`tenantId == RootTenantId ? TenantPlane.Root : TenantPlane.Workload`);
    4. publishes `SeedTenantCommand` and commits the seeded data once.
- **`SeedTenantCommand` (the fan-out).** A void `ICommand` carrying a `TenantSeedContext` (the tenant, its plane, an
  `IsNewlyProvisioned` flag, and the shared `IStoreManager`). Every `ICommandHandler<SeedTenantCommand>` runs, so each
  subsystem contributes its reserved data through one seam, ordered by `ISupportMediatorPriority`, enlisted in the one
  unit of work. Handlers are idempotent — they check for their data before writing — so a tenant may be seeded again
  (for example, after a deploy adds a new contributor), which fixes the "never reaches an existing tenant" defect.
- **The three mechanisms collapse onto it.** The OpenID identity resource server, the management resource server, the
  bootstrap administrator client, and the developer signing key are each now an `ICommandHandler<SeedTenantCommand>`.
  The management handler is a single handler that selects tenant-plane scope families always and control-plane families
  only when `context.Plane == Root`, so the former provider/merge-by-audience machinery is unnecessary.
- **Plane targeting is declarative and centralized.** `TenantPlane` (replacing `SystemResourceServerPlane`) is computed
  once by the coordinator and carried on the context; a handler opts into a plane by inspecting `context.Plane` rather
  than re-deriving "is this the root tenant?".
- **Seeding is decoupled from resolution, so it works for every strategy.** Strategies only _resolve_; the static
  single-tenant strategy triggers `ProvisionAsync` on its lazy first-resolution path, and any other origin (management
  tooling that creates a tenant, or a future strategy) triggers the same coordinator. There is exactly one way to bring
  a tenant into a seeded state.

## Options considered

- **One provisioner service plus one mediator fan-out (chosen).** Matches the service-vs-mediator split of
  [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md): orchestration (ensure the row, own the unit of
  work, order and commit) is a replaceable service; the open-ended "who contributes to a tenant" set is a mediator
  fan-out. One pattern to implement, one lifecycle rule, one transaction, plane handled once.
- **Make provisioning a mediator command too.** Rejected: provisioning is single-dispatch orchestration, not fan-out,
  and a request-scoped `IMediator` injected into it would require the handler lifetime gymnastics the existing
  single-handler convention avoids. A replaceable scoped service is the better fit.
- **Keep the three mechanisms but unify only their invocation.** Rejected: it preserves three shapes a contributor
  author must choose between and leaves the scattered plane/root logic in place.
- **Keep the developer signing key on the read-path factory subclass.** Rejected: the key is needed at the same
  cache-miss materialization boundary the provisioner already runs on, so a seed handler that refreshes the in-memory
  secrets is behavior-equivalent and removes the third mechanism. The handler keeps the self-heal (prune an
  undecryptable secret, regenerate when none is usable) that [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md)
  required; only its realization moves from overriding the read to contributing to the seed.

## Consequences

- One extensibility seam for tenant seeding (`ICommandHandler<SeedTenantCommand>`), one coordinator
  (`ITenantProvisioner`), one shared unit of work, and one place that classifies the plane and guarantees the root
  tenant — for every tenancy strategy.
- **Breaking:** `ISystemResourceServerProvider`, `ISystemResourceServerSeeder`, `SystemResourceServerDescriptor`,
  `SystemScopeDescriptor`, `SystemResourceServerPlane`, and `ISystemTenantSeeder` are removed; `TenantPlane`,
  `TenantSeedContext`, `SeedTenantCommand`, and `ITenantProvisioner` are added. The public
  `DeveloperSigningKeyOpenIdTenantFactory` is removed in favor of an internal seed handler. Acceptable under the
  pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- Adding a new reserved contributor is now additive: register one `ICommandHandler<SeedTenantCommand>` that is
  idempotent and plane-aware; it reaches both new and existing tenants on their next provisioning pass.
- The newly created tenant row commits before the seed fan-out (seed stores resolve the owning tenant from the
  database), then all seeded data commits together — a tenant is provisioned with its row and then atomically seeded,
  rather than across several independent transactions.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy-collection / mediator
  extensibility model this pipeline applies (orchestration as a service, contribution as a mediator fan-out).
- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the control-plane / workload-plane model the `TenantPlane`
  classification realizes.
- [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md) — the plane-aware
  resource-server seeding and lazy root-tenant provisioning whose mechanics this ADR supersedes.
- [ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md) — the bootstrap administrator whose
  seeder becomes a seed handler.
- [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) — the single-source-of-truth developer signing
  key whose realization moves from a read-path factory to a seed handler.

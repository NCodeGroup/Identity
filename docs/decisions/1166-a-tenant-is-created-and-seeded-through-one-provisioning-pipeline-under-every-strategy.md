# 1166. A tenant is created and seeded through one provisioning pipeline under every strategy

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A tenant reaches a usable state in two parts: its row exists, and every seed contributor has written the data it
reserves in it, such as the system resource servers, a signing key and the configured clients. Seeding that grows one
patch at a time leaves several mechanisms doing one job, with these defects:

- **Inconsistent lifecycle.** Data seeded only when a tenant is first created never reaches a tenant that already
  exists, so a resource server a deploy adds is missing from it.
- **Coupling to one strategy.** If only the static strategy seeds, a tenant created through the management API has no
  OpenID identity resource server and no tenant-plane management resource server until something else seeds it, and
  nothing does.
- **No root tenant under the dynamic strategies.** A fresh host-routed or path-routed deployment has no control plane,
  so no bootstrap administrator can obtain the first management token.
- **No reconciliation.** A dynamic tenant that is never seeded again misses a contributor a deploy adds and a bootstrap
  secret healed from configuration.
- **No shared unit of work.** Seeders that each open their own unit of work span several transactions, so a partial
  failure leaves a half-seeded tenant.
- **Seeders that target every tenant.** The bootstrap administrator, the development interactive seeder and the
  conformance seeder target a configured tenant and, when none is configured, every workload tenant. Once tenants
  created at runtime are seeded, that hands each of them a client whose tokens carry `GlobalAdmin`, a well-known
  administrator, or the conformance suite's credentials.
- **A stale row after the seed.** Seed contributors write the tenant row: a seeded or rolled key adds secrets, and
  development pruning removes them. The store issues a new secrets token for each write, and the interceptor
  regenerates the row token on save
  ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).
  The tenant a seed reads before its contributors run carries the tokens of the row before the seed, yet the row token
  is what a replica compares after a missed announcement
  ([ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md)),
  the settings and secrets tokens are what its polls compare, and the management API answers a created tenant with its
  row token as the ETag.

## Decision

**Every tenant reaches a seeded state through `ITenantProvisioner`, under every strategy; a seeder that names a tenant
seeds exactly that tenant; and `SeedAsync` returns the row its seed committed.**

### The coordinator

`ITenantProvisioner`, default `DefaultTenantProvisioner`, is the one coordinator: a replaceable, scoped service. It has
two operations:

1. `EnsureCreatedAsync(teamId, tenantId, displayName)` creates a tenant that configuration defines, the root tenant or
   the static strategy's own tenant, when it is missing. It first ensures the tenant's team exists through
   `ITeamProvisioner`, the root team with `Root:TeamDisplayName`, so the tenant's team reference holds. The tenant is created at the address its strategy derives and is
   judged like any other write
   ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)). It
   tolerates a concurrent creator by re-reading after a failed insert.
2. `SeedAsync(tenantId)` ensures and seeds the root tenant first, without reading its row back, then seeds the tenant
   and returns the committed row.

### The seed fan-out

Seeding is a mediator fan-out, the service-and-mediator split that
[ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md) describes:
orchestration, meaning ensuring the row, owning the unit of work, ordering and committing, is the replaceable service,
and the open-ended set of contributors is the fan-out.

1. `SeedTenantCommand` carries a `TenantSeedContext` with three members: `Tenant`, the seed's working copy; `Plane`, a
   `TenantPlane` of `Root` or `Workload` that the coordinator computes once; and the shared `StoreManager`.
2. Every `ICommandHandler<SeedTenantCommand>` runs, ordered by `ISupportMediatorPriority`, enlisted in one unit of work
   that the provisioner commits once. Contributors never save it.
3. A contributor that changes the tenant's settings or secrets updates `TenantSeedContext.Tenant`, so the contributors
   that follow see the change before the commit.
4. Steps registered through `TenantSeedContext.RunAfterCommit` run in order after the seed commits, each on its own
   bounded token (`PostCommitWork`). A seed that loses its commit to a concurrent seed runs again with a fresh context,
   and the discarded attempt's steps never run.

### The row the seed committed

1. After the commit and its post-commit steps, `SeedAsync` reads the tenant in a fresh unit of work and returns that
   row. A tenant that is missing before the seed, or removed in between, is answered `404`.
2. The tenant factory materializes the tenant from the returned row, so the materialized tenant carries the row,
   settings and secrets tokens of its own seed.
3. The management create response carries the returned row and does not read the tenant a second time.

### Strategies create, the materializer seeds

A strategy resolves a request to its tenant and creates a configuration-defined tenant on demand: the static strategy
creates its own tenant, and the dynamic strategies create the root tenant when a request reaches its address. Every
other tenant is created through the management API, and a request for a missing one is `404`. The tenant factory seeds
the resolved tenant on each cache miss, so every strategy reconciles its tenants. The host strategy resolves before it
can probe the cache, so it too seeds only on a miss.

`POST /api/teams/{teamId}/tenants` commits the tenant row, records the creator as owner, seeds, and answers with the
returned committed row, so the tenant's system resource servers exist before its first request
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)).

`TenantResolution:Root` groups the root tenant's `TenantId`, `DisplayName`, `Issuer` and `MtlsEndpointAliasAuthority`,
and its team's `TeamId` and `TeamDisplayName`.

A dynamic deployment boots from configuration alone: the first request to the root tenant's address creates and seeds
it, including the bootstrap administrator, who creates every other tenant.

### Contributors

Contributors are idempotent and plane-aware, so a tenant may be seeded again and a contributor reaches existing tenants
on their next seed:

- `OpenIdIdentityResourceServerSeedHandler` seeds the OpenID identity resource server.
- `ManagementResourceServerSeedHandler` seeds the management resource server, with the control-plane families only
  when `Plane == Root`.
- `TenantKeySeedHandler` provisions the tenant's keys
  ([ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md)).
- `BootstrapAdminSeedHandler` seeds the development bootstrap client.
- The opt-in `DeveloperSecretPruningSeedHandler`, `DeveloperInteractiveSeedHandler` and `ConformanceSeedHandler`.

Adding a contributor is one registered handler.

### A seeder names its tenant

`BootstrapAdmin:TenantId`, `DeveloperInteractiveSeed:TenantId` and `ConformanceSeed:TenantId` are required whenever the
seeder has something to seed, and the host refuses to start without them. Each seeds exactly that tenant, in either
plane, because a multi-tenant platform's operators belong in the root tenant. The `GlobalAdmin` stamp is carried only by
the development bootstrap client, in the bootstrap tenant; a Production instance's root administrator holds a role
assignment instead ([ADR-1512](1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md)).

## Options considered

- **Make provisioning a mediator command too.** Rejected: provisioning is single-dispatch orchestration, not fan-out,
  and a replaceable scoped service fits it better.
- **Keep separate seeding mechanisms and unify only their invocation.** Rejected: it keeps several shapes that a
  contributor author must choose between and leaves the plane and root-tenant logic scattered.
- **One `ProvisionAsync` that creates and seeds, called by every strategy's resolution.** Rejected: the host strategy
  resolves on every request before it can probe the cache, so it would seed per request, and a path request for an
  unknown tenant would create it.
- **Provision the root tenant at startup.** Rejected: the lazy path needs no hosted service, and it is where a
  configuration-only cold start already happens.
- **Default a seeder's tenant to the root tenant or the static tenant.** Rejected: the right tenant depends on the
  strategy, and a default that silently varies is the guess that seeded every tenant. Naming it is one setting.
- **Leave created tenants unseeded until their first request.** Rejected: the tenant's resource servers would be missing
  from the management API until then, and its keys would have nowhere to be provisioned.
- **Carry the committed tokens onto the working copy.** Rejected: the interceptor regenerates the row token during save,
  outside any store method, and the data contract is detached from the tracked entity.
- **Each caller reads the tenant again, or materializes from the pre-seed row.** Rejected: a caller that omits the read
  materializes the pre-seed row, which a revalidation evicts once and whose secrets poll reads the secrets again for
  nothing.
- **One provisioner service, one mediator fan-out and a post-commit read of the row (chosen).** One indexed read per
  seed returns every token from one consistent read, and every caller gets the committed row.

## Consequences

- Every tenant created at runtime starts seeded, under every strategy: its row commits first, and its seed commits in
  one unit of work.
- Each cache miss runs the contributors for the root tenant and for the tenant; contributors are idempotent.
- A seed costs one extra indexed read, including a seed after a settings change, which discards the row.
- A freshly materialized tenant is current on a revalidation unless its row changed after its seed. A row that another
  writer changes between the commit and the read is returned as read, and that writer's announcement evicts the tenant.
- A custom `ITenantProvisioner` returns the committed row.
- Tests cover the coordinator, each contributor's idempotence and plane, the management create endpoint, and the
  startup validation of the seeders' tenant settings.

## References

- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the service and
  mediator ladder.
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md):
  the interceptor-managed concurrency tokens.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the control plane and the root tenant; the team and the create
  route.
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md): plane-aware resource-server
  seeding.
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md): the development bootstrap client.
- [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md): the issuer
  judged on every write.
- [ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md): the key seed handler.
- [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md):
  revalidation after a missed announcement.
- [#619](https://github.com/NCodeGroup/identity/issues/619)
- Code: `NCode.Identity.OpenId.Tenants.ITenantProvisioner`, `DefaultTenantProvisioner`, `SeedTenantCommand`,
  `TenantSeedContext`, `TenantPlane`, `PostCommitWork`, `DefaultOpenIdTenantFactory`, `RootTenantOptions`,
  `NCode.Identity.OpenId.Management.Endpoints.Tenants.TenantApiEndpointHandler.CreateTenantAsync`,
  `NCode.Identity.Server.BootstrapAdminOptionsValidator`.

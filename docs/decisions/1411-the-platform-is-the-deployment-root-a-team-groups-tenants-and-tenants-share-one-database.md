# 1411. The platform is the deployment root, a team groups tenants, and tenants share one database

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

Tenancy exists so that one running server hosts many tenants without their data bleeding across. The question is how
strong that isolation must be, and where logical isolation (one deployment, one database, tenants separated by scoping)
ends and physical isolation (separate storage and infrastructure) begins. Everything lives in one `OpenIdDbContext`
behind a single connection: the tenants and every tenant's clients, resource servers, scopes and grants, joined by
foreign keys.

A deployment also has one root: the configuration its host starts with, the master key of its Data Protection key ring
and the tenant that holds the control plane. A customer of that deployment runs related tenants, such as a sandbox, a
testing tenant and a production tenant, that share a bill, a set of administrators and a policy
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)
models them as separate tenants). Nothing names the customer: a policy that should bind all of a customer's tenants has
to be written into each one, the administrators of one tenant cannot create another, a plan's tenant quota has no scope
to apply to, and usage cannot be summed per customer.

The family already has the pieces a grouping needs: a cascade of settings scopes, each setting declaring the scopes it
applies to ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)),
security profiles that accumulate down the cascade
([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)),
tenants named by their creator
([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)), and a role model
whose assignments are owned by a scope
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)). The open questions are how far tenant isolation
goes, what the deployment root is, what groups tenants, and how authority, settings, lifecycle and the console address
them.

## Decision

**A deployment is one control plane over one database in which tenants are a logical isolation boundary; the platform is
its one root, and a team is a persisted grouping of a customer's tenants between the platform and the tenants in the
settings cascade and in authority.**

1. **One database, logical isolation.** A deployment is one control plane over one physical database and one
   `OpenIdDbContext`. The platform, teams, tenants and their child resources form one connected graph with ordinary
   foreign keys, and the ambient-tenant query filter
   ([ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md)) enforces
   tenant isolation. A tenant is like a namespace in a cluster: a cheap scoping boundary inside one running system.
2. **Physical isolation is another deployment.** An operator who needs compliance isolation, a smaller blast radius or
   freedom from noisy neighbors runs a separate deployment with its own database. The library has no per-tenant database
   routing, projection or cross-plane materialization.
3. **Control plane and tenant plane are logical roles.** The root tenant (`TenantPlane.Root`) holds the control-plane
   management scope families, and every tenant holds the OpenID identity and tenant-plane management resource servers
   ([ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) seeds
   them). The names describe which management surface a tenant holds, not separate databases.
4. **The platform is the one deployment root.** It is the host configuration (`OpenId:Platform:Settings`), one persisted
   settings document at `/api/platform/settings`, the master key of the Data Protection key ring
   ([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)) at
   `/api/platform/encryption-keys`
   ([ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md))
   and the authority node `(platform, platform)`. It has no identifier, list, create, delete or secrets, because there
   is exactly one.
5. **A team is a persisted grouping of tenants.** Its creator names it in the tenant-identifier form
   ([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)); it has a display
   name, an `isDisabled` flag and a settings document. Every tenant belongs to exactly one team and never moves. A team
   may sit beneath a parent team
   ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md)).
6. **The platform's own team.** `TenantResolution:Root:TeamId` (default `platform`) names the team that holds the root
   tenant and every tenant a tenant-resolution strategy creates. It is top-level, holds no teams, and can be neither
   disabled nor deleted.
7. **The cascade.** Settings resolve through host configuration and the platform, the team or teams, the tenant, and the
   client or resource server, with the registered setting scopes `platform`, `team`, `tenant`, `client` and
   `resource_server`. A team document carries every setting that applies beneath it, so the security profiles and
   ceilings a team selects bind its tenants. A package extends the cascade by registering a setting
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
8. **The tenant limit.** `max_tenants` applies to the team scope, is not discoverable, and merges with `Min`. A creation
   past it answers `409`. The ceiling is checked before the insert and never serialized on the team row, so two
   creations that race may overshoot it by one.
9. **Authority runs platform, team, tenant, leaf.** `TeamAdmin` at a team creates its tenants and manages everything
   beneath it; `Owner` never creates. Granting or revoking `TeamAdmin` is `Operations.Create` at the team node, so an
   `Owner` can neither mint nor revoke it. `GlobalAdmin` acts across the deployment. Platform, team and tenant nodes
   have globally unique, retired identifiers, so an assignment matches them on type and identifier; any other node
   counts an assignment only within its tenant, because client and resource-server identifiers are unique only within a
   tenant.
10. **Tenant routes.** Tenants are created and listed under `/api/teams/{teamId}/tenants`. The root tenant holds the
    `platform_settings`, `platform_encryption_keys`, `teams`, `team_settings` and `tenants` scope families.
11. **Lifecycle.** A team's lifecycle (create, disable, restore, delete) is authority at the node above it, which is the
    platform for a top-level team. Platform authority or `GlobalAdmin` lists teams and tenants across teams. A disabled
    team's tenants answer `404` on the protocol surface and on the management routes that act through a tenant's runtime
    (sessions, principals, local accounts), as a disabled tenant does, while their stored configuration stays
    manageable. A team with tenants, child teams or an owned machine client
    ([ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md)) cannot be deleted.
    A deleted team's identifier is retired.
12. **Audit and usage carry the team.** Usage rows and tenant audit events carry the team. Team and platform audit
    events are recorded in the control-plane tenant with the resource types `team` and `platform`. The platform and a
    team own settings but no secrets.
13. **The console names the scope first.** Platform surfaces sit at the root, team surfaces under `/teams/{teamId}/…`
    and tenant surfaces under `/tenants/{tenantId}/…`
    ([ADR-1255](1255-each-tenant-surface-of-the-management-console-has-its-own-address.md)); it opens on the caller's
    `/home`
    ([ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md)).
14. **The words.** "Platform" is the deployment root; the platform environments of ADR-1495 (`production`, `lab`, `ci`)
    are where a platform runs. "Team" is the grouping. "Organization" stays free for the in-tenant B2B feature.

## Options considered

- **Per-tenant databases with a control and tenant split and connection routing.** Rejected: it forces a bootstrap and
  registry split, cross-database reads when materializing a tenant, and a connection-routing seam, to put
  database-level isolation inside one deployment when running a second deployment gives the same isolation far more
  simply.
- **No tenant scoping at all.** Rejected: it abandons isolation entirely.
- **Name the grouping `organization`.** Rejected: it collides with the in-tenant B2B vocabulary and with the
  ecosystem's use of the word.
- **Name it `customer`.** Rejected: it is ambiguous for a consumer whose own customers are organizations.
- **Name it `tenant group`.** Rejected: it names the container by its contents, implies a many-to-many relationship,
  and collides with user groups.
- **A listable set of root rows.** Rejected: nothing routes a tenant to one of several roots, so a second root would
  have no tenant to serve; the platform is singular.
- **Move a tenant between teams.** Rejected: a tenant's team is part of its authority, usage and policy, so a customer
  that reorganizes recreates tenants.
- **One control plane, one database, a team grouping and logical isolation by query filter (chosen).** It is simple to
  build, operate and reason about, keeps the graph as plain foreign keys, and still allows strong physical isolation by
  running another deployment.

## Consequences

- The data model stays one foreign-key-connected graph in one `OpenIdDbContext`; nothing depends on a per-tenant
  database or a control and tenant connection split.
- Operators who need physical isolation run a separate deployment; capacity and blast radius are managed by how many
  tenants an instance hosts, like processes to a machine or namespaces to a cluster.
- A customer's policy is written once, at its team, and binds every tenant it creates; a plan's tenant quota has a scope
  to apply to; usage sums per team.
- The administrators of a team create tenants without holding authority over the deployment, and the platform
  administrator never needs to be a member of a customer's tenant.
- Every tenant has a team, so a tenant cannot be created without one and its creation path takes a team identifier.
- A disabled team switches off all of a customer's protocol surface and tenant runtime routes in one write, while its
  tenants' stored configuration stays manageable; a change to a team document reaches its cached tenants on every
  replica.
- A tenant never moves between teams.
- The `max_tenants` check can overshoot by one under a race, which is accepted because the row carries no serialized
  counter.

## References

- `OpenIdDbContext`, `TenantPlane.Root`, the `TenantResolution:Root:TeamId` and `OpenId:Platform:Settings` options
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md) — the
  ambient-tenant query filter
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) — roles and ownership
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)
  — security profiles down the cascade
- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md) — the registered
  setting scopes
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open
- [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md) — creator-named,
  retired identifiers
- [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) — tenant
  provisioning and seeding
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key hierarchy
- [ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md) —
  the platform master key
- [ADR-1255](1255-each-tenant-surface-of-the-management-console-has-its-own-address.md) — console addresses
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md) — nested teams
- [ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md) — the caller's home
- [ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md) — machine clients
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)
  — platform environments

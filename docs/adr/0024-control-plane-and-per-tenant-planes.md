# 24. Deployment model: a control plane and per-tenant planes, so a tenant can be isolated to its own database

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

Tenancy exists so that an operator can **optionally isolate a tenant onto dedicated infrastructure — including its own
database**. That goal forces a question the single-database model hides: if every tenant's data can live in its own
database, where does the data that is *about* tenants live? You cannot resolve a request to a tenant, or even enumerate
which tenants exist, if that index is scattered inside the per-tenant databases — a bootstrapping chicken-and-egg.

Today there is one `OpenIdDbContext` holding everything (server, tenants, clients, resource servers, grants, …) behind
a single connection ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) scopes access with a query
filter *within* that one database). Per-tenant-database isolation is a stated goal, not yet a built capability. This ADR
records the target deployment model so later work builds toward it, and so the `ResourceServer`/`Scope`/`ClientGrant`
model ([ADR-0023](0023-scopes-and-resources-management-model.md)) and the system-resource-server seeding are designed on
the right side of the boundary.

## Decision

**The system is modeled as a control plane plus N per-tenant planes. Anything that must exist to discover, route to, or
administer tenants lives in the control plane; everything a tenant needs to operate is self-contained in that tenant's
plane (and therefore relocatable to a dedicated database).**

- **Control-plane (global) database** holds:
  - the **tenant registry** — the index of which tenants exist and how to reach each (id, host/path, enabled, and, in a
    fully-isolated deployment, the tenant's connection/routing information). This is the bootstrap index consulted
    *before* any tenant database is opened;
  - the **server** — the deployment-root settings and secrets (the baseline tenants narrow, ADR-0010);
  - the **root/system tenant** and its **control-plane management resource server** (administering servers and
    provisioning tenants). The root tenant *is* the control plane's tenant.
- **Per-tenant (workload) database** holds a tenant's self-contained plane: its clients, resource servers, scopes,
  grants, client grants, the tenant's own settings and secrets, and the system resource servers seeded into it (the
  OpenID Connect identity resource server and the tenant-plane management resource server).
- **The boundary is what makes a tenant relocatable.** Because a workload tenant's plane depends on nothing outside its
  own database (except the control-plane registry that routes to it and the server baseline it inherits), that database
  can be lifted onto dedicated infrastructure without breaking references.
- **The root/system tenant = the control plane** ([ADR-0023](0023-scopes-and-resources-management-model.md) follow-on):
  `GlobalAdmin` is the root tenant's realm (manage servers + tenants), `TenantAdmin` is a workload tenant's realm.

### Realization is phased (the routing seam is future work)

The single-database deployment is the current, valid default. Reaching per-tenant databases requires, in order:

1. a **control/tenant split** — a control context (registry + server + root tenant) distinct from a per-tenant context
   (a tenant's resources);
2. **per-tenant connection routing** — the ambient tenant ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md))
   resolves the connection for the tenant's context, turning "scoping by query filter in one database" into "scoping by
   a separate database"; the query filter remains correct for shared-database deployments;
3. **cross-plane reads at materialization** — building an `OpenIdTenant` merges the server baseline (control plane) with
   the tenant's settings (tenant plane), so both must be reachable while materializing.

Until those exist, everything lives in one database and the boundary is logical, not physical — but the model is
designed so the physical split is an additive change, not a redesign.

## Options considered

- **One database, tenant-scoped by query filter only (today's model, kept as the default).** Correct and simple for
  shared-hosting, but cannot deliver database-level isolation. Retained as one deployment shape, not the ceiling.
- **Push the tenant registry into each tenant's own database.** Rejected: it is unbootstrappable — you cannot find or
  route to a tenant whose existence is only recorded inside its own (possibly remote) database.
- **Make everything global (no per-tenant database).** Rejected: it abandons the isolation goal that motivated tenancy.
- **A control/tenant-plane split with a connection-routing seam (chosen).** Delivers optional per-tenant databases while
  keeping the shared-database deployment as a special case (all planes in one database).

## Consequences

- There is, by necessity, a control-plane (global) database: the tenant registry, the server, and the root/system
  tenant. A tenant cannot be fully self-contained *including* the fact of its own existence — that fact is control-plane.
- A workload tenant's plane is self-contained and relocatable to a dedicated database; the control plane routes to it.
- System resource servers are seeded into the *workload* tenant's plane (OIDC, tenant-plane management), while the
  control-plane management resource server (servers/tenants) belongs to the root tenant in the control database — which
  is why seeding and the management surface are split along this boundary.
- The multi-database routing itself is unbuilt; the current single-`OpenIdDbContext` deployment remains valid, and the
  split is an additive, phased change.

## References

- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the server baseline that tenants narrow, read
  across the control/tenant boundary at materialization.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the ambient-tenant scoping that becomes the
  connection-routing seam for per-tenant databases.
- [ADR-0023](0023-scopes-and-resources-management-model.md) — the resource server / scope / client grant model designed
  on the tenant side of this boundary, and the root/system tenant this ADR places in the control plane.

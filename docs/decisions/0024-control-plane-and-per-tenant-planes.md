# 24. Deployment model: one control plane with logical tenant isolation; physical isolation is a separate deployment

- **Status:** Accepted
- **Date:** 2026-10-01
- **Deciders:** NCode Group

## Context

Tenancy exists so that one running server can host many tenants without their data bleeding across. The real question
is how strong that isolation must be, and where to draw the line between **logical** isolation (one deployment, one
database, tenants separated by scoping) and **physical** isolation (separate storage/infrastructure).

Everything today lives in one `OpenIdDbContext` behind a single connection: the server, the tenants, and every tenant's
clients, resource servers, scopes, and grants, joined by foreign keys.
[ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) scopes tenant-bound access with a global query
filter _within_ that one database. This ADR records how far that model is meant to go — and, deliberately, where it
stops.

## Decision

**The system is a single control plane over a single physical deployment (one database, one `OpenIdDbContext`), in which
tenants are a _logical_ isolation boundary, not a physical one. The server, the tenants, and every tenant's child
resources are one connected graph with ordinary foreign-key relationships, and tenant isolation is enforced logically by
the ambient-tenant query filter ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)). When an
operator genuinely needs physical isolation (compliance, blast-radius, noisy-neighbor), they deploy a separate
infrastructure instance of the whole server — they do not give a tenant its own database.**

- **Logical isolation, like a namespace or a process.** A tenant is the OAuth/OIDC analogue of a Kubernetes namespace or
  an OS process: a scoping boundary _inside_ one running system, cheap to create, with the "kernel" — here, the query
  filter — keeping tenants from seeing one another. It is not a separate machine.
- **One control plane manages servers and tenants together.** The control plane is the central administrative surface:
  it manages the server (the deployment-root settings and secrets tenants narrow,
  [ADR-0010](0010-supported-settings-unset-means-unrestricted.md)) and provisions/administers tenants. `GlobalAdmin` is
  its realm (manage servers + tenants); `TenantAdmin` is a workload tenant's realm (manage that tenant's clients,
  resource servers, scopes, and grants). These are management _realms_ over one database, not separate databases.
- **The root/system tenant is the control plane's tenant**
  ([ADR-0023](0023-scopes-and-resources-management-model.md)): the control-plane management resource server
  (servers + tenants) is seeded into it
  ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)), while the OpenID identity and
  tenant-plane management resource servers are seeded into each workload tenant. "Control-plane" and "tenant-plane" name
  these logical roles, not physical planes.
- **Foreign keys stay.** Because everything is co-resident, the server → tenant → (clients, resource servers, scopes,
  grants) graph is modeled with ordinary foreign keys and referential integrity; there is no cross-database link to
  engineer around.
- **Physical isolation is a deployment concern, not a data-model concern.** A tenant that must be physically separated
  becomes its own deployment: a separate server instance with its own database (typically hosting that single tenant).
  The application model does not change; the operator simply runs another copy.

## Options considered

- **One control plane, one database, logical tenant isolation by query filter (chosen).** Simple to build, operate, and
  reason about; keeps the server/tenant/child graph as plain foreign keys; and still allows strong physical isolation by
  running another instance.
- **Per-tenant databases, with a control/tenant split and connection routing.** Rejected: it forces a bootstrap/registry
  split, cross-database reads when materializing a tenant, and a connection-routing seam — substantial complexity to put
  database-level isolation _inside_ one deployment, when deploying a second instance achieves the same isolation far
  more simply. (This was explored as an earlier direction and is explicitly not pursued.)
- **Make everything global (no tenant scoping).** Rejected: it abandons isolation entirely.

## Consequences

- The data model stays a single, foreign-key-connected graph in one `OpenIdDbContext`; tenant isolation is the
  [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) query filter, and nothing on any path depends
  on a per-tenant database or a control/tenant connection split.
- "Control plane" versus "tenant plane" is a _logical_ distinction (management realm plus the system resource servers
  seeded into each), which is why the management surface and system-resource-server seeding split along
  `GlobalAdmin`/`TenantAdmin`
  ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)) even though all rows share one
  database.
- Operators needing physical isolation run a separate server deployment; capacity and blast-radius are managed by how
  many tenants an instance hosts — like processes to a machine, or namespaces to a cluster.
- The library carries no per-tenant-database routing, projection, or cross-plane materialization machinery, which keeps
  it simpler to build and maintain.

## References

- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the server baseline that tenants narrow, read when
  materializing a tenant within the one database.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the ambient-tenant query filter that is the
  sole tenant-isolation mechanism.
- [ADR-0023](0023-scopes-and-resources-management-model.md) — the resource server / scope / client grant model and the
  root/system tenant that is the control plane's tenant.
- [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md) — the control-plane management
  resource server (servers + tenants) seeded into the root tenant.

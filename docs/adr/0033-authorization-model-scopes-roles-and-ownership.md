# 33. Authorization model: granular scopes, roles as bundles, and resource-scoped assignments (ownership)

- **Status:** Accepted
- **Date:** 2026-10-01
- **Deciders:** NCode Group

## Context

The management surface needs an authorization model that answers three questions at once, which earlier pieces only
answered partially:

- **Granularity.** Operators want fine-grained delegation — a "secrets manager" who can rotate a tenant's secrets but
  nothing else, a "settings manager," and so on — not just a coarse all-or-nothing admin.
- **Self-service of the thing you own.** A principal must be able to manage a resource it is responsible for even
  without tenant-wide authority. The model is Entra ID's object ownership: _"I cannot create App Registrations, but I
  can own one and manage it."_
- **Per-resource authority.** "Joe is an admin of tenant-a but not tenant-b" — authority is granted **at a specific
  resource**, not globally.

Today there are two coarse realms — `GlobalAdmin` (grants everything) and `TenantAdmin` — and a flat set of management
scope families (`clients`, `resource_servers`, `client_grants`, `grants`, `servers`, `tenants`). This has three gaps:
the `tenants` family conflates tenant **creation and cross-tenant enumeration** (global acts at the server root) with a
tenant **managing its own existing tenant** (a self-service act), so a `TenantAdmin` cannot manage its own tenant; the
scopes stop at the entity, not its leaves (settings, secrets), so "secrets manager" is inexpressible; and `TenantAdmin`
is a global claim rather than an authority bound to a particular tenant.

The resource model ([ADR-0023](0023-scopes-and-resources-management-model.md)) and the single-control-plane deployment
([ADR-0024](0024-control-plane-and-per-tenant-planes.md)) already give us a resource hierarchy and a `GlobalAdmin`
(everything) / `TenantAdmin` (a tenant) distinction. The pieces above are not three features; they are one model.

## Decision

**Authorization is resource-scoped role assignments over a resource hierarchy — Azure-RBAC-style assignments unified
with Entra-style object ownership. A _scope_ is the granular, per-leaf permission; a _role_ is a named bundle of
scopes; an _assignment_ grants a principal a role at a node of the resource hierarchy, inherited downward. Ownership is
simply an `Owner`-role assignment at a specific instance.**

### Resource hierarchy

```
server (root)
└── tenant
    ├── client            (leaves: client, client_secrets)
    ├── resource_server   (leaves: resource_server, scopes)
    └── grant
```

Each node has the entity itself plus its **leaves** (notably `settings` and `secrets`). Authority assigned at a node
applies to that node and everything beneath it.

### Scopes are granular and per-leaf

Management scopes are `{verb}:{family}` where a family is an entity **or one of its leaves**. The control-plane families
(the server and tenant **provisioning**) are seeded only into the root tenant; the tenant-plane families (a tenant's own
settings/secrets and its child resources) are seeded into every tenant. Examples:

- Control (root tenant): `servers`, `server_settings`, `server_secrets`, and the server-root verbs of `tenants` —
  **creating** a tenant and **enumerating/reading across all** tenants. Managing an _existing_ tenant you administer —
  read/update/delete/deactivate it, plus its settings, secrets, and children — is authority at that tenant node (below),
  not a server-root act.
- Tenant-plane (every tenant): `tenant_settings`, `tenant_secrets`, `clients`, `client_secrets`, `resource_servers`,
  `client_grants`, `grants`.

### Roles are bundles of scopes

A role is a named set of scopes. Built-in roles are convenience super-bundles; custom roles are any subset:

- **`GlobalAdmin`** — every scope (the whole server). "Global = everything."
- **`TenantAdmin`** — every tenant-plane scope (full authority over its tenant).
- **`Owner`** — manage a single owned instance: read/update/delete it, manage its leaves (secrets), and manage its
  owners; **not** create (see below).
- **Custom** (for example `SecretsManager` = `*:tenant_secrets`) — any subset, defined by operators. This is what the
  granularity above exists to enable, with no new code.

### Assignments bind a role to a principal at a resource node

An assignment is `(principal, role, resource-node)` and is inherited down the hierarchy. The same mechanism expresses
every case:

| Intent                              | Assignment                           |
| ----------------------------------- | ------------------------------------ |
| `GlobalAdmin` (everything)          | all-scopes role **@ root (server)**  |
| Joe is admin of tenant-a, not -b    | admin role **@ tenant-a**            |
| Joe owns app X                      | `Owner` role **@ client X**          |
| Joe manages tenant-a's secrets only | `SecretsManager` role **@ tenant-a** |

**Ownership is an `Owner`-role assignment at an instance.** The **creator of a resource is auto-assigned `Owner`**. A
resource must always have **at least one owner** (removing the last owner is refused; `GlobalAdmin`/`TenantAdmin` can
always reassign). Owners **manage but do not create** — creating a new resource is a type-level act (a tenant-wide
scope) or, later, a request/approval flow.

### Principals

A principal is a **subject (user)** today and a **service principal (client)** later; the assignment and ownership
model is defined over an abstract principal so the client case is additive. The principal's durable, server-owned
identity — one human aggregating many external connection-identities, referenced by an opaque `PrincipalId` — is decided
in [ADR-0035](0035-federated-principals-and-identity-resolution.md).

### The authorization decision

A caller may perform an operation on resource `R` if **any** of the following hold: it is `GlobalAdmin`; it holds a role
at `R` or an ancestor of `R` whose scopes cover the operation (this subsumes `TenantAdmin` @ the tenant and `Owner` @
the instance). The check is the union of type-level (role/scope) and instance-level (ownership) authority — they are the
same mechanism at different scopes.

### Realization is phased

- **Phase 1 — granular type-level authorization (built-in roles).** The per-leaf scope families above; a single
  management resource server (`urn:ncode:management`) with the provisioning families seeded only into the root tenant;
  the tenant self-service fix (a `TenantAdmin` fully manages its own tenant — the registry row itself, including
  deactivate and delete, plus its settings, secrets, and child resources — while **creating** tenants and
  **cross-tenant** enumeration remain `GlobalAdmin`); `GlobalAdmin` = everything, `TenantAdmin` = full authority over
  its tenant — still via the current realm handlers. Immediately usable.
- **Phase 2 — data-driven scoped assignments and ownership.** Persist `(principal, role, resource-node)` assignments
  with inheritance; the `Owner` role, creator-as-owner, owner-list management, and an ownership authorization handler.
  Generalizes the built-in realms into per-resource, data-driven authority (enables "Joe admin of tenant-a," custom
  roles, and client/resource-server owners).
- **Phase 3 — request/approval.** Request a resource you cannot create; an approver provisions it and assigns you as
  owner.

## Options considered

- **Keep two coarse realms (`GlobalAdmin`/`TenantAdmin`) only.** Rejected: no granular delegation, no per-resource
  authority, and no self-service of owned instances.
- **Roles and ownership as two separate subsystems.** Rejected: once tenants are ownable, "admin of tenant-a" and
  "owner of app X" are the same shape — a role at a resource scope. Two subsystems would duplicate evaluation,
  storage, and mental model.
- **Resource-scoped role assignments unifying roles and ownership (chosen).** One hierarchy, one assignment concept,
  built-in roles as sugar over custom scope bundles, ownership as an `Owner` assignment at an instance. Matches the
  Auth0/Azure/Entra prior art ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) and lands incrementally.

## Consequences

- The `tenants` scope family splits into tenant **creation / cross-tenant enumeration** (control plane, root tenant,
  `GlobalAdmin`) and **managing an existing tenant you administer** — the registry row itself (including deactivate and
  delete), its `tenant_settings`/`tenant_secrets`, and its child resources, at the tenant node — closing the hole where
  a `TenantAdmin` could not manage its own tenant.
- The management surface collapses to a **single resource-server audience** (`urn:ncode:management`); the control/tenant
  distinction becomes _which scopes are seeded where_, not a second audience (revises
  [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)).
- Granular per-leaf scopes make operator-defined roles (secrets manager, settings manager, …) expressible with no code
  change — the payoff is realized in Phase 2 when assignments become data-driven.
- A resource carries an **owner set** (Phase 2); creation assigns the creator, and the last owner cannot be removed.
- The built-in realms remain valid as the Phase-1 implementation and as the top of the role hierarchy; Phase 2
  generalizes, it does not discard them.

## References

- [ADR-0023](0023-scopes-and-resources-management-model.md) — the resource server / scope / client grant model and the
  resource hierarchy this authorization model scopes assignments over.
- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the single control plane; `GlobalAdmin`/`TenantAdmin` as the
  top of the role hierarchy.
- [ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md) — scope enforcement via resource servers
  and client grants, which these management scopes extend to the management API.
- [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md) — the control-plane management
  resource server, revised by Phase 1 into one audience with root-only provisioning scopes.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the Auth0-parity-plus posture this model follows
  (familiar RBAC/ownership shapes, more granular and data-driven).

# 34. Persisted resource-scoped role assignments and ownership

- **Status:** Accepted
- **Date:** 2026-10-01
- **Deciders:** NCode Group

## Context

[ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md) decides the authorization _model_: authority is a
role assigned to a principal at a node of the resource hierarchy, inherited downward, and ownership is simply an
`Owner`-role assignment at a specific instance. It also decides that this lands in phases — granular type-level roles
first (via the existing realm handlers), then **data-driven scoped assignments and ownership**, then request/approval.

That model needs a concrete, durable realization: where an assignment is stored, how a resource names the node it lives
at, how the ancestor chain is walked at an authorization decision, what the built-in `Owner` role is allowed to do, and
how the "a resource always has at least one owner" invariant is enforced. The existing `GlobalAdminHandler` (a role
claim) and `TenantAdminHandler` (a role claim plus a `tid` match) already cover the two coarse realms; ownership adds
_per-instance_ authority that no claim can express because it is specific to a single resource and is granted at
creation time.

## Decision

**Role assignments are a persisted, tenant-scoped record of `(principal, role, resource-node)`; an `OwnershipHandler`
grants a principal authority over a resource when it holds an assignment at that resource's node or an ancestor. The
creator of a resource is assigned the `Owner` role at the new instance, and a resource keeps at least one owner.**

### The assignment record

A `RoleAssignmentEntity` is a tenant-scoped entity (`ISupportTenantEntity`), so every assignment for a tenant's
resources — including the tenant node itself — lives in that tenant and inherits the persistence-layer tenant scoping
([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)). Server-root assignments live in the root
tenant ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)). Its columns:

- `PrincipalId` / `NormalizedPrincipalId` — the subject identifier (`sub`); a service principal (client) later. The
  model is defined over an abstract principal so the client case is additive.
- `RoleName` — the role (a name, not a foreign key), so operator-defined custom roles are additive without a schema
  change.
- `ResourceType` + `ResourceId` / `NormalizedResourceId` — the **node** the authority is granted at: a resource type
  (`server`, `tenant`, `client`, `resource_server`, `grant`) and the natural identifier of that node. The server-root
  node uses the `server` type and the root tenant id.
- Standard `ResourceId` (the assignment's own opaque id, [ADR-0014](0014-server-generated-opaque-resource-ids.md)) and
  an interceptor-managed `ConcurrencyToken` ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)).

A unique index over `(TenantId, NormalizedPrincipalId, RoleName, ResourceType, NormalizedResourceId)` makes an
assignment idempotent; a lookup index over `(TenantId, ResourceType, NormalizedResourceId)` serves both the
authorization decision (assignments at a node) and owner-list management.

### The ancestor chain

A resource's node has a fixed ancestor chain: an instance (`client` / `resource_server` / `grant`) → its `tenant` →
the `server` root. The chain is derived from the resource itself — a management resource already exposes its
`ResourceType`, `ResourceId`, and `TenantId` — so no parent pointers are persisted on the assignment. An assignment at
any node in the chain applies to the resource.

### The `Owner` role and the decision

`Owner` means **manage a single owned instance**: read, update, and delete it, manage its leaves (settings, secrets),
and manage its owners — but **not** create (creating a sibling is authority at the parent node). The `OwnershipHandler`
therefore succeeds any requirement **except `Create`** when the caller's principal holds an assignment at the
resource's node or an ancestor. It is additive: it runs alongside `GlobalAdminHandler` and `TenantAdminHandler`, and
ASP.NET Core authorization succeeds when _any_ handler succeeds, so the three together are the union of global, tenant,
and per-instance authority.

Per-scope custom roles (a `SecretsManager` that covers only `*:tenant_secrets`) are a later, additive refinement: the
assignment already carries a role name, so introducing a data-driven role→scope catalog and a per-scope coverage check
does not change the record or the creator/owner wiring.

### Creator-as-owner and the one-owner invariant

Creating a resource assigns the creator the `Owner` role at the new instance **in the same unit of work** as the
insert, so a resource is never ownerless. Owner-list management (list/add/remove owners of a resource) refuses to
remove the last `Owner`; `GlobalAdmin` and `TenantAdmin` can always reassign ownership, so a resource cannot become
unmanageable.

## Options considered

- **Persisted `(principal, role, resource-node)` assignments with an additive ownership handler (chosen).** One record
  expresses global, tenant, and instance authority; ownership is a role at an instance; the built-in realm handlers are
  unchanged and the custom-role engine is a later, additive layer.
- **A dedicated `ownership` table separate from role assignments.** Rejected: ownership and "admin of tenant-a" are the
  same shape — a role at a node — so a separate table duplicates storage and evaluation, exactly the split
  [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md) unifies.
- **Persist the full ancestor chain (parent pointers) on each assignment.** Rejected as redundant: the hierarchy is
  fixed and derivable from the resource, so storing it invites drift without buying a faster decision at this scale.
- **Migrate `GlobalAdmin`/`TenantAdmin` to data-driven assignments now.** Rejected for this step: the claim-based realm
  handlers already work and seeding every deployment with root/tenant assignments is a larger change; the ownership
  handler is purely additive and the realms can be generalized later without reworking it.

## Consequences

- A new tenant-scoped `RoleAssignmentEntity` / `PersistedRoleAssignment` / `IRoleAssignmentStore` joins the persistence
  layer; the DTO and store follow the existing resource conventions and inherit tenant scoping automatically.
- Resource creation gains a second write (the creator's `Owner` assignment) in the same unit of work; every create
  endpoint that mints an owned resource participates.
- Authority becomes the union of three additive handlers; a caller that owns an instance can manage it without any
  tenant-wide role, realizing the Entra-style "I can own one and manage it" case.
- The one-owner invariant makes ownership safe to delegate: owners can hand off but cannot orphan a resource, and the
  tenant/global realms remain a backstop.
- Per-scope custom roles, service-principal principals, and request/approval remain future, additive work; none of them
  revise this record.

## References

- [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md) — the authorization model this realizes: roles as
  scope bundles, assignments at resource nodes, ownership as an `Owner` assignment.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the tenant-scoped persistence that the
  assignment entity inherits.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — the opaque resource ids the assignment uses.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor-managed concurrency token the assignment
  carries.
- [ADR-0015](0015-management-core-validation-via-validators.md) — the validator seam through which creator-as-owner and
  the one-owner invariant are enforced.

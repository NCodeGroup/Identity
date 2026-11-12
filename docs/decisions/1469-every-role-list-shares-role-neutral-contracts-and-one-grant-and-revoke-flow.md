# 1469. Every role list shares role-neutral contracts and one grant and revoke flow

- **Status:** Accepted
- **Date:** 2026-11-07
- **Deciders:** NCode Group
- **Refines:** [ADR-1465](1465-a-role-list-tells-its-reader-whether-it-may-grant-and-revoke-the-role.md),
  [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md),
  [ADR-1449](1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md),
  [ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)

## Context

The management API serves a role list at each node that holds one: the owners of a team, a tenant, a client and a
resource server, and the administrators of a team and a tenant. Each list is read with `GET`, granted with `POST` and
revoked with `DELETE …/{principalId}`. Every list item and every grant's answer has the same shape whatever the role:
the assignment's identifier, its principal and its role name; a grant's body names only the principal.

A grant and a revoke follow the same flow for every role: authorize the caller at the node, read, write, commit and
audit. Only the role name and the operation that authorizes the change differ: `Update` for owners and `Create` for
administrators ([ADR-1465](1465-a-role-list-tells-its-reader-whether-it-may-grant-and-revoke-the-role.md)). Revoking
an owner adds the one-owner rule, which refuses to remove a resource's last owner
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)). Every grant can be refused: a client's principal holds a
role only as a machine client, at its owning node or beneath it, and the ownership service refuses any other grant to
it ([ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)).

`GET /api/me` lists a caller's role assignments across nodes as `RoleAssignmentResource`, which names each
assignment's node ([ADR-1420](1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md)).

## Decision

**A role list's contracts name the holder of any role, and one flow grants and revokes every role at a node.**

1. **A role list's item and a grant's answer are `RoleHolderResource { assignmentId, principalId, roleName }`, and a
   grant's body is `AddRoleHolderRequest { principalId }`,** for every role list. The route names the node, so an item
   does not repeat it, and the audit record of a grant or revoke carries the same three members
   ([ADR-1449](1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md)).
2. **`RoleAssignmentResource` is the contract of an assignment read across nodes,** and names its node.
3. **One flow grants every role.** The flows take one `RoleList` that names the node, the role, the operation that
   authorizes the change, the list's path and the check that the resource exists. The grant authorizes the operation at
   the node, answers `400` for a request that names no principal and `404` for a missing resource, and writes the
   assignment. It answers `409` when the ownership service refuses the grant, saving and recording nothing
   ([ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)); `200` with the
   existing assignment when the principal already holds the role; or `201` at `<list path>/<principalId>`, recording
   the grant.
4. **One flow revokes every role but the owner.** Given the `RoleList`, it authorizes the operation at the node,
   answers `404` when the principal does not hold the role, and otherwise commits the removal, records each removed
   assignment and answers `204`.
5. **An owner is revoked through its own flow,** because the one-owner rule (`409`) depends on the list rather than
   the caller.
6. **A route whose resource adds refusals applies them before the shared flow,** as a client's owners refuse a client
   the host manages (`409`) and, for a machine client, a caller without creation authority at the node that owns it
   (`403`) ([ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)).

## Options considered

- **Owner-named contracts for every list.** They misname an administrator list's items and grants. Rejected.
- **One contract per role.** The shape is the same for every role, and a package that adds a role would add contracts
  to a vocabulary that is open ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). Rejected.
- **`RoleAssignmentResource` for role-list items.** Each item, and the audit record's values, would repeat the node
  that the route and the audit record already name. Rejected.
- **A grant and a revoke per handler and role.** Each copy repeats the authorize, read, write and audit flow and can
  drift from the others in its statuses or its audit. Rejected.
- **Role-neutral contracts and one flow.** Chosen.

## Consequences

- Every role list answers the same statuses and records the same audit events for a grant and a revoke, and refuses a
  client's principal that cannot hold its role the same way.
- A role list for another role is a route that describes its list as a `RoleList` and calls the shared flow.
- A browser client reads and grants every role list through one set of types.

## References

- [ADR-1465](1465-a-role-list-tells-its-reader-whether-it-may-grant-and-revoke-the-role.md) — a role list tells its
  reader whether it may grant and revoke the role
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) — persisted resource-scoped role assignments and
  ownership
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) — a tenant administrator is a
  role assignment at the tenant's node
- [ADR-1449](1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md) — every role assignment
  the management API grants or revokes is audited
- [ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md) — custody of a
  machine client needs creation authority at the node that owns it
- Issues #627 and #628

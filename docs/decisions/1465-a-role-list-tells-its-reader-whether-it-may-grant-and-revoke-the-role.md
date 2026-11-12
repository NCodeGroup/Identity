# 1465. A role list tells its reader whether it may grant and revoke the role

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md),
  [ADR-1034](1034-persisted-role-assignments-and-ownership.md),
  [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md),
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md)

## Context

The management console lists who holds a role at a node and offers to grant and revoke the role. Whether the caller may
grant or revoke depends on the caller:

- The owners of a node are granted and revoked with `Update` at the node.
- The administrators of a team or tenant are granted and revoked with `Create` at the node.
- The owners of a client are also refused for a client the host manages (`409`), and for a caller without custody of
  the roles the client's principal holds
  ([ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md)).

A console that offers grant and revoke to every caller learns that the caller may not use them only from the `403`.

## Decision

**A role list answers whether its reader may change it, in the list itself.**

1. **`GET …/owners` on a team, tenant, client and resource server, and `GET …/admins` on a team and tenant, answer
   `RoleListResource { items, canChange }`.**
2. **`canChange` is whether a grant or a revoke of that role at that node by the caller passes every refusal that does
   not depend on the principal named.** Those are the rules listed in Context: `Update` at the node for owners,
   `Create` for administrators, and for a client's owners also a client the host does not manage and custody of the
   roles its principal holds.
3. **The route authorizes `Read` at the node before it reads
   ([ADR-1451](1451-every-management-route-authorizes-at-its-node-before-it-reads.md)).** It answers `404` for a
   missing resource and carries no ETag and no continuation token, because the answer is specific to the caller and the
   list is unpaged.
4. **The last-owner rule depends on the principal revoked, so it stays a `409`.**
5. **The console offers the grant form and the revoke controls only when `canChange` is true.** It shows the list
   read-only otherwise, and refetches every role list after any grant or revoke.

## Options considered

- **A sibling `…/owners/authority` resource.** It costs two requests. A role list is no audit snapshot and has no ETag
  to keep pure, the forces that put setting limits in a sibling
  ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)). A literal segment under `owners/` shadows
  a principal of that name. Rejected.
- **Separate `canGrant` and `canRevoke`.** Granting and revoking a role at a node need the same authority and the same
  refusals, so the two are always equal. Rejected.
- **A per-assignment `canRevoke`.** Revoke authority is the same for every assignment. The one per-assignment rule is
  the last owner, which depends on the list, not the caller. Rejected.
- **Computing the answer in the browser from `GET /api/me`.** It duplicates the authority chain and custody. Rejected.
- **The answer in the list.** Chosen.

## Consequences

- One request serves each list, and the console shows grant and revoke only to a caller who may use them.
- The authority chain stays in the server: the browser reads one boolean and never derives authority.
- Every role list is invalidated by any role change, since the caller's authority over any list can change.

## References

- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — a settings reader learns which limits the
  settings carry and whether it may change them
- [ADR-1034](1034-persisted-role-assignments-and-ownership.md) — persisted resource-scoped role assignments and
  ownership
- [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) — a tenant administrator is a
  role assignment at the tenant's node
- [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md) — a client acting for itself
  is a principal that holds role assignments
- [ADR-1451](1451-every-management-route-authorizes-at-its-node-before-it-reads.md) — every management route authorizes
  at its node before it reads
- Issue #623

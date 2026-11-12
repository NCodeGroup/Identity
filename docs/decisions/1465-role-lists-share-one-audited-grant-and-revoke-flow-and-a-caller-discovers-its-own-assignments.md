# 1465. Role lists share one audited grant and revoke flow, and a caller discovers its own assignments

- **Status:** Accepted
- **Date:** 2026-11-07
- **Deciders:** NCode Group

## Context

Management authority is a role assignment at a node of the platform, team and tenant hierarchy
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)).
The management console lists who holds a role at a node and offers to grant and revoke it, and it must show a caller
the places where the caller holds a role. The forces:

- Four kinds of node keep an owners list and two keep an administrators list. Handlers that each repeat the grant and
  revoke logic diverge in their statuses, their audit records and their refusals.
- Whether the caller may grant or revoke depends on the caller. Owners of a node are changed with `Update` at the
  node, administrators with `Create`, and the owners of a client are also refused for a client the host manages and for
  a caller without custody of a machine client. A console that offers grant and revoke to every caller learns that the
  caller may not use them only from the `403`.
- Authority is granted and removed by assignments, so the changes belong in the audit trail, whichever route or
  handler makes them.
- A caller holds roles at many nodes, and the token names no team or tenant. The console needs a way to ask which
  teams and tenants the caller reaches.
- Computing any of this in the browser would duplicate the server's rules and drift from them.

## Decision

**Every role list uses the same role-neutral contracts and one grant and revoke flow; each grant and revoke is
audited; and a caller reads its own assignments, teams and tenants from `GET /api/me`.**

### Routes and contracts

1. **The role lists are `…/owners` on a team, tenant, client and resource server and `…/admins` on a team and tenant.**
   `GET` reads the list, `POST` grants and `DELETE …/{principalId}` revokes.
2. **The contracts name no role and no owner.** `GET` answers `RoleListResource { items, canChange }`. An item, and the
   answer to a grant, is `RoleHolderResource { assignmentId, principalId, roleName }`. A grant's body is
   `AddRoleHolderRequest { principalId }`. `RoleAssignmentResource`, which names its node, serves the assignments that
   are read across nodes.
3. **`canChange` says whether the caller's grant or revoke of the role at the node passes every refusal that does not
   depend on the principal named.** Those refusals are `Update` at the node for owners, `Create` for administrators,
   and, for a client's owners, a client the host does not manage and custody of a machine client
   ([ADR-1466](1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md)). The last-owner
   rule depends on the principal, so it stays a `409` on the revoke.
4. **A list route authorizes `Read` at the node before it reads** (see
   [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md))
   and answers `404` for a missing resource. It carries no ETag and no continuation token, because the answer is
   specific to the caller and unpaged.

### The grant and revoke flow

5. **One flow grants every role.** A `RoleList` drives it: the node, the role, the operation that authorizes, the list
   path and the check that the resource exists. The flow authorizes, then answers:
   - `400` when no principal is named;
   - `404` for a missing resource;
   - `409` when the ownership service refuses, saving and recording nothing;
   - `200` with the existing assignment when the principal already holds the role, recording nothing;
   - `201` at `<list path>/<principalId>` otherwise, recording the grant.
6. **One flow revokes every role but `Owner`.** It authorizes, answers `404` when the principal does not hold the role,
   and otherwise commits, records each removed assignment and answers `204`. Owners have a flow of their own for the
   `409` that keeps one owner.
7. **A resource's extra refusals run before the shared flow.** A client's owners refuse a host-managed client (`409`)
   and a caller without custody of a machine client (`403`) first.
8. **`IResourceOwnershipService` reports what it changed.** It returns `RoleAssignmentGrant` (`IsAdded`, `IsRefused`),
   the removed assignments and `OwnerRemoval`, so the flow records exactly what happened.

### Audit

9. **Every grant and revoke records a `RoleAssignmentChangedAuditEvent`** (see
   [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)). Its resource type is
   `role_assignment` (`ManagementResourceTypes.RoleAssignment`), its change type `created` or `deleted` and its
   resource id the assignment id. Its resources are the assignment, then its node (`NodeType`, `NodeId`). Its
   `subject_id` is the principal, and its `resource_values` are `assignmentId`, `principalId` and `roleName`.
10. **The event's tenant is the assignment's**, the control-plane tenant for a team node, and its actor is the acting
    administrator or client.
11. **The endpoint handler records the event after the commit**, for the owners and admins routes, the creator's
    `Owner` assignment and a new team's `TeamAdmin` assignment. Assignments removed with their node are covered by the
    node's own `deleted` event.

### The caller's own assignments

12. **`GET /api/me` returns `CallerResource`.** `principalId` is null when none resolves, `isPlatformAdministrator` is
    true when the caller holds `Operations.Read` at the platform node, and `assignments` are
    `RoleAssignmentResource` items. How the endpoint authenticates the caller is
    [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)'s.
13. **`GET /api/me/teams` and `GET /api/me/tenants`** each return one `CollectionResource` page of the teams or tenants
    where the caller holds any assignment, ordered by identifier. The tenants are returned without `environment`.
14. **Authority stays per request.** The token names no team or tenant, so one token spans every place the caller
    holds a role, and a grant or a revocation applies on the next request.

### The console

15. **The console shows grant and revoke only when `canChange` is true**, and refetches every role list after any role
    change.
16. **The console opens on `/home`.** It lists "Open the platform" for a platform administrator and the caller's teams
    and tenants, and opens a sole destination directly, replacing the history entry. `/home` is not a management
    scope.
17. **A header Home control returns to `/home`.** A platform route becomes `/home` once the caller is known to lack
    platform authority. Back controls lead to the parent the caller can reach: the platform's team list for a platform
    administrator, a tenant's team for a platform administrator or a role holder there, and otherwise Home.

## Options considered

- **A sibling `…/owners/authority` resource.** The console would make a second request per list and the two answers
  could disagree. Rejected.
- **Separate or per-assignment grant and revoke flags.** The refusals that do not depend on the principal are the same
  for every item, so one flag on the list says it all. Rejected.
- **Computing authority in the browser.** It repeats the server's rules and drifts from them. Rejected.
- **Owner-named or per-role contracts, or `RoleAssignmentResource` items in a role list.** Each new role would add
  contracts, and a list at one node has no use for the node on every item. Rejected.
- **A flow per handler.** The statuses, refusals and audit records would diverge. Rejected.
- **Auditing in the store, or a per-node `role.granted` change type.** The store does not know the actor, and a change
  type per node multiplies the vocabulary of one fact. Rejected.
- **A team claim, token or audience.** A token would then span one team, and a caller with roles in several would need
  one per place, with a stale claim after every grant or revocation. Rejected.
- **Probing endpoints from the console to find where the caller may act.** It costs a request per place and shows a
  `403` as a failure. Rejected.
- **Chosen:** role-neutral contracts, one grant and revoke flow, `canChange` on the list, an audit event recorded after
  the commit, and `GET /api/me` for the caller's assignments.

## Consequences

- A new role list is a `RoleList` descriptor and a route, with no new contracts, statuses or audit code.
- The console never shows an action the caller's `403` would refuse, except where the principal named decides, as the
  last-owner `409` does.
- Every change to who holds authority has an audit record with its node and its principal, whichever route made it.
- A caller sees every team and tenant where it holds a role from one token, and a grant or revocation shows on its next
  request.
- The `canChange` answer can disagree with a later request only when the caller's authority changes between them, and
  the request is then refused as usual.
- Role-list responses are never cached, since they carry no ETag.

## References

- Issues [#566](https://github.com/NCodeGroup/identity/issues/566),
  [#583](https://github.com/NCodeGroup/identity/issues/583),
  [#623](https://github.com/NCodeGroup/identity/issues/623),
  [#627](https://github.com/NCodeGroup/identity/issues/627) and
  [#628](https://github.com/NCodeGroup/identity/issues/628)
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md):
  management authority is a role assignment at a node.
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md): a
  reader learns which limits it may change.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md):
  authorization at the node before the read.
- [ADR-1466](1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md): custody of a machine
  client.
- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md): the audit record shape.
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md):
  how the management API authenticates a caller.

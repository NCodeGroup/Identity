# 1449. Every role assignment the management API grants or revokes is audited

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [ADR-1034](1034-persisted-role-assignments-and-ownership.md),
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)

## Context

Role assignments are the whole of management authority
([ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md)): who may read, update or delete a
node is decided by the assignments held at that node and above it. Every other change the management API makes is
recorded as an audit event naming the acting administrator, but granting and revoking an assignment is not, so the
change that decides who may make every other change leaves no record.

## Decision

**The management API records a `role_assignment.created` or `role_assignment.deleted` audit event for every role
assignment it grants or revokes.**

1. **One event type describes the change.** `RoleAssignmentChangedAuditEvent` has the resource type
   `role_assignment` (`ManagementResourceTypes.RoleAssignment`) and the change type `created` or `deleted`. Its resource
   id is the assignment id, and its resources are the assignment followed by its node (`NodeType`, `NodeId`).
   `subject_id` is the principal, and `resource_values` carries `assignmentId`, `principalId` and `roleName`. The tenant
   is the assignment's tenant, which is the control-plane tenant for a team node. The acting administrator or client is
   the actor. The event is recorded after the commit.
2. **Every write the management API makes to an assignment is recorded.** That covers the `/owners` and `/admins` grants
   and revokes of teams, tenants, clients and resource servers, the creator's `Owner` assignment when one of those is
   created, and the `TeamAdmin` assignments a team's creation adds.
3. **The ownership service reports what it changed.** A grant returns `RoleAssignmentGrant`, whose `IsAdded` is false for
   an assignment the principal already holds, a revoke returns the assignments it removed, and an owner removal returns
   `OwnerRemoval`. A grant the principal already holds answers `200 OK` with the existing assignment and records
   nothing, and a grant that adds answers `201 Created` and records `created`.
4. **An assignment removed with its node is covered by the node's `deleted` event.** Deleting a team, tenant, client or
   resource server records the node's own event and no event per assignment.

## Options considered

- **A `role.granted` change type on each node's own event.** One query for every role change then needs one event name
  per node type. Rejected.
- **Auditing in the store.** The store does not know the request's actor and runs before the commit. Rejected.

## Consequences

- One audit query by resource type `role_assignment` lists every grant and revoke, and a query by `subject_id` lists
  every change to one principal's authority.
- A repeated grant changes nothing and leaves no event, so the log records changes of authority and not requests.
- The endpoint handlers, not the store, own the recording, so each event names the acting administrator and is recorded
  only once the change is committed.

## References

- [ADR-1034](1034-persisted-role-assignments-and-ownership.md) — persisted role assignments and ownership
- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) — audit records are
  CloudTrail-shaped JSON with an indexable envelope
- [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) — a tenant administrator is a
  role assignment at the tenant's node
- Issue #583 and the discussion on PR #580

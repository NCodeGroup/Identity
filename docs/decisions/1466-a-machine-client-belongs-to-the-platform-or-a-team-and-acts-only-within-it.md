# 1466. A machine client belongs to the platform or a team and acts only within it

- **Status:** Accepted (amends
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md) decision 3 and
  [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) decision 5)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

The management API accepts tokens only from the control-plane tenant
([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)). Creating a client there needs
creation authority at that tenant, which a `TeamAdmin` lacks. A team needs automation that acts only within it, yet a
client principal that is granted a role anywhere has no boundary. A control-plane client with an interactive grant could
collect the tokens of platform administrators.

## Decision

**A machine client is a control-plane-tenant client that the platform or one team owns, that obtains tokens only for
itself, and that holds roles only at its owning node or beneath it.**

1. **A machine client has one owning node.** It is a client of the control-plane tenant owned by the platform or by one
   team (`PersistedClient.OwningNodeType` and `OwningNodeId`, `ClientResource.owningNodeType` and `owningNodeId`). The
   owning node is set when the client is created and never changes.
2. **Its creation and listing routes are authorized at the owning node.** `POST` and `GET /api/teams/{teamId}/clients`
   create and list a team's machine clients with creation and read authority at the team node. `POST` and
   `GET /api/platform/clients` do the same for the platform's machine clients at the platform node. Each is authorized
   from the route before any read
   ([ADR-1451](1451-every-management-route-authorizes-at-its-node-before-it-reads.md)). A machine client is otherwise
   managed through the control-plane tenant's client routes
   ([ADR-1421](1421-the-management-console-addresses-a-client-and-a-resource-server-through-their-tenant.md)).
3. **It obtains tokens only for itself.** Its stored `grant_types_supported` is exactly `["client_credentials"]`,
   written when it is created, and a settings write that leaves anything else is refused `400`. It acts for no resource
   server, which is refused `400`.
4. **Its authority chain includes its owning node.** The chain of a machine client's node holds its owning node and
   every node above it, so the owner's administrators manage it. The chain reads the client row as it reads a tenant
   row to find the tenant's team, so an unknown client has no owning node and a caller without authority gets `403`
   whether or not the client exists
   ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md),
   [ADR-1451](1451-every-management-route-authorizes-at-its-node-before-it-reads.md)). The node above a machine client
   is its owning node, so the limits of its settings are set there
   ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)). Its creator is assigned no `Owner` role.
5. **Only a machine client's principal holds roles, and only within its owning node.** A role is granted to a client's
   principal only at its owning node or beneath it
   ([ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)).
6. **A team that owns a machine client cannot be deleted.** The delete answers `409`, as it does for a team that has
   tenants.
7. **A promotion leaves machine clients alone.**

## Options considered

- **Accept tokens from a team's tenants at the management API.** Rejected:
  [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md) accepts only the control-plane
  tenant's tokens.
- **Let a team administrator create any control-plane client.** Rejected: it has no boundary, and an interactive
  control-plane client could phish platform administrators.
- **A separate service-account entity.** Rejected for the reasons of
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md).
- **A machine client in a team's own tenant.** Rejected: its tokens do not authenticate the management API.

## Consequences

- A team provisions its own automation without platform authority.
- The control-plane tenant's settings, quotas and usage govern machine clients.
- The control-plane tenant's administrators can read a team's machine clients but cannot change them
  ([ADR-1467](1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)).

## References

- [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md),
  [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md),
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md),
  [ADR-1421](1421-the-management-console-addresses-a-client-and-a-resource-server-through-their-tenant.md),
  [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md),
  [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md),
  [ADR-1451](1451-every-management-route-authorizes-at-its-node-before-it-reads.md)
- Issue [#588](https://github.com/NCodeGroup/identity/issues/588)

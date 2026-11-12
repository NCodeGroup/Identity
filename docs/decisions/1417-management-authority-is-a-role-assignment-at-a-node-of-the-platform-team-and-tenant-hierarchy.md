# 1417. Management authority is a role assignment at a node of the platform, team and tenant hierarchy

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

The management API needs one answer to "who may do this to that resource". The resources form a hierarchy: the
platform, the teams beneath it (a team can nest beneath a parent team,
[ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md)), the tenants in a team
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)), and
beneath a tenant its clients, resource servers and grants.

Two sources of authority could coexist: a role assignment at a node, inherited down that hierarchy, and a role claim in
the caller's token matched against the tenant a node carries. The management API authenticates every caller against the
control-plane tenant, so the `tid` of every token is that tenant's identifier, and the platform node and every team node
carry the same identifier as their address. A tenant role claim matched by `tid` therefore reaches the platform and every
team as well as the control-plane tenant's own tenant plane: a tenant role would administer the deployment. Such a claim
also appears in no listing of who holds authority, and no endpoint grants or revokes it.

Each of the platform, a team and a tenant needs an administrator role that confers its administration where it is
assigned. A resource's owner manages that one instance but never creates anything beneath it, so it cannot be a tenant's
administrator. The management API's own surface (settings, secrets, clients, resource servers, teams, tenants) needs
scopes on a resource server it can seed into every tenant.

## Decision

**Authority is a persisted role assignment `(principal, role, node)`, inherited down the authority chain, and no role
claim confers management authority except the bootstrap `GlobalAdmin` stamp.** Roles and ownership are one mechanism.

1. **The hierarchy and the roles.** An assignment at a node holds at the node and at everything beneath it: the chain
   runs from the platform node through every team above a tenant to the tenant, and on to a client, resource server or
   grant node (`ResourceNodeTypes`). The built-in roles (`BuiltInRoles`) are `GlobalAdmin`, `TeamAdmin`, `TenantAdmin`
   and `Owner`. The three administrator roles manage their node and everything beneath it and are creation roles
   (`Operations.Create`). `Owner` manages its instance, including its settings, secrets, scopes and owners, and never
   creates.
2. **A tenant administrator.** A `TenantAdmin` assignment at a tenant's node manages the tenant and creates its clients,
   resource servers and other children. It never reaches a team or the platform, even for the control-plane tenant,
   whose identifier those nodes carry only as their address. The administrators of every team above a tenant grant that
   tenant's administrators, and its owners cannot.
3. **The record.** `PersistedRoleAssignment` (`RoleAssignmentEntity` in the Entity Framework store) sits behind
   `IRoleAssignmentStore`. It has `TenantId`, a server-generated `AssignmentId`, `PrincipalId`, `RoleName` (a name, not a
   foreign key), `ResourceType`, `ResourceId` and an interceptor-managed `ConcurrencyToken`, with a lookup index on
   tenant, resource type and resource id. Assignments at team and platform nodes live in the control-plane tenant; one at
   a tenant node is stored under that tenant's identifier.
4. **The handler.** `OwnershipHandler` resolves the caller's `PrincipalId` (`IPrincipalResolver`,
   [ADR-1035](1035-federated-principals-and-identity-resolution.md)) in the control-plane tenant's scope and succeeds
   when one of its assignments of an authorizing role sits at the node or at an ancestor that `IAuthorityChainResolver`
   derives from the resource. No parent pointers are stored. Platform, team and tenant nodes match on type and identifier
   across tenants; any other node matches only an assignment of the node's tenant.
5. **No claim authority.** The one role claim that confers management authority is the bootstrap `GlobalAdmin` stamp
   ([ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)).
   Every other authority is an assignment that the `owners` and `admins` lists show, grant and revoke.
6. **Creators and owners.** A resource's creator is assigned `Owner` in the same unit of work as the insert, unless it
   cannot hold it ([ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md)). A
   resource keeps at least one owner: removing the last answers `409`, and the administrators above it can always
   reassign.
7. **Tenant administrators over HTTP.** `/api/tenants/{tenantId}/admins` (`GET`, `POST`, `DELETE …/{principalId}`) lists,
   grants and revokes `TenantAdmin` as `/api/teams/{teamId}/admins` does `TeamAdmin`. Both need `Operations.Create` at
   the node, so an `Owner` cannot.
8. **The management scopes.** Scopes are `{verb}:{family}` (`ManagementScopes`) on the one reserved system resource
   server `urn:ncode:management`. `ManagementResourceServerSeedHandler` seeds it on the `SeedTenantCommand` fan-out into
   every tenant that lacks it, with `tenant_settings`, `tenant_secrets`, `encryption_keys`, `clients`, `client_secrets`,
   `resource_servers`, `resource_server_secrets`, `client_grants`, `local_accounts`, `read:grants` and `delete:grants`.
   Into the root tenant (`TenantPlane.Root`) only, it also seeds `platform_settings`, `platform_encryption_keys`,
   `teams`, `team_settings` and `tenants`, so a workload tenant never holds them though it shares the audience.

## Options considered

- **Two coarse claim realms.** A claim per realm cannot express a node, an owner or an inheritance chain, and appears in
  no listing. Rejected.
- **Separate role and ownership subsystems or tables.** Two mechanisms answer the same question twice and drift. One
  assignment table serves both. Rejected.
- **An ancestor chain stored on each assignment.** Every move or reparenting rewrites the assignments beneath it, where
  deriving the chain from the resource needs no stored pointers. Rejected.
- **A tenant role matched by the token's `tid`.** Every token carries the control-plane tenant's identifier, so the role
  reaches the platform and every team. Rejected.
- **`Owner` as a tenant's administrator.** An owner never creates a tenant's clients or resource servers, so the
  tenant's administration would still need a second role. Rejected.
- **Two management audiences.** A second audience splits the scopes and the seeding for no gain; scope families the
  root tenant alone holds are seeded into it alone. Rejected.
- **A role assignment at a node, with the management scopes on one resource server.** One grantable, listable authority
  source, inherited down the chain. Chosen.

## Consequences

- Every management authority except the bootstrap stamp is an assignment that the `admins` and `owners` endpoints list,
  grant and revoke.
- `TeamAdmin` and `TenantAdmin` confer the same operations where they are assigned, one tier apart.
- A team's administrators grant a tenant's administrators, and a tenant's owners cannot.
- The control-plane tenant's administrators never hold platform or team authority through the tenant they authenticate
  against.
- A resource always has an owner, and a team or platform administrator can always reassign it.
- Authorization costs one lookup of the caller's assignments over the derived chain, served by the index on tenant,
  resource type and resource id.
- A workload tenant holds the management scopes of its own families only, so a token for it cannot name a platform or
  team family.
- A smell: a new role that needs a rule outside `OwnershipHandler` means the assignment model is missing a node type.

## References

- `src/NCode.Identity.OpenId.Management/BuiltInRoles.cs`, `ManagementScopes.cs`,
  `ResourceServers/ManagementResourceServerSeedHandler.cs`
- [ADR-1035](1035-federated-principals-and-identity-resolution.md): principal resolution
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md):
  management API authentication and the `GlobalAdmin` bootstrap
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the
  platform is the deployment root
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md): a route authorizes
  at the node it names
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md): nested teams
- [ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md): machine clients

# 1417. A tenant administrator is a role assignment at the tenant's node

- **Status:** Accepted (amends [ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md),
  [ADR-1034](1034-persisted-role-assignments-and-ownership.md),
  [ADR-1044](1044-management-api-authentication-and-globaladmin-bootstrap.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md); amended by
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md): the administrators of every team above a tenant grant its
  administrators, and a `TenantAdmin` assignment never reaches a team)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

Management authority has two sources. The first is a role assignment at a node of the resource hierarchy, inherited
down the authority chain from the platform through a team and a tenant to a client or resource server
([ADR-1034](1034-persisted-role-assignments-and-ownership.md),
[ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). The second is a role
claim in the caller's token, matched against the tenant a node carries.

The management API authenticates every caller against the control-plane tenant
([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)), so the `tid` of every token is
that tenant's identifier. The platform node and every team node carry the same identifier as their address. A
`TenantAdmin` role claim matched by `tid` therefore reaches the platform node and every team node as well as the
control-plane tenant's own tenant plane: a tenant role administers the deployment. The claim appears in no listing of
who holds authority, and no assignment grants or revokes it.

The platform, a team and a tenant each have a role that confers their administration where it is assigned. A tenant has
only `Owner`, which manages the tenant but never creates anything beneath it, and no endpoint grants an administrator
that creates a tenant's clients and resource servers.

## Decision

**A tenant administrator is a `TenantAdmin` role assignment at the tenant's node, and a role claim confers no
management authority except the bootstrap `GlobalAdmin`.**

1. **The assignment is the authority.** A `TenantAdmin` assignment at a tenant's node manages the tenant and everything
   beneath it: its settings, secrets, users, clients and resource servers. `TenantAdmin` is a creation role, so the
   administrator also creates the tenant's clients, resource servers and other child resources. The same operations
   hold where `TeamAdmin` is assigned to a team.
2. **`/api/tenants/{tenantId}/admins` lists, grants and revokes it.** `GET`, `POST` and `DELETE` mirror
   `/api/teams/{teamId}/admins`. Granting and revoking is creation authority at the tenant node
   (`Operations.Create`), so an `Owner` neither mints nor revokes a tenant administrator. The assignment is stored under
   the tenant's own identifier, as the tenant's owners are.
3. **No role claim but the bootstrap `GlobalAdmin` confers management authority.** Production tokens carry no role
   claim; the bootstrap client's `GlobalAdmin` stamp
   ([ADR-1044](1044-management-api-authentication-and-globaladmin-bootstrap.md)) is the one exception. Every other
   authority is a persisted assignment that a listing shows.
4. **The control-plane tenant's administrators manage its own tenant plane.** A `TenantAdmin` assignment at the root
   tenant's node manages the root tenant's clients (the management console's included), resource servers, settings,
   secrets and users. It never reaches the platform node or a team node, which carry the control-plane tenant's
   identifier only as their address.
5. **A host-managed client is changed by no management caller.** A client whose credentials and authority come from
   host configuration, the bootstrap administrator client, is named by a registered `IHostManagedClientPolicy`. The
   management API answers `409` to every change to it after authorizing the caller, `GlobalAdmin` included, so
   authority over the root tenant's clients never yields a `GlobalAdmin` token.

## Options considered

- **Record the reach as intended.** A `TenantAdmin` claim for the control-plane tenant administering the platform and
  every team is a tenant role holding deployment authority. Rejected.
- **Restrict the claim handler to tenant-plane resources.** The claim stays a second authority source that no listing
  shows, keyed by a `tid` that only names the control-plane tenant. Rejected.
- **Use `Owner` at the tenant.** An owner manages the tenant but never creates its clients or resource servers, so the
  tenant's administration would still need a second role. Rejected.

## Consequences

- One grantable authority source: every management authority except the bootstrap stamp is an assignment that the
  `admins` and `owners` endpoints list, grant and revoke.
- `TeamAdmin` and `TenantAdmin` confer the same operations where they are assigned, one tier apart.
- A team's administrators grant a tenant's administrators, and a tenant's owners cannot.
- The control-plane tenant's administrators never hold platform or team authority through the tenant they authenticate
  against.
- A client whose credentials come from host configuration cannot be edited, re-keyed or deleted through the management
  API, so no administrator of the root tenant obtains a `GlobalAdmin` token.

## References

- [ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md) — authorization model: scopes, roles and ownership
- [ADR-1034](1034-persisted-role-assignments-and-ownership.md) — persisted role assignments and ownership
- [ADR-1044](1044-management-api-authentication-and-globaladmin-bootstrap.md) — management API authentication and the
  `GlobalAdmin` bootstrap
- [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md) — the management API
  authenticates against the control-plane tenant
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform is the
  deployment root and a team groups a customer's tenants

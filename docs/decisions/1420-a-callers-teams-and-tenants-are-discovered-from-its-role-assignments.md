# 1420. A caller's teams and tenants are discovered from its role assignments

- **Status:** Accepted (amended by [ADR-1484](1484-a-management-routes-caller-is-the-principal-its-management-access-token-authenticates.md))
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) (decision
  7: the console opens on `/home`, which lists the caller's places)

## Context

The management console signs every person in with one control-plane token
([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)) and opens on the platform's
team list. Only platform authority reads that list, so a `TeamAdmin` lands on a refusal and cannot find its own teams.
The token names no team and no tenant, and the caller has no endpoint that says where it holds authority. Automation
that calls the management API with a client's own token has the same need: it must learn the teams it administers
before it can manage them.

## Decision

1. **Authority stays per resource.** Every request is authorized from role assignments at the resource it addresses.
   The token names no team or tenant. One token serves all of a caller's teams, and a grant or revocation applies on
   the caller's next request.
2. **Three endpoints tell the caller where it holds authority.**
   - `GET /api/me` returns a `CallerResource`: the caller's `principalId` (null when no principal resolves),
     `isPlatformAdministrator` (the authorization service grants `Operations.Read` at the platform node) and the
     caller's `assignments` as `RoleAssignmentResource` items.
   - `GET /api/me/teams` returns a `CollectionResource<TeamResource>`: each team at which the caller holds any role
     assignment, ordered by identifier, in one page.
   - `GET /api/me/tenants` returns a `CollectionResource<TenantResource>`: each tenant at which the caller holds any
     role assignment, ordered by identifier, in one page, without the `environment` field.
3. **The console opens on `/home`.** `/home` is the default route. It lists "Open the platform" for a platform
   administrator, the caller's teams and the caller's tenants. When exactly one destination exists, the console opens
   it and replaces the history entry. A Home control in the header returns to `/home`. Once the caller's access is
   known and the caller lacks platform authority, a platform route is replaced by `/home`. A team's back control leads
   to the platform's team list for a platform administrator and Home for any other caller. A tenant's back control
   leads to its team when the caller is a platform administrator or holds a role at that team, and Home otherwise.
   While the caller's access is unknown, the back controls lead where the route's parent leads.
4. **Home is not a management scope.** Each entry on the page opens its own scope
   ([ADR-1064](1064-frontend-presentation-platform.md)), and the page holds no settings, secrets or usage of its own.

## Options considered

- **A team claim or a per-team token.** Rejected: it is stale after a revocation until the token expires, it needs one
  sign-in per team, it grows with the number of teams, and it is a second source of truth beside the role assignments.
  The per-request check stays anyway.
- **An audience or resource indicator per team.** Rejected: it has the same staleness, and the management API is one
  resource server ([ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md)).
- **Probing endpoints in the console.** Rejected: it guesses, and it cannot list the teams a caller holds.

## Consequences

- A grant or revocation shows on the caller's next request.
- One token spans every team and tenant the caller holds a role at.
- `GET /api/me` serves automation as well as the console.
- A caller with no role assignments and no platform authority sees an empty home.

## References

- [ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md),
  [ADR-1034](1034-persisted-role-assignments-and-ownership.md),
  [ADR-1064](1064-frontend-presentation-platform.md),
  [ADR-1174](1174-the-management-consoles-url-is-its-navigation-state.md),
  [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md)
- Issue [#566](https://github.com/NCodeGroup/Identity/issues/566)

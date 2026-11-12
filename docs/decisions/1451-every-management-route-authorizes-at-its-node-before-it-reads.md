# 1451. Every management route authorizes at its node before it reads

- **Status:** Accepted (amends [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md):
  decision 1 covers every management route and decision 4 every entity validator)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

[ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) binds the team and
tenant routes: each authorizes at a node built from its route's values before it reads. The client, resource-server,
scope, client-grant, local-account and platform routes read first. A caller without authority gets `404` for an
identifier that does not exist and `403` for one that does, so these routes disclose which clients, resource servers,
grants, accounts and settings exist, and the store loads a row for a caller who may not see any of them.

[ADR-1034](1034-persisted-role-assignments-and-ownership.md) gives an `Owner` the management of its instance's leaves: its
settings, secrets and scopes. A leaf write that authorizes at the tenant node from the row refuses an owner and needs
the read to find the row's tenant.

A request body can name a node too. A client names the resource server it belongs to, and an initial access token names
the resource server it is scoped to. That node comes from the body, and the route authorizes it before it reads the
resource server.

## Decision

**Every management route authorizes the caller at a node built from its route values, and from a node its request body
names, before it reads.**

1. **A client's and a resource server's own routes and leaves authorize at that resource's node from the route.** Its
   settings, secrets and scopes authorize at `ResourceNode.For(tenantId, type, id)`: Update or Delete for a write,
   Create for creating, importing or rotating a secret (rotating also needs Update), and Create for a settings write
   that issues a secret. An `Owner` of the instance holds these, as ADR-1034 grants.
2. **Creating a client or a resource server is Create at the tenant node.** A client create that names a resource
   server also authorizes Update at that resource server's node, built from the request body, before it reads the
   resource server. An initial access token scoped to a resource server does the same.
3. **Client-grant routes authorize at the tenant scope.** The route uses `TenantScopeResource`, as the grant's GET
   does. A grant is the tenant's policy between two of its resources, so an owner of the client or of the resource
   server alone does not hold it.
4. **Local-account routes authorize at the tenant scope.** There is no per-account owner model. Every account write,
   including enable, disable, password, claims and metadata, authorizes through
   `ILocalAccountValidator.AuthorizeUpdateAsync`.
5. **Platform settings and encryption-key routes authorize at the platform node** before they check that the platform
   exists.
6. **A validator splits authority from row checks.** `IClientValidator`, `IResourceServerValidator`,
   `IClientGrantValidator` and `ILocalAccountValidator` expose `AuthorizeCreateAsync`, `AuthorizeUpdateAsync` and
   `AuthorizeDeleteAsync`, which take the caller and the route values and run before any read, as `ITenantValidator`
   and `ITeamValidator` do. Their `Validate*Async` methods take the row without the caller. A `Validate*Async` with no
   row rule returns `null` and remains the replacement hook ADR-1015 describes. Settings, secret and scope writes
   authorize in the handler through `GetAuthorizationFailureAsync`, as the tenant handler does.
7. **A check that needs the row stays after the read.** The If-Match precondition, uniqueness, a client's custody
   ([ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md)), a settings write's limit
   ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)), `IsSystem` and
   dependents judge the row, so they run in `Validate*Async` or in the handler after the read.

## Options considered

- **Authorize leaves at the tenant node.** A leaf write authorizes against the tenant from the row. It refuses an
  `Owner` of the instance and needs the read. Rejected.
- **Authorize client grants at the client node or at both nodes.** An owner of a client or a resource server would
  grant itself access to the tenant's APIs. Rejected.
- **Authorize at the node the route and body name, before the read.** The decision about a caller is the same whether or
  not the identifier exists, and an owner holds its instance's leaves. Chosen.

## Consequences

- No management answer to a stranger depends on a store, and no row is read for a caller without authority.
- An owner updates and deletes its client's or resource server's settings, scopes and secrets.
- Creating a secret needs creation authority at the instance's node.
- Every entity validator has the same two halves: route-only authorization before the read and row-only validation after
  it.

## References

- [ADR-1015](1015-management-core-validation-via-validators.md) — management core validation via replaceable
  validators
- [ADR-1034](1034-persisted-role-assignments-and-ownership.md) — persisted resource-scoped role assignments and
  ownership
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — a limit is set by
  the authority above the scope whose document carries it
- [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md) — a client acting for
  itself is a principal that holds role assignments
- [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) — a management
  route authorizes the caller at the node it names before it reads

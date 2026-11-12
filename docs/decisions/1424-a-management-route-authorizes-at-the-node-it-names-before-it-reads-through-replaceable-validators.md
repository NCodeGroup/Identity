# 1424. A management route authorizes at the node it names before it reads, through replaceable validators

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md), decision 6, for the management API

## Context

A management route that reads before it authorizes loads a row for a caller who holds no authority over it. The answer
then depends on what the store holds: a stranger gets `404` for an identifier that does not exist and `403` for one
that does, so the route discloses which teams, tenants, clients, resource servers, grants, accounts, settings, secrets,
keys, owners and administrators exist, and the store does the work of finding them for a caller who may not see any of
them.

Authority is decided at nodes
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md),
[ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)),
and a node is built from the route's values alone: the platform, a team or a tenant needs no row. A tenant node of an
unknown tenant has no team ancestor, so a role at a team never reaches it. The decision about a caller is therefore the
same whether or not the identifier exists.

[Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 6
answers a caller without authority `404` so that nobody probes existence. Its purpose holds when authority is decided
before any read, and its answer hides the one fact operators and provisioning tools need: whether they lack authority
or the resource is gone. A tool that reads `404` for a resource it may not see recreates it.

An `Owner` of an instance manages that instance's leaves: its settings, secrets and scopes. A leaf write that
authorizes at the tenant node from the row refuses an owner and needs the read to find the row's tenant. A request
body can name a node too: a client names the resource server it belongs to, and an initial access token names the
resource server it is scoped to.

Validation has two kinds of check. Authority depends on the caller and the route's values and must run before any read.
Row rules depend on the stored row and the request's store and run after it. An integrator needs to replace either
without editing the library, and a precondition must fail on a fact that was queried, not on an exception that was
caught.

## Decision

**Every management route authorizes the caller at a node built from its route values, and from a node its request body
names, before it opens a store, through one replaceable validator per entity.**

1. **The node comes from the route.** A caller without authority gets `401` when anonymous and `403` otherwise, for any
   identifier, existing or not. Only an authorized caller can get `404`. A team administrator gets `403` for a tenant
   outside its team, and a tenant administrator for any other tenant, whether or not it exists.
2. **`403`, not `404`.** Authority is decided at nodes regardless of existence, so `403` discloses nothing, and an
   operator or a provisioning tool tells "no authority" from "gone". The purpose of project-template ADR-0129 holds:
   nobody probes existence, and no row is loaded for a stranger. This record holds the management API's
   `401`/`403`/`404` rule;
   [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md) cites it
   for tenant scoping.
3. **A GET route reads through a read function.** `ManagementApiEndpointHandler.ProcessGetAsync` takes the node and a
   `readAsync` function and reads only after authorizing. `GetAuthorizationFailureAsync(httpContext, resource,
   requirement, cancellationToken)` returns the `401` or `403` result, or `null` when the caller holds the authority,
   so a route that is not a plain read, and a route of a capability package, authorizes with the same call. Settings,
   secret and scope writes authorize through it.
4. **Resource routes and their leaves authorize at the resource's node.** A client's and a resource server's own
   routes, settings, secrets and scopes authorize at `ResourceNode.For(tenantId, type, id)`: Update or Delete for a
   write, Create for creating, importing or rotating a secret (rotating also needs Update) and for a settings write
   that issues a secret. An `Owner` of the instance holds these.
5. **Creating a client or a resource server is Create at the tenant node.** A client create that names a resource
   server, and an initial access token scoped to one, also authorize Update at that resource server's node, built from
   the body, before the resource server is read.
6. **Other routes authorize at the node that fits them.** Client-grant and local-account routes authorize at
   `TenantScopeResource`: a grant is the tenant's policy between two of its resources, and there is no per-account
   owner model. Every local-account write other than create and delete, including enable, disable, password, claims
   and metadata, is `Update` at the tenant scope, through `ILocalAccountValidator.AuthorizeUpdateAsync`. Platform
   settings and encryption-key routes authorize at the platform node before checking that the
   thing exists. Team routes authorize at the team node first; disabling, restoring or deleting a team then authorizes
   at the node above it against the stored row
   ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md)).
7. **One replaceable validator per entity.** `ITeamValidator`, `ITenantValidator`, `IClientValidator`,
   `IResourceServerValidator`, `IClientGrantValidator` and `ILocalAccountValidator` are registered with
   `TryAddSingleton`, so an integrator registers its own first, over an internal default on the public
   `DefaultResourceValidator` base, which maps an authorization failure to a `401` or `403` `ManagementError`.
8. **A validator splits authority from row checks.** `AuthorizeCreateAsync`, `AuthorizeUpdateAsync` and
   `AuthorizeDeleteAsync` take the caller and the route values and run before the read. `Validate*Async` takes the row
   and the request's open `IStoreManager`, one unit of work, never the caller, and runs after the read. All return
   `ManagementError?`.
9. **Row checks run in order and return the first error.** `Validate*Async` returns the first `ManagementError`, whose
   `StatusCode` and fixed `Detail` never carry an exception message. A validator with no row rule returns `null` and
   remains the replacement hook. The row checks are `If-Match`, uniqueness, parent exists, no dependents, `IsSystem`,
   machine-client custody and a settings write's limit
   ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)).
   An update validates the proposed state, the patch applied to a throwaway model.
10. **A precondition fails on a queried fact.** A precondition fails only on a fact a validator or handler queried,
    never on a caught exception, so stores expose facts such as existence and dependents as query methods. A lost race
    is answered as
    [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md) decides.

## Options considered

- **Read, then authorize.** The route loads the row and decides on it. It discloses existence through the difference
  between `404` and `403` and loads a row for a caller with no authority. Rejected.
- **`404` for every refusal.** Every refusal looks like an absent resource. It hides "no authority" from operators and
  provisioning tools, which then recreate what they may not see. Rejected.
- **A mediator fan-out.** A command per operation and a handler per check, each with its own store manager, ordered by
  priority. It multiplies units of work for one request and spreads one entity's rules over many classes. Rejected.
- **Inline validation per endpoint.** Each endpoint carries its own checks, so an integrator cannot replace them and
  the rules drift apart. Rejected.
- **Inferring a failure from a caught `InvalidOperationException`.** The failure then depends on an exception's
  message and on what else threw it. Rejected.
- **Authorizing leaves at the tenant node.** It refuses the owners of the instance that holds the leaf. Rejected.
- **Authorizing client grants at the client node.** An owner of a client would grant itself the tenant's APIs.
  Rejected.
- **Authorize first, through replaceable validators.** The caller's authority is decided at the node before any read,
  so the answer to a stranger never depends on the store, and each entity's rules can be replaced as a unit. Chosen.

## Consequences

- A refused caller's answer never depends on a store and costs no read.
- A caller without authority learns nothing about which identifiers exist, and an authorized caller still gets `404`
  for one that does not.
- A capability package's routes take the same shape through `GetAuthorizationFailureAsync` and `ProcessGetAsync`.
- A validator's authority checks run before its row checks, and a check that needs the row runs after the read.
- An integrator replaces an entity's rules by registering its own validator before the library's default.
- Every error a validator returns has a fixed detail, so no exception message reaches a caller.

## References

- Code: `ManagementApiEndpointHandler`, `DefaultResourceValidator`, `ITeamValidator`, `ITenantValidator`,
  `IClientValidator`, `IResourceServerValidator`, `IClientGrantValidator`, `ILocalAccountValidator`,
  `ResourceNode.For`, `TenantScopeResource`
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — the
  platform is the deployment root and a team groups a customer's tenants
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) —
  management authority is a role assignment at a node
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — a
  limit is set by the authority above its scope
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md) — a team nests
  beneath a parent team
- [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md) — the
  management API's answer to a lost race
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md) — tenant
  selection and scoping
- [Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) —
  browser-facing requests are safe by construction

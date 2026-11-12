# 1424. A management route authorizes the caller at the node it names before it reads

- **Status:** Accepted (amends [ADR-1015](1015-management-core-validation-via-validators.md); amended by
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md): the node above a child team is its parent, so a team's
  deletion, like its disable, is authorized at the team's node before the read and at the node above it against the
  stored row; amended by [ADR-1451](1451-every-management-route-authorizes-at-its-node-before-it-reads.md): every
  management route, and every entity validator, takes this shape)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md),
  decision 6 only, for the management API

## Context

A management route that reads before it authorizes loads a row for a caller who holds no authority over it. The answer
then depends on what the store holds: a stranger gets `404` for an identifier that does not exist and `403` for one
that does, so the route discloses which teams, tenants, settings, secrets, keys, owners and administrators exist, and
the store does the work of finding them for a caller who may not see any of them.

Authority is decided at nodes
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
[ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md)), and a node is built from the
route's values alone: the platform, a team or a tenant needs no row. A tenant node of an unknown tenant has no team
ancestor, so a role at a team never reaches it. The decision about a caller is therefore the same whether or not the
identifier exists.

[Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 6
answers a caller without authority `404` so that nobody probes existence. Its purpose holds when authority is decided
before any read, and its answer hides the one fact operators and provisioning tools need: whether they lack authority
or the resource is gone. A tool that reads `404` for a resource it may not see recreates it.

## Decision

**A team or tenant route authorizes the caller at the node its route names before it reads what the route names.**

1. **The node comes from the route's values.** Every team and tenant route authorizes at the platform, a team node or
   a tenant node built from the identifiers in its path, before it opens a store to read what the path names. A caller
   without authority gets `401` when anonymous and `403` otherwise, for any identifier, existing or not. Only an
   authorized caller can get `404`. A team administrator gets `403` for a tenant outside its team, and a tenant
   administrator for any other tenant, whether or not it exists. The owner and administrator routes of teams and
   tenants, including a tenant's administrators, already have this shape.
2. **`403`, not `404`.** This record supersedes decision 6 of project-template ADR-0129 for the management API.
   Authority is decided at nodes regardless of existence, so `403` reveals nothing, and an operator or provisioning tool
   tells "no authority" from "gone". The purpose of ADR-0129 holds: nobody probes existence, and no row is loaded for a
   stranger.
3. **A GET route reads through a read function.** `ProcessGetAsync` takes the node and a `readAsync` function and
   reads only after authorizing. `GetAuthorizationFailureAsync(httpContext, resource, requirement, cancellationToken)`
   returns the `401` or `403` result, or `null` when the caller holds the authority, so a route that is not a plain read
   authorizes with the same call.
4. **A validator splits authority from row checks.** `ITeamValidator` and `ITenantValidator` expose
   `AuthorizeCreateAsync`, `AuthorizeUpdateAsync` and `AuthorizeDeleteAsync`, which take only route values and run
   before any read. Their `Validate*Async` methods judge the row and never authorize the route's node again. A check
   that depends on the row stays in `Validate*Async`, after the read: disabling or restoring a team is platform
   authority judged against the stored `IsDisabled`, and a settings write's limit check needs the stored document
   ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)).

## Options considered

- **Read, then authorize.** The route loads the row and decides on it. It discloses existence through the difference
  between `404` and `403` and loads a row for a caller with no authority. Rejected.
- **`404` for every refusal.** Every refusal looks like an absent resource. It hides "no authority" from operators and
  provisioning tools, which then recreate what they may not see. Rejected.
- **Authorize first.** The caller's authority is decided at the node before any read, so the answer to a stranger
  never depends on the store. Chosen.

## Consequences

- A refused caller's answer never depends on a store and costs no read.
- A caller without authority learns nothing about which identifiers exist, and an authorized caller still gets `404`
  for one that does not.
- A capability package's routes take the same shape through `GetAuthorizationFailureAsync` and `ProcessGetAsync`.
- A validator's authority checks run before its row checks, and a check that needs the row runs after the read.

## References

- [ADR-1015](1015-management-core-validation-via-validators.md) — management core validation via replaceable
  validators
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform is the
  deployment root and a team groups a customer's tenants
- [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) — a tenant administrator is a
  role assignment at the tenant's node
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — a limit is set by
  the authority above the scope whose document carries it
- [Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) —
  browser-facing requests are safe by construction

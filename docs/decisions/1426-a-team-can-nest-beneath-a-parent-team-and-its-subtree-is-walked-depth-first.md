# 1426. A team can nest beneath a parent team, and its subtree is walked depth first

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A team groups a customer's tenants, and its settings, administrators, quotas and usage are the customer's
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)). A
customer with several products, each needing a sandbox, a testing and a production tenant, needs two levels: the
customer's policy, administrators, quota and usage at one team, and each product's at a team beneath it.

One flat team per product puts the customer's policy in every product team, leaves no customer-level administrators who
create product teams, and gives quotas and usage no customer scope: the reasons teams exist at all.

Nesting makes a team's effects reach a subtree. Reconciliation of a team
([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md))
and the coherence check of a team settings write reach every team beneath the team they start from. A team can hold any
number of child teams, so a subtree read as one list is unbounded in memory and in result size. Teams nest at most
`TeamPaths.MaxDepth` (8) deep, so a subtree has a bounded number of levels and an unbounded number of teams on each of
them.

## Decision

**A team can sit beneath a parent team: settings and limits cascade through every ancestor, authority flows down the
chain, disabling a team suspends everything beneath it, quotas and usage roll up, and reconciliation, cache eviction
and missed-announcement revalidation reach every descendant. A team's subtree is walked depth first, one page of child
teams per level, and `ITeamStore` has no read of a whole subtree.**

### The tree

1. **A team's parent is fixed when it is created; a team never moves**, as a tenant never moves. The tree has no cycles
   by construction, because a parent must already exist, and no move can silently change the settings, authority and
   quotas of a subtree. A team holds tenants and child teams side by side.
2. **Each team row stores its immutable path**: every identifier from the top-level team down to itself, each followed
   by `/`, after a leading `/` (`/acme/acme-web/`), built once at insert from the parent's path. `TeamPaths`, in
   `NCode.Identity.OpenId.Persistence.Abstractions`, formats and parses the path. A subtree is a prefix match, so no
   closure table is needed. `PersistedTeam` exposes `ParentTeamId`, set at creation, and `AncestorTeamIds`, top-level
   first.
3. **Depth is bounded.** A top-level team is at depth 1. `TeamPaths.MaxDepth = 8` is the store's hard bound. The
   platform-scoped setting `max_team_depth` (an integer between 1 and 8, unset meaning 8, not discoverable, not a
   limit) bounds the depth a team is created at, and a creation past it answers `409`.

### Settings and limits

4. **Settings cascade through every ancestor**: platform, each ancestor team from the top down, the team, the tenant,
   the client. Every level is the one `team` scope; there is no setting scope per level.
   `IEffectiveSettingsResolver.ResolveTeamAsync` folds the lineage, and
   `IEffectiveSettingsResolver.ResolveTeamParentAsync` returns what a team's document merges onto: its parent team's
   committed settings, or the platform's for a top-level team. A materialized `OpenIdTeam` layers its document over its
   parent's settings provider and exposes `Parent` and `Path`.
5. **Limits follow the chain**
   ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)). The
   node above a child team's settings document is its parent team's node, and every limit merges with a meet, so a
   parent's limit caps every team and tenant beneath it. `max_teams` (a team-scoped limit, an integer of at least 0, not
   discoverable) bounds the teams beneath a team at every level. `max_tenants` bounds the tenants of a team and of every
   team beneath it. A creation is refused when any team on the lineage is full; the check runs before the insert, so
   two racing creations may overshoot by one. Top-level team creation is platform authority and is not bounded by
   `max_teams`.

### Authority and lifecycle

6. **Authority flows down the chain.** A role at a team reaches every team and tenant beneath it. Creating a child team
   is `Operations.Create` at the parent team, and listing its child teams is `Operations.List` at the team. A team's
   lifecycle (create, disable, restore, delete) is authority at the node above it. `TenantAdmin`
   ([ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md)) never reaches a team, because
   the chain of a team node holds team nodes and the platform, never a tenant node. Every team route authorizes at the
   node it names before it reads
   ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)), and then
   authorizes a disable, a restore or a delete at the node above against the stored parent.
7. **A disabled team suspends everything beneath it.** `OpenIdTeam.IsDisabled` is true when the team or any ancestor is
   disabled, so every tenant beneath answers `404`, and the ended-session sweep and the key rollover skip them. A team
   with tenants or child teams cannot be deleted (`409`).
8. **A promotion stays between two direct tenants of the team its route names**
   ([ADR-1425](1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md)).

### Usage, reconciliation and caches

9. **Usage rolls up.** Usage rows store the tenant's team path
   ([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)), so a deleted
   tenant's or team's usage still rolls up. `ITenantUsageStore.ListByTeamAsync` is a path prefix match, and
   `TeamUsageEntry` carries the team identifier.
10. **Reconciliation, cache eviction and revalidation reach every descendant**
    ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md),
    [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md)).
    The team cache indexes each team under every ancestor and the tenant cache indexes each tenant under its team and
    every ancestor. A cached team or tenant reports the revision of every team it was materialized over, so the
    missed-announcement handler finds a stale ancestor and evicts the subtree beneath it. A team settings write checks
    client coherence for every tenant beneath.

### Walking a subtree

11. **One walker visits every team beneath a team.** `ITeamDescendantWalker.WalkDescendantsAsync(teamId, visitAsync,
    cancellationToken)` (default `DefaultTeamDescendantWalker`, replaceable) visits every team beneath once, each parent
    before its children (pre-order depth first). It reads `ITeamStore.GetChildPageAsync` 100 teams at a time, each page
    in a unit of work of its own, and holds at most one page per level. `visitAsync` returns whether to descend beneath
    the visited team.
12. **The store has no read of a whole subtree.** `ITeamStore` serves the tree through `GetChildPageAsync`,
    `GetLineageAsync` and `CountDescendantsAsync`; `GetChildPageAsync` is one untracked query.
13. **Callers keep little state.** `TeamSettingsReconciliationService` remembers only the requested teams it reconciled
    and declines a requested team's subtree that another walk reaches, so each team is reconciled once per pass.
    `DefaultTeamClientCoherenceChecker` holds only the settings on the path to the visited team and declines the
    subtree of a team whose document cannot be honored.

### The API

14. `POST` and `GET /api/teams/{teamId}/teams` create and list child teams, `TeamResource.ParentTeamId` names a team's
    parent, and `GET /api/teams` lists every level. The management console's team scope has a Teams surface
    (`/teams/{teamId}/teams`), and its usage table names each entry's team.

## Options considered

- **One flat team per product.** The customer's policy, administrators, quota and usage have no scope of their own, so
  each product team repeats them. Rejected.
- **Re-parentable teams.** A move silently changes a subtree's settings, authority and quotas and needs cycle checks
  serialized across replicas. Rejected.
- **A closure table.** The immutable path answers the same queries in one column, and usage rows carry it. Rejected.
- **A setting scope per level.** One recursive `team` scope keeps the settings vocabulary open. Rejected.
- **Unbounded depth.** Every walk of the lineage is bounded by `TeamPaths.MaxDepth`. Rejected.
- **One list of the whole subtree.** Simple, but its memory and result size grow with the number of teams beneath the
  team. Rejected.
- **Breadth-first walking.** A level's frontier is unbounded, and the coherence check needs each team's parent
  settings, which a level-wide pass would hold for the whole level. Rejected.
- **A keyset over `Path`.** The order of a path column depends on the database collation, so a page boundary could skip
  or repeat a team. Rejected.
- **A tree of teams with immutable parents, bounded depth, a stored path and a paged depth-first walk.** Chosen.

## Consequences

- A customer's policy, administrators, quota and usage live at one team, and each product's at a team beneath it.
- A subtree is read with one prefix match on the stored path, and the path is built once, so no query follows parent
  links.
- A change to a team's settings, a disable or an eviction fans out to every team and tenant beneath it, bounded by the
  depth limit.
- A walk costs one child-page read per team walked, and its memory is bounded by the depth times the page size.
- A custom `ITeamStore` serves the tree (`GetChildPageAsync`, `GetLineageAsync`, `CountDescendantsAsync`), a custom
  `ITenantStore` counts the tenants of a team's subtree, a custom `ITenantUsageStore` lists by team path prefix and
  `ITenantUsageRecorder` records under the team path, a custom `IEffectiveSettingsResolver` resolves
  `ResolveTeamParentAsync`, a custom `OpenIdTeam` exposes `Parent` and `Path`, and a custom `IOpenIdTeamCache` evicts a
  team's subtree. A custom walker can be registered in place of `DefaultTeamDescendantWalker`.

## References

- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) — tenant usage is a
  daily ledger that replicas add to
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — the
  platform is the deployment root and a team groups a customer's tenants
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) —
  settings are reconciled per team
- [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md) — a
  replica that may have missed a team announcement evicts only the teams whose row changed
- [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) — a tenant administrator is a
  role assignment at the tenant's node
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — a
  limit is set by the authority above the scope whose document carries it
- [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) — a management
  route authorizes the caller at the node it names before it reads
- [ADR-1425](1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md) — a team
  promotes one tenant's configuration to another and environment-bound values stay

# 1426. A team can nest beneath a parent team

- **Status:** Accepted (amends
  [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md),
  [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md),
  [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md),
  [ADR-1418](1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md) and
  [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md); amended by
  [ADR-1455](1455-a-teams-subtree-is-walked-depth-first-one-page-of-child-teams-per-level.md): a subtree is walked one
  page of child teams per level, and `ITeamStore` has no read of a whole subtree)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A team groups a customer's tenants, and its settings, administrators, quotas and usage are the customer's
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). A customer with
several products, each needing a sandbox, a testing and a production tenant, needs two levels: the customer's policy,
administrators, quota and usage at one team, and each product's at a team beneath it (issue
[#567](https://github.com/NCodeGroup/identity/issues/567)).

One flat team per product puts the customer's policy in every product team, leaves no customer-level administrators who
create product teams, and gives quotas and usage no customer scope: the reasons teams exist at all.

## Decision

**A team can sit beneath a parent team: settings and limits cascade through every ancestor, authority flows down the
chain, disabling a team suspends everything beneath it, quotas and usage roll up, and reconciliation, cache eviction
and missed-announcement revalidation reach every descendant.**

1. **A team's parent is fixed when it is created; a team never moves**, as a tenant never moves. The tree has no cycles
   by construction, because a parent must already exist, and no move can silently change the settings, authority and
   quotas of a subtree. A team holds tenants and child teams side by side, so a customer's existing team becomes the
   parent of its product teams without moving a tenant.
2. **The platform's own team (`TenantResolution:Root:TeamId`) is top-level and holds no teams**, so a team
   administrator of the platform team never gains authority over customer teams.
3. **Each team row stores its path**: the team identifiers from its top-level team down to itself, each followed by
   `/`, after a leading `/` (`/acme/acme-web/`), built once at insert from the parent's path and never changed. A team
   identifier is 1 to 63 lowercase letters, digits or hyphens, so it never contains `/`. `TeamPaths` formats and parses
   the path, `TeamPaths.MaxDepth = 8` is the hard bound, and a subtree is a prefix match, so no closure table is
   needed. `PersistedTeam` exposes `ParentTeamId`, set at creation, and `AncestorTeamIds`, top-level first.
4. **The depth of a team is bounded.** A top-level team is at depth 1. The platform-scoped setting `max_team_depth`
   (an integer between 1 and 8, unset meaning 8, not discoverable, not a limit) bounds the depth a team is created at,
   and a creation past it answers `409`. The store refuses a depth past 8.
5. **Settings cascade through every ancestor**: platform, each ancestor team from the top down, the team, the tenant,
   the client. Every level is the one `team` scope; there is no setting scope per level.
   `IEffectiveSettingsResolver.ResolveTeamAsync` folds the lineage, and
   `IEffectiveSettingsResolver.ResolveTeamParentAsync` resolves what a team's document merges onto: its parent team's
   committed settings, or the platform's for a top-level team. A materialized `OpenIdTeam` layers its document over
   its parent team's settings provider, or the platform's for a top-level team, and exposes `Parent` and `Path`.
6. **A limit follows the chain**
   ([ADR-1418](1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md)). The node above a
   team's settings document is its parent team's node, or the platform for a top-level team, so a child team's limits
   are written by its parent's administrators. Every limit merges with a meet, so a parent's limit caps every team and
   tenant beneath it. `max_teams` is a limit. A child team's creation needs no separate limit check: `Create` at the
   parent is held only by roles that also hold `Update` there.
7. **Authority flows down the chain.** A role at a team reaches every team and tenant beneath it. Creating a child team
   is `Operations.Create` at the parent team node, and listing a team's child teams is `Operations.List` at the team
   node. A team's lifecycle (create, disable, restore, delete) is authority at the node above it: the platform for a
   top-level team, the parent team for a child team. `TenantAdmin`
   ([ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md)) stays confined to its tenant:
   the chain of a team node holds team nodes and the platform, never a tenant node, so a tenant administrator never
   reaches a team, and the administrators of every team above a tenant grant its administrators. Every team route
   authorizes at the node it names before it reads
   ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)): creating a
   child team authorizes `Operations.Create` at the parent team its route names; updating or deleting a team
   authorizes at the team's own node, which every role above it reaches, and then, against the stored parent,
   authorizes a disable, a restore or a deletion at the node above it, so a caller without authority at the team gets
   `403` for any identifier and nothing is read. Listing a team's child teams and reading its usage authorize at the
   team node first.
8. **A disabled team suspends everything beneath it.** `OpenIdTeam.IsDisabled` is true when the team or any team above
   it is disabled, so every tenant beneath a disabled team answers `404`, and the ended-session sweep and the key
   rollover skip them. A team with tenants or child teams cannot be deleted (`409`).
9. **Quotas roll up.** `max_tenants` at a team bounds the tenants of the team and of every team beneath it, and a
   tenant creation is refused when any team on its lineage is full. The team-scoped limit `max_teams` (an integer of at
   least 0, merged with a meet, not discoverable) bounds the teams beneath a team at every level, and a child-team
   creation is refused when any team on the parent's lineage is full. Top-level team creation is platform authority and
   is not bounded by `max_teams`. The checks run before the insert, and two racing creations may overshoot by one.
10. **Usage rolls up.** Every usage row stores the tenant's team path
    ([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)), so a deleted tenant's or deleted team's
    usage still rolls up to every team above it. `ITenantUsageStore.ListByTeamAsync` is a prefix match on the path, and
    `TeamUsageEntry` carries the team identifier, the path's last segment.
11. **Reconciliation reaches every descendant.** A triggered team is reconciled with every team beneath it, each under
    its own lease and fingerprint ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)), and each team once per
    drained request even when an ancestor and a descendant are both pending. A descendant that does not settle is
    retried with its own subtree, and a failed descendant read leaves the triggered team unsettled. The platform pass
    reconciles every team once. A team settings write checks client coherence for every tenant of the team and of
    every team beneath it.
12. **Cache eviction and revalidation reach every descendant**
    ([ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md)).
    The team cache indexes each cached team under every team above it, and the tenant cache indexes each tenant under
    its team and every team above it, so evicting a team evicts every cached team and tenant beneath it, on every
    replica, through the `team-changed` broadcast. A cached team, and a cached tenant, report the revision of every
    team it was materialized over, its own team and then each parent, so the missed-announcement handler finds a stale
    ancestor that is no longer cached itself and evicts the subtree beneath it.
13. **The management API addresses the tree.** `POST /api/teams/{teamId}/teams` creates a child team and
    `GET /api/teams/{teamId}/teams` lists a team's child teams. `TeamResource.ParentTeamId` names a team's parent, and
    `GET /api/teams` lists every team at every level. A team scope of the console has a Teams surface
    (`/teams/{teamId}/teams`) that lists, creates, edits and deletes child teams, the platform team list shows each
    team's parent, a team scope's exit leads to its parent team when it has one, and the usage table names each
    entry's team.
14. **A promotion stays between two tenants of one team.** A promotion
    ([ADR-1425](1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md))
    copies between the direct tenants of the team its route names, the environments of one product. A tenant of a team
    beneath it resolves under that team's settings and limits, and the node above a promoted tenant document is the
    tenant's own team. A parent team's administrators reach a child team's tenants, so they promote within a child team
    through that team's route.

## Options considered

- **One flat team per product.** The customer's policy, administrators, quota and usage have no scope of their own, so
  each product team repeats them. Rejected.
- **Re-parentable teams.** A move silently changes a subtree's settings, authority and quotas and needs cycle checks
  serialized across replicas. A team that holds tenants and child teams side by side covers a customer that grows.
  Rejected.
- **A closure table.** The immutable path answers the same queries in one column, and usage rows carry it. Rejected.
- **A setting scope per level.** One recursive `team` scope keeps the settings vocabulary open. Rejected.
- **Unbounded depth.** Every walk of the lineage is bounded by `TeamPaths.MaxDepth`. Rejected.

## Consequences

- A customer's policy, administrators, quota and usage live at one team, and each product's at a team beneath it.
- A subtree is read with one prefix match on the stored path, and the path is built once, so no query follows parent
  links.
- A change to a team's settings, a disable or an eviction fans out to every team and tenant beneath it, bounded by the
  depth limit.
- A custom `ITeamStore` serves the tree (`GetChildPageAsync`, `GetLineageAsync`, `GetDescendantsAsync`,
  `CountDescendantsAsync`), a custom `ITenantStore` counts the tenants of a team's subtree, a custom
  `ITenantUsageStore` lists by team path prefix and `ITenantUsageRecorder` records under the team path, a custom
  `IEffectiveSettingsResolver` resolves `ResolveTeamParentAsync`, a custom `OpenIdTeam` exposes `Parent` and `Path`,
  and a custom `IOpenIdTeamCache` evicts a team's subtree.

## References

- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — tenant usage is a daily ledger that
  replicas add to
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform is the
  deployment root and a team groups a customer's tenants
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) — settings are reconciled per team
- [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md) — a
  replica that may have missed a team announcement evicts only the teams whose row changed
- [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) — a tenant administrator is a
  role assignment at the tenant's node
- [ADR-1418](1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md) — a limit is set by
  the authority above the scope whose document carries it
- [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) — a management
  route authorizes the caller at the node it names before it reads
- [ADR-1425](1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md) — a team
  promotes one tenant's configuration to another and environment-bound values stay

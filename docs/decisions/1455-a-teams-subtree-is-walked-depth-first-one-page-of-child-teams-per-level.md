# 1455. A team's subtree is walked depth first, one page of child teams per level

- **Status:** Accepted (amends [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md))
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

The reconciliation of a team ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)) and the coherence check of a team
settings write reach every team beneath the team they start from
([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md)). A subtree read in one list is unbounded in memory and in
result size, because a team can hold any number of child teams.

Teams nest at most `TeamPaths.MaxDepth` (8) deep, so a subtree has a bounded number of levels and an unbounded number of
teams on each of them.

## Decision

**A team's subtree is walked depth first, one page of child teams per level, and `ITeamStore` has no read of a whole
subtree.**

1. **One walker visits every team beneath a team.** `ITeamDescendantWalker.WalkDescendantsAsync(teamId, visitAsync,
   cancellationToken)` visits every team beneath a team once, each parent before its children (depth first,
   pre-order). It reads child teams with `ITeamStore.GetChildPageAsync`, 100 at a time, each page in a unit of work of
   its own. `visitAsync` returns whether to walk beneath the visited team. The walk holds at most one page per level, so
   at most `TeamPaths.MaxDepth` pages.
2. **The store has no read of a whole subtree.** `ITeamStore` declares no member that returns the teams of a subtree in
   one list, and `GetChildPageAsync` is one untracked query.
3. **The reconciliation remembers only what it reconciled.** `TeamSettingsReconciliationService` remembers only the
   requested teams it reconciled in a pass and declines a requested team's subtree when another's walk reaches it, so
   each team is reconciled once per pass.
4. **The coherence check holds the path.** `DefaultTeamClientCoherenceChecker` holds only the settings of the teams on
   the path to the visited team and declines the subtree of a team whose document cannot be honored.

## Options considered

- **One list of the whole subtree.** Simple, but its memory and result size grow with the number of teams beneath the
  team. Rejected.
- **Breadth first, one level at a time.** A level's frontier is unbounded, and the coherence check needs each team's
  parent settings, which a level-wide pass would hold for the whole level. Rejected.
- **One keyset over `Path`.** The order of a path column depends on the database collation, so a page boundary could
  skip or repeat a team. Rejected.

## Consequences

- A walk costs one child-page read per team walked, and its memory is bounded by the depth times the page size.
- A custom `ITeamStore` serves no subtree read.
- A custom walker can be registered in place of `DefaultTeamDescendantWalker`.

## References

- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) — settings are reconciled per team
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md) — a team can nest beneath a parent team

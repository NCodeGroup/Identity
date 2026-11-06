# 1416. A replica that may have missed a team announcement evicts only the teams whose row changed

- **Status:** Accepted (amends [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md),
  [ADR-1412](1412-settings-are-reconciled-per-team.md); amended by
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md): a cached team or tenant reports the revision of every
  team above it, and evicting a team evicts every team beneath it)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Refines:** [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md)

## Context

A replica serves teams and tenants from `IOpenIdTeamCache` and `IOpenIdTenantCache`. The `team-changed` channel
announces a team change to every replica
([ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md),
[ADR-1412](1412-settings-are-reconciled-per-team.md)), and a replica whose subscription was interrupted may have
missed a message, which
[ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) has its handler learn of.

Clearing every cached team and tenant whenever the channel may have missed a message re-materializes everything the
replica serves, with its store reads, settings providers and key collections, for changes that mostly did not happen.

Every committed change to a team modifies the team row, and the interceptor regenerates the row's concurrency token on
save ([ADR-1012](1012-interceptor-managed-concurrency-tokens.md)). `TeamChangedCommand` follows such a commit, so a
team whose stored token equals the token its cached copy was materialized with has not changed.

## Decision

**A replica that may have missed a `team-changed` announcement compares the row token of each team it serves with the
store's and evicts only the teams whose token differs or whose row is gone, with their tenants.**

1. **A materialized team carries its row token.** `OpenIdTeam.ConcurrencyToken` is the concurrency token of the team
   row the team was materialized from.
2. **A tenant exposes its team.** `OpenIdTenant.Team` is the team the tenant was materialized under. A cached tenant
   can outlive its team's cache entry, because both lifetimes are counted from different instants, and it holds that
   team's settings provider.
3. **A cache reports the revisions it serves.** `TeamRevision(TeamId, ConcurrencyToken)` pairs a team with its token.
   `IOpenIdTeamCache.GetRevisionsAsync` returns one revision per cached team, and
   `IOpenIdTenantCache.GetTeamRevisionsAsync` returns the distinct revisions of the teams its cached tenants were
   materialized under.
4. **The store answers the current tokens in one query.** `ITeamStore.GetConcurrencyTokensAsync` takes the team
   identifiers and returns the row token of each listed team that exists, keyed by team identifier without regard to
   case, filtered in the DBMS
   ([ADR-1080](1080-store-query-methods-push-filtering-to-dbms.md)).
5. **The handler evicts the stale teams.** `TeamChangeBroadcastHandler.HandleMissedMessagesAsync` collects both
   caches' revisions, reads the current tokens in batches of 500, and evicts each team whose token differs or whose
   row is gone exactly as an announcement does: the team, then its tenants. When the tokens cannot be read, it logs
   `TeamCacheRevalidationFailed` (event 1037) and clears both caches, which is always safe.

## Options considered

- **Clear every cache.** Simple and always correct, but every missed message costs a full re-materialization of what the
  replica serves. Rejected.
- **A change journal read since the disconnect.** It needs a change-time column on the team row, the instant the
  subscription dropped and a margin for clock skew between replicas. Rejected.
- **Polling a version column.** [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) rejects
  continuous polling. Rejected.

## Consequences

- A restored connection costs one indexed query per 500 served teams, and evicts only the teams that changed.
- A reconciliation record write changes the row token, so such a team is evicted too, which is conservative.
- The `tenant-changed` channel still clears the tenant cache when it may have missed a message, because its
  announcements are not all commits to the tenant row: a root-key change is announced without one.
- A custom implementation of `OpenIdTeam`, `OpenIdTenant`, `IOpenIdTeamCache`, `IOpenIdTenantCache` or `ITeamStore`
  implements the added members.

## References

- [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) — replicas announce changes through a broadcast
  port
- [ADR-1412](1412-settings-are-reconciled-per-team.md) — settings are reconciled per team
- [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — settings converge through
  one reconciliation
- [ADR-1012](1012-interceptor-managed-concurrency-tokens.md) — the interceptor manages concurrency tokens
- [ADR-1080](1080-store-query-methods-push-filtering-to-dbms.md) — store query methods push filtering to the DBMS

# 1416. A replica that may have missed announcements evicts only teams and tenants whose row changed

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md)

## Context

A replica serves teams and tenants from `IOpenIdTeamCache` and `IOpenIdTenantCache`. The `team-changed` and
`tenant-changed` channels announce a change to every replica
([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)).
A replica whose subscription was interrupted may have missed a message, which the broadcast port tells its handler
through `IReplicaBroadcastHandler.HandleMissedMessagesAsync`
([ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md)). This record decides what the `team-changed`
and `tenant-changed` handlers do with that call.

Clearing every cached team, tenant and master key whenever a channel may have missed a message re-materializes
everything the replica serves, with its store reads, settings providers and key collections, for changes that mostly
did not happen.

Every committed change to a team or tenant modifies its row, and the interceptor regenerates the row's concurrency
token on save
([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).
A cached copy whose token equals the stored token has not changed.

## Decision

**A replica that may have missed a `team-changed` or `tenant-changed` announcement compares the row token of each team
and tenant it serves with the store's and evicts only those whose token differs or whose row is gone.**

1. **A materialized team and tenant carry their row token.** `OpenIdTeam.ConcurrencyToken` and
   `OpenIdTenant.ConcurrencyToken` are the concurrency token of the row the object was materialized from.
2. **A tenant exposes its team.** `OpenIdTenant.Team` is the team the tenant was materialized under. A cached tenant
   can outlive its team's cache entry, because both lifetimes are counted from different instants, and it holds that
   team's settings provider.
3. **A cache reports the revisions it serves.** `TeamRevision(TeamId, ConcurrencyToken)` pairs a team with its token
   and `TenantRevision(TenantId, ConcurrencyToken)` pairs a tenant with its token.
   - `IOpenIdTeamCache.GetRevisionsAsync` returns a `TeamRevision` for every team a cached team was materialized
     over, its own team and then each parent
     ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md)).
   - `IOpenIdTenantCache.GetTeamRevisionsAsync` returns the same for every team a cached tenant was materialized
     over.
   - `IOpenIdTenantCache.GetRevisionsAsync` returns a `TenantRevision` for every cached tenant.
4. **The store answers the current tokens in one query.** `ITeamStore.GetConcurrencyTokensAsync` and
   `ITenantStore.GetConcurrencyTokensAsync` take identifiers and return the row token of each listed row that exists,
   keyed by identifier without regard to case and filtered in the DBMS
   ([ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).
5. **The team handler evicts the stale teams.** `TeamChangeBroadcastHandler.HandleMissedMessagesAsync` collects both
   caches' team revisions, reads the current tokens in batches of 500, and evicts each team whose token differs or
   whose row is gone exactly as an announcement does: the team, every team beneath it, and their tenants. When the
   tokens cannot be read, it logs `TeamCacheRevalidationFailed` (event 1037) and clears both caches, which is always
   safe.
6. **The tenant handler evicts the stale tenants.** `TenantChangeBroadcastHandler.HandleMissedMessagesAsync` reads the
   tenant tokens in batches of 500 and evicts each tenant whose token differs or whose row is gone exactly as an
   announcement does: the cache entry and the tenant's master keys. When the tokens cannot be read it logs
   `TenantCacheRevalidationFailed` (event 1040), and when an eviction fails it logs `TenantCacheEvictionFailed` (event
   1041). In both cases it then evicts every cached tenant and master key, which is always safe.
7. **Every change the tenant channel carries commits to the tenant row.** A record, settings or secrets write
   regenerates the row's token. A root key set, state change or removal is followed by
   `ITenantStore.MarkSecretsChangedAsync`, which issues new secrets and row tokens, before
   `OwnerRootKeyChangeAnnouncer` sends `TenantChangedCommand`. The command is sent even when the mark fails.
8. **A seeded tenant carries the row token its seed committed**
   ([ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md)).
9. **Master keys of tenants the replica does not cache are left to the `master-key-changed` handler.** That handler
   evicts every owner's master keys when it may have missed a message
   ([ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md)).

## Options considered

- **Clear every cache.** Simple and always correct, but every missed message costs a full re-materialization of what
  the replica serves. Rejected.
- **A root key revision carried by the cached tenant.** The tenant cache would read the secrets layer's root key
  records and compare two stores, where the tenant row already commits to every change. Rejected.
- **A change journal read since the disconnect.** It needs a change-time column on the row, the instant the
  subscription dropped and a margin for clock skew between replicas. Rejected.
- **Polling a version column.** [ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md) rejects
  continuous polling. Rejected.
- **Compare row tokens.** A restored connection evicts only what changed and needs no new column or clock. Chosen.

## Consequences

- A restored connection costs one indexed query per 500 served rows, and evicts only the teams and tenants that
  changed.
- A reconciliation record write changes a team's token, so that team is evicted too, which is conservative.
- A root key change makes every replica's secrets poll read the tenant's secrets again.
- A management write that read the tenant row before a root key change is answered `412`
  ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md)) and a retry succeeds.
- When marking a tenant's secrets changed fails, the announcement still goes out, so a replica that receives it evicts
  the tenant. A replica that misses it finds the row token unchanged and serves the cached tenant until
  `TenantCacheLifetime` expires it, which bounds that window.

## References

- [ADR-1205](1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md) — replicas announce changes through a
  broadcast port
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)
  — settings converge through one reconciliation
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)
  — the interceptor manages concurrency tokens
- [ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md) — store
  query methods push filtering to the DBMS
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md) — a team can nest beneath a parent team
- [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md)
  — a master key rotates, and its data keys are rewrapped in the background
- [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) — a tenant is provisioned
  through one pipeline
- [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md) — a management write that loses a
  concurrency race is answered 412
- [project-template ADR-0140](project-template/0140-replicas-converge-through-reconciliation.md) — replicas converge
  through reconciliation

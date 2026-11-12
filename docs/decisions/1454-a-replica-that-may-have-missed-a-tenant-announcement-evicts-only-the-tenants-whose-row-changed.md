# 1454. A replica that may have missed a tenant announcement evicts only the tenants whose row changed

- **Status:** Accepted (amends [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md),
  [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) decision 2,
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md),
  [ADR-1416](1416-a-replica-that-may-have-missed-a-team-announcement-evicts-only-the-teams-whose-row-changed.md); amended by
  [ADR-1463](1463-a-seeded-tenant-is-materialized-from-the-row-its-seed-committed.md): a seeded tenant carries the
  row token its seed committed)
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md)

## Context

A replica serves tenants from `IOpenIdTenantCache`. The `tenant-changed` channel announces a tenant change to every
replica ([ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md)), and a replica whose subscription was
interrupted may have missed a message, which
[ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) has its handler learn of.

Clearing every cached tenant and every master key whenever the channel may have missed a message re-materializes
everything the replica serves, with its store reads, settings providers and key collections, for changes that mostly did
not happen.

A record, settings or secrets write modifies the tenant row, and the interceptor regenerates the row's concurrency token
on save ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)). A tenant whose stored token equals the token its
cached copy was materialized with has not changed.

## Decision

**A replica that may have missed a `tenant-changed` announcement compares the row token of each tenant it serves with
the store's and evicts only the tenants whose token differs or whose row is gone.**

1. **A materialized tenant carries its row token.** `OpenIdTenant.ConcurrencyToken` is the concurrency token of the
   tenant row the tenant was materialized from.
2. **Every change the channel carries commits to the tenant row.** A record, settings or secrets write regenerates the
   row's token. A root key set, state change or removal is followed by `ITenantStore.MarkSecretsChangedAsync`, which
   issues new secrets and row tokens, before `OwnerRootKeyChangeAnnouncer` sends `TenantChangedCommand`. The command is
   sent even when the mark fails.
3. **A cache reports the revisions it serves.** `TenantRevision(TenantId, ConcurrencyToken)` pairs a tenant with its
   token. `IOpenIdTenantCache.GetRevisionsAsync` returns one revision per cached tenant.
4. **The store answers the current tokens in one query.** `ITenantStore.GetConcurrencyTokensAsync` takes the tenant
   identifiers and returns the row token of each listed tenant that exists, keyed by tenant identifier without regard to
   case, filtered in the DBMS ([ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).
5. **The handler evicts the stale tenants.** `TenantChangeBroadcastHandler.HandleMissedMessagesAsync` reads the current
   tokens in batches of 500 and evicts each tenant whose token differs or whose row is gone exactly as an announcement
   does: the cache entry and the tenant's master keys. When the tokens cannot be read, it logs
   `TenantCacheRevalidationFailed` (event 1040), and when an eviction fails it logs `TenantCacheEvictionFailed` (event
   1041). In both cases it then evicts every cached tenant and master key, which is always safe.
6. **Master keys of tenants the replica does not cache are left to the `master-key-changed` handler.** That handler
   evicts every owner's master keys when it may have missed a message
   ([ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)).

## Options considered

- **Clear every cached tenant and master key.** Simple and always correct, but every missed message costs a full
  re-materialization of what the replica serves. Rejected.
- **A root key revision carried by the cached tenant.** The tenant cache would read the secrets layer's root key records
  and compare two stores, where the tenant row already commits to every change. Rejected.
- **A change journal read since the disconnect.** It needs a change-time column on the tenant row, the instant the
  subscription dropped and a margin for clock skew between replicas. Rejected.
- **Polling a version column.** [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) rejects
  continuous polling. Rejected.

## Consequences

- A restored connection costs one indexed query per 500 cached tenants, and evicts only the tenants that changed.
- A tenant whose materialization seeded keys carries the token read before its seed, so it is evicted once.
- A root key change makes every replica's secrets poll read the tenant's secrets again.
- A management write that read the tenant row before a root key change is answered `412`
  ([ADR-1214](1214-a-management-write-that-loses-a-race-is-answered-412.md)) and a retry succeeds.
- When marking a tenant's secrets changed fails, the announcement still goes out, so a replica that receives it evicts
  the tenant. A replica that misses it finds the row token unchanged and serves the cached tenant until
  `TenantCacheLifetime` expires it, which bounds that window.
- A custom implementation of `OpenIdTenant`, `IOpenIdTenantCache` or `ITenantStore` implements the added members.

## References

- [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) — replicas announce changes through a broadcast
  port
- [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — settings converge through
  one reconciliation
- [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)
  — a master key rotates, and its data keys are rewrapped in the background
- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md) — a tenant can wrap its master keys
  under a root key it controls
- [ADR-1416](1416-a-replica-that-may-have-missed-a-team-announcement-evicts-only-the-teams-whose-row-changed.md) — a
  replica that may have missed a team announcement evicts only the teams whose row changed
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) — the interceptor manages concurrency tokens
- [ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md) — store query methods push filtering to the DBMS
- [ADR-1214](1214-a-management-write-that-loses-a-race-is-answered-412.md) — a management write that loses a
  concurrency race is answered 412

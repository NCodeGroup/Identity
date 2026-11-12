# 1199. Tenant usage is a daily ledger that replicas add to once and prune in batches

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md)

## Context

The daily quotas of [ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md)
keep only the current UTC day's count, in a shared counter that expires at midnight. The hosted platform needs more: it
bills a plan on what a tenant consumed over a period, reports that usage to the customer, and admits and bills an
overage instead of refusing at the quota. That needs a durable record of each tenant's usage, with its own retention,
that the management API can read.

Usage must stay out of metric series: a tenant attribute on a metric creates a series per tenant
([ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)). Two
sources were proposed: rolling the day's quota counter up into storage, or appending a record per request through the
event seam.

Several replicas add to the same rows, and a save can fail with its outcome unknown: a timeout after the database
committed looks the same as one before. A retry of a batch that had committed would add its counts twice, and billing
data must not overcount. A ledger also grows with use, so it needs a retention period and a prune that stays bounded
however many days are past it, on every Entity Framework provider and with several replicas pruning at once.

## Decision

**Usage is a ledger in the OpenID database with one row per tenant, UTC day and meter. Each replica counts usage in
memory and adds it to the ledger every minute in numbered batches that the ledger adds once each, and each replica
prunes the days past retention in bounded batches.**

1. **One row per tenant, UTC day and meter.** The `TenantUsage` table holds the tenant's id, its team path, the day,
   the meter and the count, unique on the tenant, day and meter. The day is the UTC calendar day, the window the quotas
   count. A row references its tenant by id, with no foreign key and no tenant query filter, so the usage of a deleted
   tenant survives for its final bill and rolls up to every team above it
   ([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
   [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md)).
2. **The meter is an open vocabulary**
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). The family records `requests`,
   `token_requests`, `requests_overage` and `token_requests_overage` (`TenantUsageMeters`), and a package records its
   own through `ITenantUsageRecorder.Record`, which refuses a name the ledger cannot store, so a bad name fails at its
   caller rather than at every flush.
3. **Every tenant is metered, with or without a quota.** `RateLimitEndpointFilter` records the meters that the
   limiter's stages added once the endpoint has run, unless a stage refused the request
   ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)). Public metadata and
   refused requests are not usage. Each meter is recorded at most once per request.
4. **Replicas count in memory and add to the ledger.** `ITenantUsageRecorder.Record` increments an in-memory count
   under a lock and costs no I/O on the request path. `TenantUsageFlushService` drains the counts every
   `TenantUsageOptions.FlushInterval` (one minute) in batches of 256 rows, each read with one query per day and saved as
   one unit of work, so the time between a batch's read and its save stays short however many tenants are active. The
   row's concurrency token serializes replicas that add to the same row, so this works on every provider.
5. **A batch is added once.** A replica draws a `ReplicaId` (`Guid`) per process and numbers its batches from 1.
   `ITenantUsageStore.AddAsync(replicaId, batchNumber, usage, cancellationToken)` adds the counts and sets the
   replica's `LastBatchNumber` in `TenantUsageCheckpoints` (unique on `ReplicaId`, with a concurrency token) in the same
   unit of work, and skips a batch numbered at or below it, so a retried save that had committed adds nothing. An empty
   batch or a number below 1 throws `ArgumentOutOfRangeException`.
6. **A failed batch is retried in order.** It stays at the head of the queue and is retried before anything else; new
   usage waits in the recorder until the queue is empty, so memory stays bounded and a later batch never commits before
   an earlier one. A cancelled flush logs nothing.
7. **A stop flushes once more.** `StopAsync` memoizes one final flush under a lock, and every concurrent stop awaits
   it; a semaphore serializes it with the periodic flush. Usage it cannot add, or holds when the stop token ends its
   wait, is logged as `TenantUsageDiscardedOnStop` (event 2051, warning). An abrupt stop loses at most one interval of
   one replica's usage.
8. **The ledger keeps 400 days by default.** `TenantUsageOptions.Retention` changes it. That covers a full year with a
   margin for a late invoice or dispute. `TenantUsageOptions` is bound from `OpenId:TenantUsage` and validated when the
   host starts
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
9. **The prune is bounded.** `TenantUsagePruneService` prunes every `TenantUsageOptions.PruneInterval` (one hour, a
   validated timer period) on a `PeriodicTimer` through the registered `TimeProvider`.
   `ITenantUsageStore.RemoveBeforeAsync(day, limit, cancellationToken)` stages up to `limit` usage entries before the
   day, and every checkpoint whose `LastDay` is before it, for removal through the change tracker, and returns the
   staged usage count. The service repeats while a 256-entry batch is full. A batch lost to another replica's prune
   counts as progress, so replicas need not coordinate; any other failure is logged (`TenantUsagePruneFailed`) and
   retried next tick.
10. **The management API reads the ledger.** `GET /api/tenants/{tenantId}/usage?from=&to=` (authorized as a read of the
    tenant node) and `GET /api/teams/{teamId}/usage` (`ITenantUsageStore.ListByTeamAsync`, a path prefix match,
    `TeamUsageEntry`) return inclusive UTC days ordered by day and meter. The range defaults to the last 30 days and
    spans at most 366. The console's Usage tab shows one column per meter present, so a new meter appears without a UI
    change.
11. **Usage is not a metric dimension**
    ([ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)).
    The ledger lags by up to one flush interval, and a quota keeps using the shared counter, which must be exact.

## Options considered

- **Roll the day's quota counter up into storage.** Rejected: the counter exists only for a tenant with a quota, counts
  refused requests as well as admitted ones, and expires at midnight, so a rollup races its expiry and misses every
  tenant without a quota.
- **Append a record per request through the event seam.** Rejected: a row per request is log-level write load on the
  database and on every event sink.
- **Bulk update or set-based delete statements.** Rejected: not every Entity Framework provider supports them, the
  InMemory provider included, and a concurrency token and the change tracker give the same correctness.
- **Store usage in the shared-state store (Valkey).** Rejected: shared state is for short-lived coordination and may be
  evicted; billing data belongs with the tenant's other durable records.
- **A marker row per batch.** Rejected: it grows with every flush of every replica and needs its own prune; one
  checkpoint row per replica carries the same fact.
- **Per-replica absolute totals.** Rejected: the ledger row sums several replicas, so a total per replica needs a
  second table and a sum on every read.
- **Document the overcount.** Rejected: billing data that can double a batch is wrong, not approximate.
- **One unbounded prune unit of work.** Rejected: it holds a transaction and a change tracker over every day past
  retention, which grows without limit after an outage of the prune.
- **Numbered batches added once, bounded batches pruned, the concurrency token serializing replicas.** Chosen.

## Consequences

- A request costs one in-memory increment per meter; the database sees one small unit of work per replica per minute.
- A retried save that had committed adds nothing, so the ledger does not overcount, and a failed batch never lets a
  later one commit first.
- The ledger can lag by up to one flush interval, and an abrupt stop of a replica loses up to one interval of its
  usage. Billing tolerates both.
- Each prune unit of work is bounded at 256 entries, so a long outage of the prune cannot make one large transaction.
- A deleted tenant's usage survives and rolls up to every team above it.
- The ledger is not exposed as a metric; dashboards that need per-tenant usage read the management API.
- Tests cover the recorder, the flush service's retry and stop, the store's checkpoint, the prune loop and the
  management endpoints, with the InMemory provider.

## References

- [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md) — pruning
  in bounded batches
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) —
  validated options
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md) —
  protocol metrics
- [ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md) — daily quotas and
  the overage meters
- [ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md) — rate-limit stages
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) and
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md) — teams and team paths
- `src/NCode.Identity.OpenId.Authentication/Usage/`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/TenantUsageStore.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs`

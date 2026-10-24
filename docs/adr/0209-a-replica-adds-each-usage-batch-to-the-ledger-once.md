# 209. A replica adds each usage batch to the ledger once

- **Status:** Accepted (amended by [ADR-0213](0213-the-usage-ledger-is-pruned-in-bounded-batches.md): the prune removes
  a bounded batch per unit of work)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

[ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) §3 returned the counts of a batch that failed to
save to memory, and the next flush added them again. A save can commit and still fail at its caller: the connection
drops after the commit, a timeout fires, or a shutdown cancels the call. The batch was then in the ledger and in memory,
and the next flush added it a second time, so the ledger overcounted billed usage (backlog `R25`).

A host can also stop a service twice at once. `WebApplicationFactory` stops the host while the host's own `RunAsync`
stops it too, and an application can call `StopAsync` while `RunAsync` waits. The stop that returned first let its
caller dispose the host's services under the other stop's final flush. That flush failed, and its warning reached a
logging provider that was already disposed (backlog `R54`).

## Decision

**A replica numbers its usage batches, the ledger records the last batch each replica added in the same unit of work as
the counts, and a failed batch is retried under its number.**

1. **A replica numbers its batches.** `TenantUsageFlushService` draws a `ReplicaId` once per instance, which is once per
   process, and numbers its batches 1, 2, 3, … in the order it adds them. A batch keeps its number until the ledger has
   added it.
2. **The ledger records the last added batch with the counts.**
   `ITenantUsageStore.AddAsync(replicaId, batchNumber, usage, cancellationToken)` adds the counts and sets the replica's
   `LastBatchNumber` in its row of the `TenantUsageCheckpoints` table, in the same unit of work. A batch numbered at or
   below the replica's `LastBatchNumber` was added before, so the store does nothing for it, and a retry of a save that
   committed adds nothing. The checkpoint is unique on `ReplicaId` and carries a concurrency token
   ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)), so two units of work that add one replica's batches
   cannot both commit
   ([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)). An empty `usage` and a
   `batchNumber` below 1 throw `ArgumentOutOfRangeException`.
3. **A failed batch is retried in order, and new usage waits.** A batch that fails for any reason stays at the head of
   the replica's queue, with the batches behind it. Every flush retries the queue in order, and drains the recorder into
   new numbered batches only when the queue is empty. While a batch is held, new usage waits in the recorder, so memory
   stays bounded. The order matters: the ledger skips any number at or below the last one added, so a later batch must
   never commit before an earlier one. A flush that is cancelled logs nothing, and the final flush retries its batch.
4. **The checkpoint is removed with the days past retention.** The checkpoint keeps `LastDay`, the latest usage day the
   replica added. `RemoveBeforeAsync(day, limit)` removes the checkpoints whose `LastDay` is before the day along with
   up to `limit` of the usage entries before it ([ADR-0213](0213-the-usage-ledger-is-pruned-in-bounded-batches.md)), and
   returns the count of usage entries removed. A checkpoint lives as long as the usage it guards.
5. **The final flush runs once, never throws, and logs what it discards.** `TenantUsageFlushService.StopAsync` memoizes
   its task under a lock, so every later or concurrent caller awaits the same flush and no stop returns while the flush
   runs. A semaphore serializes the periodic flush and the final flush: the final flush waits for it with the stop
   token, and when the token ends the wait it logs the usage it holds as discarded and returns without flushing. The
   final flush retries the held batches, adds the new usage, and logs the usage it cannot add as
   `TenantUsageDiscardedOnStop` (event 2051, a warning).

## Options considered

- **A marker row per batch.** Rejected: it writes one row per batch per replica per minute, and a retry after the prune
  of the marker counts the batch twice.
- **A conditional upsert or a set-based increment.** Rejected: not every provider supports it (Entity Framework
  InMemory does not), and it bypasses the concurrency token
  ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md),
  [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)).
- **Per-replica absolute totals, summed on read.** Rejected: it multiplies the rows, and every read must sum them.
- **Document the overcount.** Rejected: a billing ledger must not charge for usage twice.

## Consequences

- Each request is counted once, even when the outcome of a save is unknown.
- A batch that keeps failing holds back later usage in memory, until the database accepts it.
- Each process adds one checkpoint row, which is pruned with the retention.
- **Breaking:** the `ITenantUsageStore.AddAsync` signature takes the replica's identifier and the batch's number. A
  store implementer must skip a batch numbered at or below the replica's last added one, record the number in the same
  unit of work as the counts, and remove the checkpoints with the expired days in `RemoveBeforeAsync`. An Entity
  Framework deployment applies the `AddTenantUsageCheckpoints` migration, which creates the `TenantUsageCheckpoints`
  table.
- This implements backlog `R25` and the final-flush part of `R54`; the logging part of `R54` is
  [ADR-0210](0210-the-reference-hosts-log-to-standard-output.md).

## References

- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor-managed concurrency tokens that make two
  units of work conflict.
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger that this decision amends.
- [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a store-neutral
  exception.
- [ADR-0210](0210-the-reference-hosts-log-to-standard-output.md) — the reference hosts log to standard output, the
  other part of `R54`.
- Code: `NCode.Identity.OpenId.Authentication.Usage.TenantUsageFlushService`,
  `NCode.Identity.OpenId.Persistence.Stores.ITenantUsageStore`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores.TenantUsageStore`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities.TenantUsageCheckpointEntity`,
  `tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/ApplicationRunLoopHost.cs`.

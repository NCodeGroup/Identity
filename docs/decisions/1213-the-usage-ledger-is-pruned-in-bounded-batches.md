# 1213. The usage ledger is pruned in bounded batches that every provider supports

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md)

## Context

The usage ledger keeps the days within its retention and removes the older ones
([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)). The prune removed the days past retention
with two `ExecuteDeleteAsync` calls. The Entity Framework InMemory provider, which the hermetic host uses, does not
support them, so every prune failed, and the failure was retried and logged (`TenantUsagePruneFailed`) at every flush.
[ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) rejects a bulk update statement because not
every provider supports it (issue [#264](https://github.com/NCodeGroup/identity/issues/264)).

## Decision

**The ledger is pruned through the change tracker, in bounded batches, each in its own unit of work.**

1. **The store stages a bounded batch for removal.**
   `ITenantUsageStore.RemoveBeforeAsync(day, limit, cancellationToken)` stages up to `limit` usage entries before the
   day, and every checkpoint whose last day is before it, for removal when the unit of work is saved, and returns the
   count of usage entries it staged. The Entity Framework store reads the rows and removes them through the change
   tracker, so every provider supports it.
2. **A hosted service prunes on an interval.** `TenantUsagePruneService` prunes every `TenantUsageOptions.PruneInterval`
   (`OpenId:TenantUsage:PruneInterval`, one hour by default, validated as a timer period when the host starts —
   [ADR-1221](1221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md)) on a `PeriodicTimer`
   through the registered `TimeProvider`, in units of work of 256 entries, and repeats while a batch is full.
3. **A lost race is progress.** A batch whose save loses a race to the prune of another replica counts as progress,
   because the other replica removed rows of it, and the next batch is read.
4. **Any other failure is retried at the next tick.** The service logs it (`TenantUsagePruneFailed`), and the prune runs
   again at the next tick.

## Options considered

- **Set-based deletes on relational providers, with a fallback for the others.** Rejected: it has two code paths, and
  the delete runs outside the unit of work.
- **Keep the set-based deletes and wait a day after a failure.** Rejected: the InMemory provider never prunes, and a
  real failure stays hidden for a day.
- **One unbounded unit of work.** Rejected: it loads a whole day of every tenant at once.

## Consequences

- The ledger is pruned on every provider.
- Replicas that prune at the same time waste at most one batch.
- A long backlog of expired days is pruned in many small units of work.
- **Breaking:** `ITenantUsageStore.RemoveBeforeAsync` takes a `limit`, and removes the rows when the unit of work is
  saved.
- This implements issue [#264](https://github.com/NCodeGroup/identity/issues/264).

## References

- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger and its retention.
- [ADR-1209](1209-a-replica-adds-each-usage-batch-to-the-ledger-once.md) — the checkpoints that the prune removes with
  the days they guard.
- [ADR-1208](1208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a
  `StoreConcurrencyException`.
- Code: `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.TenantUsageStore`,
  `NCode.Identity.OpenId.Authentication.Usage.TenantUsagePruneService`.

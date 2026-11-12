# 1303. Audit records are kept in an indexed store that each replica writes in batches

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md)

## Context

[ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) fixes the audit record, a
CloudTrail-shaped JSON object with an indexable envelope, and logs it. A log rotates and is often kept by another team,
so audit history that an operator can query by tenant, subject, client, actor, resource or trace needs a store of its
own, and the management API needs a lookup over it.

Every audited operation publishes its event inline. The events library's background delivery is a bounded queue that
drops events when full, and the server does not enable it, so a handler that wrote to the database would add a round trip
to every audited request.

## Decision

**Each replica queues the record of every audit event in memory and writes the queue to an indexed `AuditRecords` table in
batches; the store keeps a record for 90 days by default.**

1. **The store keeps the envelope and the whole record.** `IAuditRecordStore` (`NCode.Identity.OpenId.Persistence`)
   persists `PersistedAuditRecord`: the envelope fields ADR-1283 names as indexed (event id and time, name, category,
   outcome, severity, tenant, subject, client, actor type and id, trace id and source IP), the resources in the child
   table `AuditRecordResources`, and the whole record as `AuditRecordWriter` writes it, kept as a `JsonElement` in a
   `text` column like every other JSON column of the context. Each index leads with the field a lookup filters on and
   then the event time. An envelope value longer than its column is not indexed: an optional field is `null` and a
   required one keeps its leading characters, while the JSON keeps it whole. Records are not tenant-scoped data: they
   outlive their tenant until their retention ends, as the usage ledger does
   ([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)).
2. **Queued inline, written in the background.** `AuditRecordHandler` maps each audit event and adds it to a bounded
   in-memory queue (`QueueCapacity`, 10,000 by default), so the audited operation never waits on the database. A record
   that arrives when the queue is full is dropped and counted, and the count is logged once per flush: the log, which
   every audit event reaches whatever the store does, stays the complete record.
3. **Batches, written once.** `AuditRecordFlushService` writes the queue every `FlushInterval` (five seconds) and once
   more when the host stops, in batches of 256, each its own unit of work. A stop writes the held records once, even when
   the host stops the service twice: concurrent stops share one final flush. A batch that fails is held and written
   first next time. The store skips an event identifier it already holds, under a unique index on the event id, so a
   batch whose save committed but reported a failure adds nothing twice.
4. **Retention.** `AuditRecordPruneService` removes the records older than `Retention` (90 days by default) every
   `PruneInterval` (one hour by default), on a `PeriodicTimer` through the registered `TimeProvider`, 256 records per
   unit of work while a batch is full. A batch whose save loses a race to another replica's prune counts as a full
   batch, because that replica removed records of it. Any other failure, a store timeout included, is logged
   (`AuditRecordPruneFailed`) and the prune runs again at the next tick; only the host's stopping token ends the
   service. With `Enabled: false` it does not run, as the writer does not. The store declares its retention and is
   pruned in bounded batches by its own hosted service, as
   [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md) requires.
5. **Configuration.** `OpenId:AuditRecords` binds `AuditRecordOptions` strictly and `AuditRecordOptionsValidator` checks
   it when the host starts
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)):
   `FlushInterval` and `PruneInterval` are periods a timer accepts, `QueueCapacity` is at least one and `Retention` at
   least one day; `Enabled: false` keeps the records out of the store and leaves them in the log.
6. **The management API looks records up newest first.** `GET api/tenants/{tenantId}/audit-records` returns a page of the
   addressed tenant's records, newest first, filtered by a time range (`startTime` inclusive, `endTime` exclusive),
   `eventName`, `eventCategory`, `outcome`, `subjectId`, `clientId`, `actorId`, `traceId`, and a resource by
   `resourceType` and `resourceId`, which matches the target or any owner it names, as CloudTrail's `LookupEvents` does.
   It pages by event time, with the surrogate identifier breaking ties between records of the same instant, over the
   index (`TenantId`, `EventTime`, `Id`); the opaque cursor of the other management lists carries the last item's event
   time and identifier. Records are not tenant-scoped data, so the tenant is a criterion of every lookup, and a record
   without a tenant is not listed. Each item carries the envelope and the whole `record`.

## Options considered

- **Write each record inline, in the audited request's unit of work.** Rejected: a database round trip on every audited
  operation, and an audit write that fails would fail the operation.
- **The events library's background delivery.** Rejected: it delivers one event at a time, so it would write one row per
  unit of work.
- **A transactional outbox.** Rejected: the log already keeps what a crashing replica held.
- **An in-memory queue written in batches by a hosted service.** Chosen: the audited operation never waits, and a batch
  is one unit of work.

## Consequences

- An operator looks up a tenant's audit history through the management API by subject, client, actor, resource, trace or
  event name over a time range.
- A replica that stops abruptly loses at most one `FlushInterval` of records from the store, never from the log.
- The `AuditRecords` and `AuditRecordResources` tables grow with the audited traffic of `Retention`; a deployment with a
  compliance need sets a longer one, and one without a store need sets `Enabled: false`.
- A full queue drops records from the store, and the logged count says how many.

## References

- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) — the audit record
- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) — the usage ledger and
  its prune service, which the audit store mirrors
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) — the
  startup validation of the options
- [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md) — bounded
  pruning of data that grows with use
- Issue [#468](https://github.com/NCodeGroup/identity/issues/468) — the prune interval and its own hosted service

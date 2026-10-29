# 303. Audit records are kept in an indexed store that each replica writes in batches

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

[ADR-0283](0283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) fixed the audit record, a
CloudTrail-shaped JSON object with an indexable envelope, and logged it, leaving a persisted store and a lookup API to be
decided separately (decision 16). A log rotates and is often kept by another team, so audit history an operator can query
by tenant, subject, client, actor, resource or trace needed a store of its own (issue #383); the management lookup API
builds on it (issue #384).

Every audited operation publishes its event inline. The events library's background delivery is a bounded queue that
drops events when full, and the server does not enable it, so a handler that wrote to the database would add a round trip
to every audited request.

## Decision

**Each replica queues the record of every audit event in memory and writes the queue to an indexed `AuditRecords` table in
batches; the store keeps a record for 90 days by default.**

1. **The store keeps the envelope and the whole record.** `IAuditRecordStore` (`NCode.Identity.OpenId.Persistence`)
   persists `PersistedAuditRecord`: the envelope fields ADR-0283 names as indexed (event id and time, name, category,
   outcome, severity, tenant, subject, client, actor type and id, trace id and source IP), the resources in a child
   table, and the record's JSON as `AuditRecordWriter` writes it. Each index leads with the field a lookup filters on and
   then the event time. An envelope value longer than its column is not indexed: an optional field is `null` and a
   required one keeps its leading characters, while the JSON keeps it whole. Records are not tenant-scoped data: they are
   kept after their tenant is deleted, until their retention ends, as the usage ledger is
   ([ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)).
2. **Queued inline, written in the background.** `AuditRecordHandler` maps each audit event and adds it to a bounded
   in-memory queue (`QueueCapacity`, 10,000 by default), so the audited operation never waits on the database. A record
   that arrives when the queue is full is dropped and counted, and the count is logged once per flush: the log, which
   every audit event reaches whatever the store does, stays the complete record.
3. **Batches, written once.** `AuditRecordFlushService` writes the queue every `FlushInterval` (five seconds) and once
   more when the host stops, in batches of 256, each its own unit of work. A batch that fails is held and written first
   next time. The store skips an event identifier it already holds, under a unique index, so a batch whose save committed
   but reported a failure adds nothing twice.
4. **Retention.** The same service removes the records older than `Retention` (90 days, CloudTrail's event history) in
   batches, at most once an hour.
5. **Configuration.** `OpenId:AuditRecords` binds `AuditRecordOptions` strictly and validates it when the host starts
   ([ADR-0221](0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md)); `Enabled: false`
   keeps the records out of the store and leaves them in the log.

## Options considered

- **Write each record inline, in the audited request's unit of work.** Rejected: a database round trip on every audited
  operation, and an audit write that fails would fail the operation.
- **The events library's background delivery.** Rejected: it delivers one event at a time, so it would write one row per
  unit of work, and enabling it changes every handler that opted into it.
- **A transactional outbox.** Deferred to a durable job dispatcher (issue #25): it would keep a record that a replica
  holds when it crashes, which the log keeps today.

## Consequences

- An operator queries audit history by tenant, subject, client, actor, resource, trace or event name over a time range;
  the lookup API is issue #384.
- A replica that stops abruptly loses at most `FlushInterval` of records from the store, never from the log.
- The `AuditRecords` and `AuditRecordResources` tables grow with the audited traffic of `Retention`; a deployment with a
  compliance need sets a longer one, and one without a store need sets `Enabled: false`.

## References

- [ADR-0283](0283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) — the audit record
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger the writer mirrors
- Issues [#383](https://github.com/NCodeGroup/Identity/issues/383) and
  [#384](https://github.com/NCodeGroup/Identity/issues/384)

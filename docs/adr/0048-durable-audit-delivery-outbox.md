# 48. Durable audit delivery: an outbox with at-least-once, publish-after-commit semantics

- **Status:** Proposed
- **Date:** 2026-10-04
- **Deciders:** NCode Group

## Context

[ADR-0047](./0047-auditing-and-events-observation-seam.md) establishes the event seam and its default
delivery: synchronous, fault-isolated, best-effort, with an optional in-memory background queue for slow
sinks. That best-effort posture is correct for observation — a failing or slow sink must never break or
stall a request — but it is **insufficient for compliance-grade audit**, which has the opposite
requirement: an audit record **must not be lost**, even across a process crash, and must truthfully
reflect committed state.

Three failure modes make best-effort delivery unsafe for mandatory audit:

1. **Crash loss.** The in-memory background queue drops everything it holds if the process dies; a
   synchronous sink that throws is swallowed. Either way the record is gone, with no record that it was
   lost.
2. **Phantom records.** Publishing an event for a persisted change _before_ its transaction commits
   emits an audit record for something that never happened if the transaction then rolls back.
3. **Lost-after-commit.** Publishing _after_ commit but out-of-band means a crash in the window between
   commit and dispatch loses the record for a change that did happen.

These are the classic dual-write problems between a database and a message/sink, and they cannot be
solved by tuning the in-process publisher alone.

## Decision

Layer a **transactional outbox** on top of the ADR-0047 seam for events that require durability, giving
compliance-grade audit **at-least-once, publish-after-commit** delivery, while leaving best-effort
observation exactly as ADR-0047 defines it.

_This ADR is a stub placeholder capturing the decision to pursue durable delivery as a separate concern;
the detailed mechanism (outbox schema, dispatcher, retry/back-off, de-duplication contract, and
ordering guarantees) is to be elaborated before it moves to Accepted._

Intended shape:

- **Durability is opt-in per event**, selected by the audit contract, not forced on every event. A plain
  observation event stays best-effort; an event marked durable is written to the outbox.
- **The outbox record is written in the same transaction as the state change it audits**, so the record
  commits atomically with the change — no phantom records, no lost-after-commit.
- **A dispatcher drains the outbox after commit** and delivers to the ADR-0047 handlers, retrying with
  back-off until each durable sink acknowledges, so delivery is **at-least-once**.
- **Consumers de-duplicate on the event's unique id** (the GUID already on the `AuditEvent` envelope per
  ADR-0047), because at-least-once implies possible redelivery.
- **Back-pressure never drops a durable record** (contrast best-effort, which may): the outbox is the
  durable buffer, so a slow sink delays delivery rather than losing data.
- The outbox lives in the **persistence layer** and is wired by composition, consistent with
  [ADR-0016](./0016-implementation-packages-depend-only-on-abstractions.md) and the EF Core persistence
  package; hosts that do not need durable audit do not register it.

## Options considered

_To be elaborated. Candidates include: a transactional outbox (chosen direction); synchronous
write-ahead audit that fails the operation if the audit write fails (rejected direction — couples the
request's success to the audit sink, the opposite of ADR-0047's fault isolation); and an external durable
queue/broker as the first hop (heavier operational surface, revisited if an outbox proves insufficient)._

## Consequences

_To be elaborated. Expected: mandatory audit survives crashes and reflects only committed state; hosts
that need durability pay a persistence cost and accept at-least-once (hence idempotent) delivery; the
best-effort seam stays unchanged for everything else._

## References

- [ADR-0047](./0047-auditing-and-events-observation-seam.md) — the event/observation seam this builds on,
  including the unique event id used for de-duplication.
- [ADR-0016](./0016-implementation-packages-depend-only-on-abstractions.md) — persistence-layer placement
  and composition wiring.
- [ADR-0041](./0041-host-owned-ef-migrations-deliberate-outside-development.md) — the host owns schema, so
  the outbox table is a host-owned migration.

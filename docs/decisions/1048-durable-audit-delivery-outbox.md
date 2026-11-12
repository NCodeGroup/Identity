# 1048. Durable audit delivery: an outbox with at-least-once, publish-after-commit semantics

- **Status:** Proposed
- **Date:** 2026-10-04
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md)

## Context

[project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md) writes an effect
that must follow a commit to an outbox in the same unit of work and delivers it at least once, through idempotent
handlers, so it is never sent for a change that rolled back or lost after one that committed. This record applies it to
mandatory audit.

[ADR-1047](./1047-auditing-and-events-observation-seam.md) establishes the event seam and its default
delivery: synchronous, fault-isolated, best-effort, with an optional in-memory background queue for slow
sinks. That best-effort posture is correct for observation — a failing or slow sink must never break or
stall a request — but it is **insufficient for compliance-grade audit**, which has the opposite
requirement: an audit record **must not be lost**, even across a process crash, and must truthfully
reflect committed state.

## Decision

Layer a **transactional outbox** on top of the ADR-1047 seam for events that require durability, giving
compliance-grade audit **at-least-once, publish-after-commit** delivery, while leaving best-effort
observation exactly as ADR-1047 defines it.

_This ADR is a stub placeholder capturing the decision to pursue durable delivery as a separate concern;
the detailed mechanism (outbox schema, dispatcher, retry/back-off, de-duplication contract, and
ordering guarantees) is to be elaborated before it moves to Accepted._

> **Reconciliation ([ADR-1090](1090-durable-outbound-delivery-dispatch-adapter.md)).** The generic job-dispatch port
> `IJobDispatcher` ([ADR-1090](1090-durable-outbound-delivery-dispatch-adapter.md)) is the seam this outbox plugs into:
> the transactional outbox described here is one **durable `IJobDispatcher` adapter** (write the serialized job in the
> state-changing transaction, drain and run its handler after commit), not a parallel mechanism. When this ADR is
> elaborated to Accepted, it should be specified as that adapter.

Intended shape:

- **Durability is opt-in per event**, selected by the audit contract, not forced on every event. A plain
  observation event stays best-effort; an event marked durable is written to the outbox.
- **The outbox record is written in the same transaction as the state change it audits**, so the record
  commits atomically with the change — no phantom records, no lost-after-commit.
- **A dispatcher drains the outbox after commit** and delivers to the ADR-1047 handlers, retrying with
  back-off until each durable sink acknowledges, so delivery is **at-least-once**.
- **Consumers de-duplicate on the event's unique id** (the GUID already on the `AuditEvent` envelope per
  ADR-1047), because at-least-once implies possible redelivery.
- **Back-pressure never drops a durable record** (contrast best-effort, which may): the outbox is the
  durable buffer, so a slow sink delays delivery rather than losing data.
- The outbox lives in the **persistence layer** and is wired by composition, consistent with
  [ADR-1500](./1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) and the EF Core persistence
  package; hosts that do not need durable audit do not register it.

## Options considered

_To be elaborated. Candidates include: a transactional outbox (chosen direction); synchronous
write-ahead audit that fails the operation if the audit write fails (rejected direction — couples the
request's success to the audit sink, the opposite of ADR-1047's fault isolation); and an external durable
queue/broker as the first hop (heavier operational surface, revisited if an outbox proves insufficient)._

## Consequences

_To be elaborated. Expected: mandatory audit survives crashes and reflects only committed state; hosts
that need durability pay a persistence cost and accept at-least-once (hence idempotent) delivery; the
best-effort seam stays unchanged for everything else._

## References

- [ADR-1047](./1047-auditing-and-events-observation-seam.md) — the event/observation seam this builds on,
  including the unique event id used for de-duplication.
- [ADR-1500](./1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) — persistence-layer placement
  and composition wiring.
- [ADR-1073](./1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md) — the host owns schema, so
  the outbox table is a host-owned migration.
- [ADR-1090](./1090-durable-outbound-delivery-dispatch-adapter.md) — the generic job-dispatch port this outbox is a
  durable adapter of.

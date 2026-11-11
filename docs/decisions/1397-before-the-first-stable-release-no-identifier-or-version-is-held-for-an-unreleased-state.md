# 1397. Before the first stable release, no identifier or version is held for an unreleased state

- **Status:** Accepted (amends
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md),
  [ADR-1246](1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md),
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) and
  [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md))
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md) and
  [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)

## Context

A version number and a reserved identifier each tell a consumer that something it holds stays meaningful: a SIEM
pipeline that parses audit records keys on `schema_version`, and an operator's log filter or alert keys on a log event
ID, so a shipped event ID is never renumbered or reused
([`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md)).

The family is greenfield and pre-release ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)): no audit
record and no log line has reached a consumer of a release. A version number or a reserved identifier that counts a
state only the repository held protects no consumer:

- [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) decision 9 sets
  `AuditRecordWriter.SchemaVersion` to `1.1` and counts `tenant_environment_type` as an additive change over a `1.0`
  record that no release carries, while
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) decision 4 defines the
  record's schema as `"1.0"`. A first release whose records start at `1.1` names a version that no `1.0` reader saw.
- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) decision 13,
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md) and
  [ADR-1246](1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md) decision 9 hold the
  log event IDs `14003`, `14004`, `2019` and `2044` unassigned. A first release whose event-ID tables hold those gaps
  protects no consumer and names events the code does not have.

## Decision

1. **A version number counts released states only.** The audit record's `schema_version` is `"1.0"`, and the `1.0`
   record carries every field `AuditRecordWriter` writes, `tenant_environment_type` among them. The rule that a field
   added to the record bumps the minor version applies to a field added to a released schema.
2. **An identifier is reserved only once it has shipped.** The append-only rule for log event IDs, enum members and
   wire vocabularies protects the values of a release. The ID of an event that exists only in the repository and is
   removed is free: no comment or record reserves it, and a later event may take it. Assigned IDs are not renumbered
   to close a gap, because a gap costs nothing and a renumbering changes every ID after it.
3. **The first stable release's values are the baseline.** From the first stable tag, every version number and
   identifier the release carries is shipped, and the append-only and version-bump rules govern it.
   [#535](https://github.com/NCodeGroup/identity/issues/535) retires this decision then.

## Options considered

- **Keep the reservations and the `1.1` version until the first release.** Rejected: they describe history that no
  consumer saw, and the first release would ship them as if a released reader depended on them.
- **Renumber the event IDs to remove the gaps.** Rejected: nothing reads a gap, and renumbering touches every
  assigned ID after it for no reader.
- **Count only released states (chosen).** The first release's version numbers and identifier tables describe what
  it carries, and the rules that protect consumers start where consumers do.

## Consequences

- `AuditRecordWriter.SchemaVersion` is `"1.0"`, and [`docs/audit-records.md`](../audit-records.md) documents the
  `1.0` record with `tenant_environment_type`.
- `EventIds.cs` in `NCode.Identity.Events` and `NCode.Identity.OpenId.Authentication` list the assigned IDs only;
  `14003`, `14004`, `2019` and `2044` are unassigned and free.
- A reviewer rejects a version bump or an identifier reservation made for a pre-release state.

## References

- [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)
- [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)
- [ADR-1246](1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md)
- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)
- [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md)
- [ADR-1393](1393-the-pre-release-product-is-described-as-it-is-and-its-changes-are-not-tracked.md)
- [`AuditRecordWriter`](../../src/NCode.Identity.Events.Abstractions/Audit/AuditRecordWriter.cs)
- [`NCode.Identity.Events` event IDs](../../src/NCode.Identity.Events/Logging/EventIds.cs)
- [`NCode.Identity.OpenId.Authentication` event IDs](../../src/NCode.Identity.OpenId.Authentication/Logging/EventIds.cs)

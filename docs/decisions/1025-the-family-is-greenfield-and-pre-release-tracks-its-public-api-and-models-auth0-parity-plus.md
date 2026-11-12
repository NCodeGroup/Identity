# 1025. The family is greenfield and pre-release, tracks its public API, and models Auth0-parity-plus

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)

## Context

The family is a greenfield **OpenID Connect + OAuth 2.0** server built from the ground up. It has not had a stable
release, and nothing in it has reached a consumer. Four forces shape day-to-day design and are worth deciding once, so
that no feature re-litigates them:

1. **How much backward compatibility is owed to an unreleased product.** With no external consumer to protect, a
   deprecation cycle, a compatibility shim or a dual code path buys nothing, and a half-migrated "two sources of truth"
   state is the worse outcome. The project owner's standing direction, recorded at the top of
   [`repository.instructions.md`](../../.github/instructions/repository.instructions.md), holds until the first stable
   release: take the best long-term, cohesive design whatever the refactor costs, nothing is shipped, and breaking
   changes are allowed and never tracked.
2. **How a public API stays reviewable.** An accidental change to the public surface (a renamed method, a removed
   overload, a changed signature, a flipped nullability) is trivially easy to make without noticing, and reviewer
   vigilance does not scale. The surface needs a machine-checked, diff-visible record, and a plan for the day it
   becomes a contract.
3. **What a version number or an identifier promises.** A version number and a reserved identifier each tell a
   consumer that something it holds stays meaningful: a SIEM pipeline that parses audit records keys on
   `schema_version`, and an operator's log filter or alert keys on a log event ID, so a shipped event ID is never
   renumbered or reused
   ([`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md)). A number or a
   reservation that counts a state only the repository held protects no consumer.
4. **How closely to follow Auth0.** Auth0's vocabulary and shapes (resource servers, scopes and permissions, client
   grants, tenants) are well understood and lower the onboarding cost
   ([ADR-1023](1023-scopes-and-resources-management-model.md) adopts them deliberately). The open question is whether
   to clone Auth0 exactly or to improve on it.

## Decision

**The family is greenfield until its first stable release, judges a breaking change by design quality alone, tracks its
public API surface with an analyzer so that it evolves compatibly once released, counts only released states in its
version numbers and identifiers, and models Auth0 with finer control where that is cheap and principled.**

1. **Greenfield, pre-release posture.** The owner's standing direction holds until the first stable release, when
   [#535](https://github.com/NCodeGroup/identity/issues/535) retires it. `AGENTS.md`'s Landing section sets
   `pre-release: untracked`, and every rule about how a text describes the product is
   [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)'s.
2. **A breaking change is judged by design quality alone.** A clean cutover beats a shim or a dual code path, and
   reviewers weigh no compatibility cost.
3. **Auth0-parity-plus.** Concepts follow Auth0's vocabulary and shapes (resource servers, scopes and permissions,
   client grants, tenants). The family adds finer, data-driven control where it is cheap and principled, and favors
   general mechanisms over hard-coded special cases.
4. **The test for a deviation from Auth0.** A deviation keeps the mental model an Auth0 user already has and gives the
   operator a control Auth0 lacks, at low complexity. Feature work cites Auth0 as the baseline and justifies each
   deviation that way. For example, a system resource server (`IsSystem`) is implicitly available to every client in
   its tenant, which delivers Auth0's "OIDC scopes just work" behavior through a general resource-server property
   rather than an OIDC special case, so the same mechanism can bless any operator-chosen resource server.
5. **The public API surface is explicit, diff-visible and guarded by an analyzer.** Every packable project carries
   [`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md),
   wired in [`Directory.Build.targets`](../../Directory.Build.targets), with a `PublicAPI.Shipped.txt` /
   `PublicAPI.Unshipped.txt` pair. `RS0016` fails the build for a public symbol absent from both files, and `RS0017`
   for a listed symbol that does not exist, so the surface changes only through a reviewable edit.
6. **Before the first stable release the surface lives in `PublicAPI.Unshipped.txt`.** `PublicAPI.Shipped.txt` holds
   only `#nullable enable`, and the churn of `PublicAPI.Unshipped.txt` is the routine signal of evolution, not a smell.
   At the first stable release the surface moves to `PublicAPI.Shipped.txt`.
7. **From the first stable release the shipped surface evolves backward-compatibly,** as
   [`docs/api-compatibility.md`](../api-compatibility.md) details: additive changes only, a new overload over a
   changed signature, required inputs as constructor parameters and optional inputs as `init` properties, wire-enum
   members appended and never renumbered, and removal by `[Obsolete]` first and then across a major version. A
   breaking change is a major version.
8. **A version number counts released states only.** The audit record's `schema_version`
   (`AuditRecordWriter.SchemaVersion`) is `"1.0"`, and the `1.0` record carries every field `AuditRecordWriter` writes,
   `tenant_environment_type` among them. The rule that a field added to the record bumps the minor version applies to
   a field added to a released schema.
9. **An identifier is reserved only once it has shipped.** The ID of a log event, enum member or wire value that
   exists only in the repository and is removed is free: no comment or record reserves it, and a later one may take
   it. Assigned IDs are never renumbered to close a gap, because a gap costs nothing and a renumbering changes every
   ID after it.
10. **The first stable release's values are the baseline.** From then on every version number and identifier it
    carries is shipped, and the append-only and version-bump rules govern it.

## Options considered

- **Maintain backward compatibility from day one.** Rejected: it forces deprecation cycles and compatibility shims
  that slow the clean design while no consumer exists to benefit, and it normalizes long-lived transitional code.
- **Clone Auth0 exactly.** Rejected: it forgoes improvements that are cheap to make early and hard to retrofit, and a
  faithful clone inherits Auth0's limitations without reason.
- **Diverge freely from Auth0.** Rejected: it discards the familiarity and the battle-tested model that make the
  system approachable and auditable, and novelty for its own sake raises onboarding and review cost.
- **Rely on code review to catch public-surface changes.** Rejected: humans miss surface changes, especially
  nullability changes and removed overloads.
- **Hold versions and reservations for unreleased states.** Rejected: they describe history that no consumer saw, and
  the first release would ship them as if a released reader depended on them.
- **Renumber the event IDs to close gaps.** Rejected: nothing reads a gap, and a renumbering touches every assigned ID
  after it for no reader.
- **Greenfield posture, an analyzer-tracked surface, released-states-only numbering and Auth0-parity-plus
  (chosen).** One durable reference each feature can cite, with rules that protect consumers starting where consumers
  do.

## Consequences

- Breaking changes (renames, removed settings, tightened nullability, reshaped contracts) are expected and need no
  migration path until the first stable release, and `PublicAPI.Unshipped.txt` churn is routine.
- The diff of `PublicAPI.*.txt` makes every surface change impossible to miss in review, and the build enforces that
  the files stay in sync with the code, so reviewing a pull request includes reviewing that diff.
- Feature work cites Auth0 as the baseline and justifies each deliberate deviation as "parity-plus", a concrete control
  Auth0 lacks, added at low complexity, which keeps divergence intentional rather than accidental.
- Reviewers evaluate a breaking change by design quality alone, not by compatibility cost.
- `AuditRecordWriter.SchemaVersion` is `"1.0"`, and [`docs/audit-records.md`](../audit-records.md) documents the `1.0`
  record with `tenant_environment_type`.
- `EventIds.cs` in `NCode.Identity.Events` and in `NCode.Identity.OpenId.Authentication` list the assigned IDs only;
  an ID of a removed event is unassigned and free.
- A reviewer rejects a version bump or an identifier reservation made for a pre-release state.
- At the first stable release the latitude of this record ends for the shipped surface, its version numbers and its
  identifiers, and `docs/api-compatibility.md` governs.

## References

- [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)
- [ADR-1023](1023-scopes-and-resources-management-model.md)
- [#535](https://github.com/NCodeGroup/identity/issues/535)
- [`repository.instructions.md`](../../.github/instructions/repository.instructions.md)
- [`docs/api-compatibility.md`](../api-compatibility.md)
- [`Directory.Build.targets`](../../Directory.Build.targets)
- [`AuditRecordWriter`](../../src/NCode.Identity.Events.Abstractions/Audit/AuditRecordWriter.cs)
- [`NCode.Identity.Events` event IDs](../../src/NCode.Identity.Events/Logging/EventIds.cs)
- [`NCode.Identity.OpenId.Authentication` event IDs](../../src/NCode.Identity.OpenId.Authentication/Logging/EventIds.cs)

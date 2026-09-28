# 5. The public API surface is tracked and evolves backward-compatibly

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

These are shipped libraries. An accidental change to the **public API surface** — a renamed method, a removed overload,
a changed signature, a flipped nullability — is a breaking change for every consumer, and it is trivially easy to make
one without noticing. "Reviewer vigilance" does not scale.

## Decision

**The public surface is explicit, diff-visible, and guarded by an analyzer; once GA it evolves additively.**

- Every packable project carries [`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
  (wired for all shipping projects in [`Directory.Build.targets`](../../Directory.Build.targets)) and a sibling
  `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` pair. A public symbol absent from both files fails the build
  (`RS0016`); a listed symbol that no longer exists fails (`RS0017`). With warnings-as-errors, the surface cannot drift
  without a deliberate, reviewable edit to those files. Pre-1.0, `Shipped.txt` stays empty and the whole surface lives
  in `Unshipped.txt`, so it is diff-visible while we remain free to break it before the first stable release; at the
  first stable release the surface moves into `Shipped.txt`.
- **Once GA, a shipped surface evolves backward-compatibly:** additive-only, new overloads over changed signatures,
  required inputs as constructor parameters but optional inputs as `init` properties (never a new constructor
  parameter), appended (never renumbered) wire-enum members, and `[Obsolete]`-then-remove across a major version. A
  breaking change is a **major** version bump.

## Options considered

- **Rely on code review.** Rejected: humans miss surface changes, especially nullability and overload removals.
- **Track the surface with the analyzer (chosen).** The diff of `PublicAPI.*.txt` makes every surface change impossible
  to miss in review, and the build enforces that the files stay in sync with reality.

## Consequences

- The first enablement is bulk work (every existing public member must be listed via the analyzer's "Add all items to
  the public API" fixer); after that, adding API is a small, obvious diff.
- Reviewing a PR includes reviewing the `PublicAPI.*.txt` diff — the surface change is front and center.

## References

- [`Directory.Build.targets`](../../Directory.Build.targets), [ADR-0003](0003-central-package-management-and-lockstep-versioning.md).

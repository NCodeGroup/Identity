# 4. No cross-assembly InternalsVisibleTo in production

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

The family is split into many packages that collaborate at runtime. It is tempting to let one package reach another's
`internal` types via `InternalsVisibleTo` to avoid designing a public seam. That coupling is invisible to consumers,
untracked by the public-API guard, and turns every internal refactor of one package into a potential break of another.

## Decision

**Packages collaborate only through deliberately-designed public contracts — never through `InternalsVisibleTo`.**
`InternalsVisibleTo` is reserved for **test projects** (the matching `*.Tests` assembly) plus `DynamicProxyGenAssembly2`
so Moq/Castle can proxy `internal` types and `internal virtual` members. A type that must be public **only** for
cross-assembly collaboration (never for application code) is marked `[EditorBrowsable(EditorBrowsableState.Never)]`
with a `<summary>` that opens _"This API supports the NCode.Identity infrastructure and is not intended to be used
directly from your code."_ — the EF Core / ASP.NET Core pattern.

## Options considered

- **Cross-assembly `InternalsVisibleTo`.** Rejected: hidden coupling, not tracked by `PublicApiAnalyzers`, and it makes
  the package boundary meaningless — a refactor safe within one assembly silently breaks another.
- **Merge the packages so everything is one assembly.** Rejected: the package split exists so consumers take only what
  they need (abstractions without implementations, one mechanism without another).
- **Public seams + infrastructure-public marking (chosen).** The collaboration surface is explicit, reviewable, and
  version-tracked; `[EditorBrowsable(Never)]` keeps it out of application-developer IntelliSense.

## Consequences

- Every inter-package interaction is a designed, documented, SemVer-tracked contract.
- A little more up-front design for genuinely internal collaboration types; the payoff is that internal refactors stay
  internal.

## References

- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §2.

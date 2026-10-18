# 127. Warnings are errors in every project, including tests

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group

## Context

[`Directory.Build.props`](../../Directory.Build.props) sets `TreatWarningsAsErrors=true` for the repository, but
[`tests/Directory.Build.props`](../../tests/Directory.Build.props) overrode it back to `false`, justified in a comment
as *"Test projects are never shipped: no XML documentation, and warnings must not fail the build."* Every build
therefore carried a standing pile of warnings that nobody read.

Three facts make that exemption indefensible:

1. **"Never shipped" is not the repository's actual criterion.** `NCode.Identity.OpenId.Playground`,
   `.Playground.Database`, `.Playground.MigrationService`, and `.Playground.AppHost` all set `IsPackable=false` and are
   equally never shipped — yet all of them inherit the gate and hold at zero warnings. `AppHost` suppresses exactly one
   rule (`NoWarn=ASPIRE010`) rather than dropping the gate wholesale. Test projects were the single exception, which
   makes the exemption an accident rather than a policy.
2. **Warnings in tests are disproportionately correctness defects.** `xunit` brings `xunit.analyzers` transitively, so
   the `xUnit1xxx`/`xUnit2xxx` rules run against test code: unused theory parameters, `null` passed to a value-type
   parameter, assertions that cannot fail. A silently-broken test stops protecting production code without any other
   signal — the worst class of warning to ignore, not the most tolerable.
3. **The noise floor was eroding a security gate.** `NU1901`–`NU1904` NuGet vulnerability advisories are deliberately
   warnings-locally / errors-in-CI (`WarningsNotAsErrors` when `CI` is unset). A build that always prints warnings
   trains everyone to stop reading them, including those.

The audit that forced the question found the real cost was trivial: 20 unique warnings across the whole solution, all
in `tests/` — 8 `CS8600`, 4 `CS8602`, 3 `CA1869`, 2 `CA2201`, and one each of `CS0105`, `CS8620`, and `CA5350`.

## Decision

**Hold every project in the repository to zero warnings.** The criterion is **"code we maintain"**, not "code we
ship". `tests/Directory.Build.props` no longer overrides `TreatWarningsAsErrors`; test projects inherit the root gate
like every other project.

The only sanctioned escape hatches, in order of preference:

1. **Fix the code.** This is the default, and it was the right answer for 16 of the 20 findings. Nullable `out`
   arguments annotated `[MaybeNullWhen(false)]` get a **nullable local**; a maybe-null result is narrowed with
   `Assert.NotNull(x)` before dereference; `AuthenticateResult.Fail(new Exception(…))` becomes a specific exception
   type. One such fix also removed a `null!`.
2. **A per-rule entry in the `[tests/**/*.cs]` block of [`.editorconfig`](../../.editorconfig), with a one-line
   justification.** This is for rules that are legitimate on shipping code but genuine noise in tests — e.g. `CA1869`
   (cache `JsonSerializerOptions`): `JsonSerializerOptions` becomes immutable on first use, so a fresh instance per
   test is required for isolation and per-test converter setup, and the caching win is meaningless in a test.
3. **A tight `#pragma warning disable/restore` with a reason**, for a single isolated site where the rule should stay
   live everywhere else — e.g. the one `CA5350` on `SHA1.HashData`, where the test cross-validates the service's SHA-1
   support against the framework and therefore *must* name the weak algorithm.

🚫 Never reintroduce `TreatWarningsAsErrors=false`, and never use a project-level `NoWarn` as a general pressure valve.

### `IDE0005` is enforced solution-wide

Unused `using` directives rot identically in both trees, so `IDE0005` is raised to `warning` under `[*.cs]` rather than
being disabled for tests as it previously was. Two Roslyn quirks make this non-obvious and are recorded here so the
configuration is not "simplified" back into breakage:

- `IDE0005` is **hidden by default** under `AnalysisLevel` `latest-recommended`, so warnings-as-errors never saw it
  until it was raised explicitly.
- Roslyn only evaluates `IDE0005` when `GenerateDocumentationFile` is `true`, and **hard-errors** with
  `EnableGenerateDocumentationFile` if the rule is raised while that property is `false`
  ([dotnet/roslyn#41640](https://github.com/dotnet/roslyn/issues/41640)). Test projects previously set it to `false`.
  They now set it to `true` and suppress `CS1591` (missing XML comment on a public member) instead: the generated XML
  is discarded because test assemblies are never packed, and the trade buys `IDE0005` coverage over the test tree.

### Analyzers that remain deliberately off for tests

Three analyzers stay gated on `IsPackable != false` in [`Directory.Build.targets`](../../Directory.Build.targets).
This asymmetry is a decision, not an oversight:

- **`Microsoft.CodeAnalysis.BannedApiAnalyzers` (`RS0030`)** — tests legitimately use banned APIs as *cross-validation
  oracles*. `NCode.Identity.Jose.Tests` references the third-party `jose-jwt` package precisely to check our output
  against an independent implementation; banning it would defeat the test strategy. (Oracles carry their own
  correctness obligation — see [ADR-0126](0126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md).)
- **`Nullable.Extended.Analyzer` (`NX000x`, the `!` ban)** — tests need `null!` to exercise guard clauses. Production
  code remains bound by the ban.
- **`Microsoft.CodeAnalysis.PublicApiAnalyzers`** — not applicable; test assemblies have no tracked public surface.

## Consequences

- A new warning anywhere in the repository breaks the build immediately, so `NU190x` advisories and `xunit.analyzers`
  findings surface against a clean baseline instead of being lost in noise.
- Relaxing a rule now requires naming the rule and writing down why, which keeps the exception list auditable — and
  reviewable — rather than implicit in a disabled gate.
- Enabling `IDE0005` removed unused `using` directives from 182 files in a single mechanical pass
  (`dotnet format style --diagnostics IDE0005`). That command is the maintenance tool for the rule; CSharpier must be
  re-run afterwards, since the formatter and CSharpier can disagree about the resulting layout.
- Test assemblies now emit an XML documentation file. It is build output only — never packed, never published.

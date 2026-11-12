# 1127. Every project builds with zero warnings, and the family's own banned APIs bind shipping code

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0100](project-template/0100-warnings-are-errors-in-every-project.md), [project-template ADR-0102](project-template/0102-disallowed-apis-are-banned-at-build-time.md)

## Context

[project-template ADR-0100](project-template/0100-warnings-are-errors-in-every-project.md) holds every project, test
projects included, to zero warnings, and
[project-template ADR-0102](project-template/0102-disallowed-apis-are-banned-at-build-time.md) bans disallowed APIs at
build time. This record states how the family applies both: which remedies it uses for a warning, which analyzers stay
off for tests, and which banned-API lists bind which projects.

The criterion for the zero-warning gate is code the family maintains, not code it ships. Test projects are never
published, but they are maintained code, and the xUnit analyzers report real defects in tests. A standing pile of
warnings that nobody reads hides the next real one.

The family also has rules of its own that a template list cannot carry: the legacy `JwtSecurityTokenHandler` and
`JwtSecurityToken` types and the throwing Unix-time conversions of `DateTimeOffset` have no place in shipping code. Tests
legitimately use independent implementations as cross-validation oracles, so a ban on such types cannot bind them.

## Decision

**Every project in the repository, test projects included, holds to zero warnings, and the family's own banned-API list
binds the projects that ship.**

1. **No project opts out of the gate.** `tests/Directory.Build.props` never sets `TreatWarningsAsErrors` back to
   `false`; test projects inherit the root gate like every other project.
2. **The family applies project-template ADR-0100's remedies in order.**
   - Fix the code first: a nullable local for an `out` argument annotated `[MaybeNullWhen(false)]`, `Assert.NotNull`
     before a dereference, a specific exception type instead of `new Exception(…)`.
   - Then a per-rule entry with a one-line reason in the `[tests/**/*.cs]` block of
     [`.editorconfig`](../../.editorconfig).
   - Then a tight `#pragma warning disable` and `restore` pair with a reason at one site, such as the `CA5350` on
     `SHA1.HashData` in a test that cross-validates SHA-1 support against the framework.
3. **The `[tests/**/*.cs]` block turns off five rules, each with its reason in the file.** `CA1707` (test method names
   use the `Method_State_Expected` idiom), `CA1816` (test base classes have no finalizer), `CA1859` (tests hold
   collaborators through their interface), `CA1861` (tests pass inline array literals to assertion helpers) and
   `CA1869`. `CA1869` is off because `JsonSerializerOptions` becomes immutable on first use, so each test builds its own
   instance for isolation and per-test converters; the caching win is irrelevant in a test.
4. **`IDE0005` is a warning in every C# file.** `[*.cs]` in `.editorconfig` raises it, as the template's global
   configuration does. Roslyn evaluates the rule only when `GenerateDocumentationFile` is `true` and hard-errors when the
   rule is raised while that property is `false`
   ([dotnet/roslyn#41640](https://github.com/dotnet/roslyn/issues/41640)). `tests/Directory.Build.props` therefore
   generates documentation and suppresses `CS1591` (missing XML comment on a public member); the XML is discarded
   because test assemblies are never packed.
5. **`dotnet format style --diagnostics IDE0005` is the maintenance tool for unused usings.** CSharpier runs after it,
   because the two can disagree on the resulting layout.
6. **The family's root `BannedSymbols.txt` binds shipping code.** Beyond the template's `build/BannedSymbols.txt`, which
   binds every project, it bans the legacy `JwtSecurityTokenHandler` and `JwtSecurityToken` and the throwing
   `DateTimeOffset.FromUnixTimeSeconds` and `FromUnixTimeMilliseconds`. The replacement is
   `NCode.Identity.Json.DateCoercion` or `JsonElement.TryGetNumericDate`. `Directory.Build.targets` adds the file only
   where `IsPackable` is not `false`.
7. **That list stays off test projects.** Tests use independent implementations as cross-validation oracles, such as the
   `jose-jwt` package that `NCode.Identity.Jose.Tests` references, and banning them would defeat the test strategy. The
   banned-API analyzer itself runs in every project, so `BannedSymbols.Encoding.txt`, added to every project, binds
   tests too ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
   `BannedSymbols.Buffers.txt` binds every project that is not a test project, hosts and benchmark projects included,
   and leaves the test projects free of it
   ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
8. **`Microsoft.CodeAnalysis.PublicApiAnalyzers` runs only where `IsPackable` is not `false`.** Test assemblies have no
   tracked public surface. The null-forgiving operator ban is
   [project-template ADR-0118](project-template/0118-the-null-forgiving-operator-is-banned-outside-tests.md)'s, and it
   exempts `*.Tests` projects.

## Options considered

- **Exempt test projects from the gate because they are never shipped.** Rejected: "never shipped" is not the
  repository's criterion, which is code the family maintains. The xUnit analyzers report real defects in tests, and the
  warnings that accumulate under an exemption go unread.
- **Suppress rules wholesale for tests instead of fixing the code.** Rejected: fixing the code is the default remedy, and
  a rule is turned off for tests only when it is legitimate on shipping code and noise in a test, with its reason
  recorded beside the entry.
- **Apply the family's banned-API list to test projects too.** Rejected: tests use banned types as independent oracles,
  so the ban would defeat the cross-validation they exist for.
- **Hold every project to zero warnings, bind shipping code with the family's list, and keep the strict-encoding list on
  everything.** Chosen.

## Consequences

- A warning anywhere fails the build, so a new warning is fixed or justified in the change that introduces it.
- Test assemblies emit an XML documentation file. It is build output only, never packed and never published.
- Each rule turned off for tests carries a one-line reason in `.editorconfig`, so a reader sees the exception and its
  cause together.
- A test can reference a banned type as an oracle without a suppression, while shipping code cannot use the legacy JWT
  handler or the throwing Unix-time conversions.
- Unused usings are removed by one mechanical command, followed by CSharpier.

## References

- [`Directory.Build.props`](../../Directory.Build.props), [`tests/Directory.Build.props`](../../tests/Directory.Build.props)
  and [`Directory.Build.targets`](../../Directory.Build.targets)
- [`.editorconfig`](../../.editorconfig), the `[*.cs]` and `[tests/**/*.cs]` blocks
- [`BannedSymbols.txt`](../../BannedSymbols.txt) and [`BannedSymbols.Encoding.txt`](../../BannedSymbols.Encoding.txt)
- [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)
- [project-template ADR-0100](project-template/0100-warnings-are-errors-in-every-project.md),
  [project-template ADR-0102](project-template/0102-disallowed-apis-are-banned-at-build-time.md) and
  [project-template ADR-0118](project-template/0118-the-null-forgiving-operator-is-banned-outside-tests.md)
- [dotnet/roslyn#41640](https://github.com/dotnet/roslyn/issues/41640)

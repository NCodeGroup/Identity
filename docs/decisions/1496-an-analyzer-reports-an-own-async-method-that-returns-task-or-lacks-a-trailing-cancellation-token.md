# 1496. An analyzer reports an own async method that returns Task or lacks a trailing cancellation token

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

The C# conventions in `.github/instructions/csharp-production.instructions.md` §1 have an async method return
`ValueTask` or `ValueTask<T>`, end in `Async` and take a `CancellationToken` as its last parameter. The repository's own
analyzer project, `NCode.Identity.Analyzers`
([ADR-1486](1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md)),
enforces the return and token shape in every project, production and tests alike. The forces:

- **Review is not a gate.** `.editorconfig`, `BannedSymbols.txt` and a text gate cannot tell a method the family writes
  from one a framework fixes, so none of them can report the first and allow the second.
- **Task is allowed only at a framework contract.** An xUnit test method returns `Task`, an ASP.NET Core middleware
  `InvokeAsync` returns `Task`, and an override or implementation of a member that a package outside the family declares
  has the shape that package declares. A method the family declares has the family's shape.
- **The family's assemblies share one prefix.** `NCode.Identity` and `NCode.Identity.*` are built from this repository,
  while `NCode.Mediator` carries the same `NCode.` prefix and is not. A project reference reaches the compiler as
  metadata, so the compiler cannot tell a family member from a foreign one by where its symbol came from.
- **A method group can reach the framework after its declaration.** A method that `MapGet` takes as a delegate has the
  delegate's shape, and the call that hands it over can sit in another file than the method.
- **Some work must not be cancelled.** Work that follows a committed change
  ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)) runs on its own
  bounded token, so no caller can pass a token that drops it. Work a decision makes uncancellable runs on
  `CancellationToken.None`, because a caller's abort must not drop it
  ([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md),
  [ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)).

## Decision

**`NCode.Identity.Analyzers` fails the build in every project, production and tests alike, with `NCID0002` on an own
method that returns `Task` or `Task<T>` and with `NCID0003` on an own asynchronous method whose last parameter is not a
`CancellationToken`.**

1. **Two diagnostics report two shapes.** `NCID0002` reports a method that returns `Task` or `Task<T>`. `NCID0003`
   reports a method that returns `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`, or is `async`, and has no
   `CancellationToken` as its last parameter. Both are errors in the Design category and carry the `CompilationEnd`
   tag, because a method group that reaches an ASP.NET Core API can follow the declaration. They are separate so one
   pass fixes a method that fails both, and a framework-fixed `Task` return can be exempt without the token rule.
2. **The analyzer skips the shapes a framework fixes:**
   - constructors, accessors, lambdas and local functions, which are not ordinary methods;
   - entry points, `Main` and `<Main>$`;
   - the dispose pattern's `DisposeAsyncCore()`;
   - xUnit test methods, whose attribute derives from `Xunit.FactAttribute`;
   - an ASP.NET Core middleware's `Invoke` or `InvokeAsync(HttpContext, ...)`;
   - an override or implementation, implicit or explicit, of a member that an assembly outside the family declares;
   - a method group handed to a method, constructor or delegate type that a `Microsoft.AspNetCore.*` assembly declares.

   Generated code is not analyzed.
3. **The family is identified by assembly name.** It is `NCode.Identity`, `NCode.Identity.*` and the assembly being
   compiled. A member declared in the family is the family's own, so its implementer has the family's shape.
   `NCode.Mediator` shares the `NCode.` prefix but is foreign, so an implementation of its handler contracts keeps the
   shape it declares, and a new foreign package needs no change.
4. **A method that follows a committed change suppresses `NCID0003` with a justification.** The post-commit types and
   wrappers carry `[SuppressMessage("Design", "NCID0003", Justification = ...)]` at type or method level, citing the
   post-commit rule of the records above: the work follows a committed change and runs on its own bounded token through
   `PostCommitWork`. The same attribute marks a method a decision makes uncancellable, whose whole awaited work runs on
   `CancellationToken.None`. Such a method takes no token, because a parameter it ignores would misstate its
   cancellation. Nothing else suppresses the rules: no marker attribute, no `#pragma` and no `.editorconfig` severity.
5. **A shared stored task is not a method.** A `Task<T>` that several callers await is exposed as a property, such as
   `KeyAccessClientEntry.Creation`, or kept private, created with `.AsTask()` from the `ValueTask` that makes it.
6. **Tests follow the rule.** A test project's own async helpers return `ValueTask` and take the token last, and an
   xUnit test method keeps `async Task`.
7. **Endpoint handlers are converted, not exempted.** The endpoint handlers the family writes are `ValueTask` handlers
   that take the token, and the mapping passes `httpContext.RequestAborted`. Only a method group handed directly to an
   ASP.NET Core API is exempt, because the framework fixes that delegate's shape.
8. **The tests compile snippets with Roslyn** against `TRUSTED_PLATFORM_ASSEMBLIES`, as the tests of `NCID0001` do, so
   no test downloads a reference assembly.

## Options considered

- **A rule for production only.** Rejected: tests are our async code too, and a test project that follows another shape
  teaches the wrong one.
- **A marker attribute for the post-commit exception.** Rejected: it adds a public type to a package, and
  `[SuppressMessage]` with a justification already records the reason where the method is.
- **Exempting every method with an `HttpContext` parameter.** Rejected: the handlers convert cleanly to a `ValueTask`
  that takes the token, so an exemption would keep a shape the rule is meant to remove.
- **Matching framework contracts by namespace.** Rejected: the `NCode.*` prefix is shared, so a namespace test either
  exempts a member the family declares or reports one a foreign package fixes.
- **A warning, or an IDE-only rule.** Rejected: the rule must fail the build, because the build keeps it.
- **Two error diagnostics that skip framework-fixed shapes and accept a justified suppression.** Chosen: it reports
  every own method that breaks the convention and nothing a framework fixes.

## Consequences

- The rule is tool-enforced: `csharp-production.instructions.md` §1 and `csharp-testing.instructions.md` mark it so.
- Every own async method of `src/`, of `hosts/` and of every test project returns `ValueTask` or `ValueTask<T>` and
  takes the token last, except where a framework contract or a suppression with a justification fixes the shape.
- A family method handed to an API that takes a `Task` calls `.AsTask()`, and a `ValueTask` is awaited exactly once.
- A change to the analyzer reaches every project, so every test project is affected by it.

## References

- [#743](https://github.com/NCodeGroup/identity/issues/743)
- [#744](https://github.com/NCodeGroup/identity/issues/744)
- [ADR-1486](1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md):
  the analyzer project
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md): the post-commit
  rule that the justifications cite
- [ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md) and
  [ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md): the uncancellable work that
  the justifications cite
- `analyzers/NCode.Identity.Analyzers/AsyncMethodShapeAnalyzer.cs`

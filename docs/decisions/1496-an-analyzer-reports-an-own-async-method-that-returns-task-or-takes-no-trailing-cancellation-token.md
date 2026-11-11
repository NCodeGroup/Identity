# 1496. An analyzer reports an own async method that returns Task or takes no trailing cancellation token

- **Status:** Accepted
- **Refines:** [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md) (the same analyzer project)
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

The template's Language and Async rule, in `.github/instructions/csharp-production.instructions.md` §1, has an async
method return `ValueTask` or `ValueTask<T>`, end in `Async` and take a `CancellationToken` as its last parameter. Review
alone kept the rule, and the integration test project's conversion
([#740](https://github.com/NCodeGroup/Identity/issues/740)) found the same shape in every test project: helpers that
return `Task` and take no token, beside the production code that follows the rule. The forces:

- **Review is the only gate.** `.editorconfig`, `BannedSymbols.txt` and a text gate cannot tell a method the family
  writes from one a framework fixes, so none of them can report the first and allow the second.
- **Task is allowed only at a framework contract.** An xUnit test method returns `Task`, an ASP.NET Core middleware
  `InvokeAsync` returns `Task`, and an override or implementation of a member that a package outside the family
  declares has the shape that package declares. A method the family declares has the family's shape.
- **The family's assemblies share one prefix.** `NCode.Identity` and `NCode.Identity.*` are built from this repository, while `NCode.Mediator`
  carries the same `NCode.` prefix and is not. A project reference reaches the compiler as metadata, so the compiler
  cannot tell a family member from a foreign one by where its symbol came from.
- **A method group can reach the framework after its declaration.** A method that `MapGet` takes as a delegate has the
  delegate's shape, and the call that hands it over can sit in another file than the method.
- **Some work must not be cancelled.** Work that follows a committed change
  ([ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md),
  [ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md),
  [ADR-1468](1468-a-security-effect-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)) runs on
  its own bounded token, so no caller can pass a token that drops it. Work an ADR makes uncancellable runs on
  `CancellationToken.None`, because a caller's abort must not drop it
  ([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md),
  [ADR-1308](1308-a-refresh-lease-is-released-only-by-the-owner-token-its-winner-holds.md)).

## Decision

**`NCode.Identity.Analyzers` fails the build in every project, production and tests alike, with `NCID0002` on an own
method that returns `Task` or `Task<T>` and with `NCID0003` on an own asynchronous method whose last parameter is not a
`CancellationToken`.**

1. **Two diagnostics report two shapes.** `NCID0002` reports a method that returns `Task` or `Task<T>`. `NCID0003`
   reports a method that returns `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`, or is `async`, and has no
   `CancellationToken` as its last parameter. They are separate so one pass fixes a method that fails both, and a
   framework-fixed `Task` return can be suppressed without the token rule. Both are in the Design category and carry the
   `CompilationEnd` tag, because a method group that reaches an ASP.NET Core API can follow the declaration.
2. **The analyzer skips the shapes a framework fixes:**
   - constructors, accessors, lambdas and local functions, which are not ordinary methods;
   - entry points, `Main` and `<Main>$`;
   - the dispose pattern's `DisposeAsyncCore()`;
   - xUnit test methods, whose attribute derives from `Xunit.FactAttribute`;
   - an ASP.NET Core middleware's `Invoke` or `InvokeAsync(HttpContext, ...)`;
   - an override or implementation, implicit or explicit, of a member that an assembly outside the family declares;
   - a method group handed to a method, constructor or delegate type that a `Microsoft.AspNetCore.*` assembly declares.

   Generated code is not analyzed. An explicit interface implementation is checked like any other method, so it is
   skipped only when the member it implements is declared outside the family.
3. **The family test is by assembly name.** The family is `NCode.Identity` and `NCode.Identity.*` and the assembly being compiled. A member declared in the
   family is the family's own, so its implementer has the family's shape. `NCode.Mediator` is outside the family, so an
   implementation of its handler contracts keeps the shape it declares.
4. **A method that follows a committed change suppresses `NCID0003` with a justification.** The post-commit types and
   wrappers carry `[SuppressMessage("Design", "NCID0003", Justification = "...")]` at type or method level, citing
   ADR-1351, ADR-1464 and ADR-1468: the work follows a committed change and runs on its own bounded token through
   `PostCommitWork`. The same attribute marks a method an ADR makes uncancellable (ADR-1291, ADR-1293, ADR-1308), whose
   whole awaited work runs on `CancellationToken.None`. Such a method takes no token, because a parameter it ignores
   would misstate its cancellation. Nothing else suppresses the rule: no marker attribute, no `#pragma` and no
   `.editorconfig` severity.
5. **A shared stored task is not a method.** A `Task<T>` that several callers await is exposed as a property, such as
   `KeyAccessClientEntry.Creation`, or kept private, with `.AsTask()` where the `ValueTask` that creates it is turned
   into the stored task.
6. **Both diagnostics are errors in every project, tests included.** No `.editorconfig` entry lowers them, and the tests
   are not a separate tier: a test project's own async helpers return `ValueTask` and take the token last, and an xUnit
   test method keeps `async Task`.
7. **Endpoint handlers are converted, not exempted.** A handler the family writes as `Task Handle(HttpContext)` and
   calls from a lambda is a `ValueTask` handler that takes the token, and the mapping passes `httpContext.RequestAborted`.
   Only a method group handed to an ASP.NET Core API is exempt, because the framework fixes that delegate's shape.
8. **The tests compile snippets with Roslyn,** as the tests of `NCID0001` do, against the test host's
   `TRUSTED_PLATFORM_ASSEMBLIES`, so no test downloads a reference assembly.

## Options considered

- **A `Task`/`ValueTask` rule only for production.** Rejected: the rule covers tests, whose helpers are async methods of
  ours, and a test project that follows another shape teaches the wrong one.
- **A marker attribute for the post-commit exception.** Rejected: it adds a public type to a package, and
  `[SuppressMessage]` with a justification that cites the ADR already records the reason where the method is.
- **Exempting every method with an `HttpContext` parameter.** Rejected: the handlers convert cleanly to a `ValueTask`
  that takes the token, so an exemption would keep a shape the rule is meant to remove.
- **Matching framework contracts by namespace instead of assembly name.** Rejected: the family's own `NCode.*`
  assemblies share the prefix, so a namespace test either exempts a member the family declares or reports one a
  foreign package fixes.
- **A warning, or an IDE-only rule.** Rejected: the rule must fail the build, because the build, not review, keeps it.

## Consequences

- The rule is tool-enforced: `.github/instructions/csharp-production.instructions.md` §1 and
  `.github/instructions/csharp-testing.instructions.md` mark it ✅.
- Every own async method of `src/` and of every test project returns `ValueTask` or `ValueTask<T>` and takes the token
  last, except where a framework contract or a suppression with an ADR-citing justification fixes the shape.
- A method that the family declares and a caller hands to an API that takes a `Task` calls `.AsTask()`, and a
  `ValueTask` is awaited exactly once.
- A change to the analyzer reaches every project, so every test project is affected by it.
- A new foreign package whose member a method implements needs no change: the family test names the family, so every
  other assembly is foreign.

## References

- [#743](https://github.com/NCodeGroup/Identity/issues/743)
- [#744](https://github.com/NCodeGroup/Identity/issues/744)
- [#740](https://github.com/NCodeGroup/Identity/issues/740), the issue of PR #741: the integration test project's
  conversion
- [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md): the analyzer project
- [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md)
- [ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md)
- [ADR-1468](1468-a-security-effect-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)
- `src/NCode.Identity.Analyzers/AsyncMethodShapeAnalyzer.cs`

# 1262. Minimal-API request delegates are generated at compile time

- **Status:** Accepted
- **Date:** 2026-10-25
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0120](project-template/0120-request-delegates-are-generated-at-compile-time.md)

## Context

[project-template ADR-0120](project-template/0120-request-delegates-are-generated-at-compile-time.md) generates
minimal-API request delegates at compile time, and this record adds the family's measurements, its opt-out and its
tracking issue.

The family maps about 170 minimal-API endpoints, as method groups, across about 10 packages. With runtime binding,
ASP.NET Core's `RequestDelegateFactory.Create` builds expression trees for each endpoint at every host start, and
`Expression.Compile` turns them into dynamic methods. Dynamic methods skip tiered compilation, so the JIT compiles every
one of them fully optimized before the endpoint answers its first request.

The integration suite starts about 400 test hosts, and each pays that cost again. Profiling with `dotnet-counters` and
stack samples puts the time in the JIT and in the factory's expression compilation, not in locks, garbage collection or
the thread pool. Production pays the same cost on every cold start and on every scale-out replica.

The Request Delegate Generator (RDG) has one behavioral gap. Its generated form binding (`TryResolveFormAsync` in
`dotnet/aspnetcore`'s `RequestDelegateGeneratorSources`) reads the form without checking
`IAntiforgeryValidationFeature`. A post whose antiforgery validation failed therefore answers 500, where the runtime
factory answers 400.

## Decision

**The family generates its request delegates at compile time, and a project with a form-bound endpoint opts out until
the generator checks antiforgery validation.**

1. **The generator is on family-wide.** `Directory.Build.props` sets `EnableRequestDelegateGenerator=true`, so every
   project that maps endpoints gets generated delegates without opting in.
2. **A project with a form-bound endpoint opts out.** An endpoint that binds `[FromForm]`, `IFormFile` or
   `IFormCollection` makes its project file set `EnableRequestDelegateGenerator=false`, with a comment that names the
   guard test, this record and issue [#186](https://github.com/NCodeGroup/identity/issues/186).
   `NCode.Identity.OpenId.Authentication.Interactive`, whose pages post antiforgery-protected forms, is that project.
3. **Every form endpoint has a guard test.** It posts without the antiforgery token and expects a 4xx response and no
   change, as `LocalAccountLifecycleTests.ChangePassword_WithoutTheAntiforgeryToken_ChangesNothing` does for the
   change-password form. Re-enabling the generator for such a project is safe only when those tests pass.

## Options considered

- **Lower the test hosts' logging to `Warning`.** It gains about 10% on a subset of the suite. Rejected: logging is not
  where the time goes.
- **Pre-generate signing keys for the test hosts.** Rejected: key generation is not the cost.
- **Generate the delegates at compile time (chosen).** It removes the expression-tree compilation from every host start,
  in tests and in production alike, for one build switch. Measured on one machine, the integration tests run in about
  41 s with generated delegates against about 6 minutes with runtime binding, and the local fast Definition of Done
  takes about 1.5 minutes.

## Consequences

- Host start and the integration suite skip the expression-tree compilation of about 170 endpoints.
- A form-bound endpoint costs its project the generator until upstream is fixed, and the guard test is what keeps the
  runtime factory's 400 answer observable.
- A new project that binds a form must set the opt-out, or its failed-antiforgery posts answer 500; the guard test
  fails in that case.
- Issue [#186](https://github.com/NCodeGroup/identity/issues/186) tracks re-enabling the generator for the Interactive
  package once `dotnet/aspnetcore`'s generated form binding checks `IAntiforgeryValidationFeature`.

## References

- [`Directory.Build.props`](../../Directory.Build.props) — the family-wide switch.
- [`NCode.Identity.OpenId.Authentication.Interactive.csproj`](../../src/NCode.Identity.OpenId.Authentication.Interactive/NCode.Identity.OpenId.Authentication.Interactive.csproj)
  — the opt-out.
- [`LocalAccountLifecycleTests`](../../tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/LocalAccountLifecycleTests.cs)
  — the guard test `ChangePassword_WithoutTheAntiforgeryToken_ChangesNothing`.
- Issue [#186](https://github.com/NCodeGroup/identity/issues/186).
- [ADR-1043](1043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) — the endpoint-group mechanism that
  maps the endpoints.
- `dotnet/aspnetcore` `release/10.0`: `RequestDelegateGeneratorSources` (`TryResolveFormAsync`) and
  `RequestDelegateFactory`.

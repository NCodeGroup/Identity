# 262. Minimal-API request delegates are generated at compile time

- **Status:** Accepted
- **Date:** 2026-10-25
- **Deciders:** NCode Group

## Context

The family maps about 170 minimal-API endpoints, as method groups, across 10 packages. With runtime binding, ASP.NET
Core's `RequestDelegateFactory.Create` builds expression trees for each endpoint at every host start, and
`Expression.Compile` turns them into dynamic methods. Dynamic methods skip tiered compilation, so the JIT compiles every
one of them fully optimized before the endpoint answers its first request.

The integration suite starts about 400 test hosts, and each pays that cost again:

- `dotnet-counters` shows about 32 s of thread time in the JIT per 2 s of wall time (about 2,000 methods compiled per
  2 s), with CPU use near 20% of 16 cores. Lock contention, garbage collection and thread-pool queueing are negligible.
- A stack sample finds 15 of 17 busy threads inside the factory's expression-compilation path.
- The 413 integration tests take 5m58s, and the local fast Definition of Done about 7 minutes.

Production pays the same cost on every cold start and every scale-out replica, before the host serves its first
request.

The Request Delegate Generator (RDG) is the ASP.NET Core source generator that emits the same request delegates at
compile time. It intercepts each `Map*` call with a C# interceptor, which works only in the project that makes the call,
so it is switched on per project. It emits a diagnostic for a handler shape it cannot generate.

RDG has one known behavioral gap. In `dotnet/aspnetcore` `release/10.0`, its generated form binding
(`TryResolveFormAsync` in `RequestDelegateGeneratorSources`) reads the form without checking
`IAntiforgeryValidationFeature`. For a request whose antiforgery validation failed, `FormFeature` then throws
`InvalidOperationException`, and the endpoint answers 500. The runtime factory checks the feature and answers 400.

## Decision

**Every project generates its minimal-API request delegates at compile time, except a project with form-bound
endpoints, which binds at runtime until the generated form binding honors antiforgery validation.**

1. **The generator is on family-wide.** `Directory.Build.props` sets `EnableRequestDelegateGenerator=true`, so every
   project that maps endpoints gets generated delegates without opting in.
2. **`NCode.Identity.OpenId.Authentication.Interactive` opts out.** Its pages post forms protected by antiforgery
   tokens, and under RDG a post whose token fails validation answers 500 instead of 400. Its project file sets
   `EnableRequestDelegateGenerator=false` and names the guard test and this ADR.
3. **A project with form-bound endpoints opts out.** An endpoint that binds `[FromForm]`, `IFormFile` or
   `IFormCollection` makes its project set `EnableRequestDelegateGenerator=false` until issue [#186](https://github.com/NCodeGroup/Identity/issues/186) re-enables the
   generator after upstream closes the gap. Every form endpoint has an integration test that posts without the
   antiforgery token and expects a 4xx response and no change, as
   `LocalAccountLifecycleTests.ChangePassword_WithoutTheAntiforgeryToken_ChangesNothing` does for the change-password
   form. Re-enabling the generator for such a project is safe only when those tests pass.
4. **An unsupported handler shape fails the build.** RDG's diagnostics are warnings, and the family treats warnings as
   errors, so a handler RDG cannot generate stops the build instead of silently falling back to runtime binding.

## Options considered

- **Keep runtime binding and share one test host across tests.** Rejected: a shared host loses the isolation each test
  gets from its own host, and it does nothing for a production cold start.
- **Lower the test hosts' logging to `Warning`.** Measured on a subset of the suite, it takes 58 s to 52 s, about 10%.
  Rejected: logging is not where the time goes.
- **Pre-generate a pool of signing keys for the test hosts.** Rejected: key generation is not the cost; the JIT is.
- **Generate the delegates at compile time (chosen).** It removes the expression-tree compilation from every host
  start, in tests and in production alike, for one build switch.

## Consequences

- The 413 integration tests run in 41 s instead of 5m58s on the same machine, and the local fast Definition of Done
  drops from about 7 minutes to about 1m36s.
- A host starts faster, because its endpoints no longer compile expression trees before the first request.
- A new endpoint whose handler shape RDG cannot generate fails the build; the fix is a supported shape, not a
  suppression.
- A new form-bound endpoint in a project that generates its delegates is the smell this decision guards against: move
  it, or opt the project out and add the no-token test.
- Issue [#186](https://github.com/NCodeGroup/Identity/issues/186) tracks re-enabling the generator for the Interactive package once `dotnet/aspnetcore`'s generated form
  binding checks `IAntiforgeryValidationFeature`.

## References

- [`Directory.Build.props`](../../Directory.Build.props) — the family-wide switch.
- [`NCode.Identity.OpenId.Authentication.Interactive.csproj`](../../src/NCode.Identity.OpenId.Authentication.Interactive/NCode.Identity.OpenId.Authentication.Interactive.csproj)
  — the opt-out.
- [`LocalAccountLifecycleTests`](../../tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/LocalAccountLifecycleTests.cs)
  — the guard test `ChangePassword_WithoutTheAntiforgeryToken_ChangesNothing`.
- Issue [#186](https://github.com/NCodeGroup/Identity/issues/186).
- [ADR-0043](0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) — the endpoint-group mechanism that maps the endpoints.
- `dotnet/aspnetcore` `release/10.0`: `RequestDelegateGeneratorSources` (`TryResolveFormAsync`) and
  `RequestDelegateFactory`.

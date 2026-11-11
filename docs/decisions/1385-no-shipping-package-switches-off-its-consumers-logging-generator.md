# 1385. No shipping package switches off its consumers' logging generator

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
- **Supersedes:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md), decision 1's `Microsoft.Extensions.Http.Resilience` for `NCode.Identity.OpenId.Authentication`
- **Amends:** [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md), decision 2's mechanism, and [ADR-1336](1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md), decision 6's transitive pins

## Context

`NCode.Identity.OpenId.Authentication` bounded its guarded outbound client with `AddResilienceHandler` from
`Microsoft.Extensions.Http.Resilience`. That package depends on `Microsoft.Extensions.Telemetry.Abstractions`, whose
`buildTransitive` targets set `DisableMicrosoftExtensionsLoggingSourceGenerator=true` in every project that consumes it,
which removes the `Microsoft.Extensions.Logging.Generators` analyzer and adds `Microsoft.Gen.Logging`. The two
generators emit a different logged-state type for `[LoggerMessage]`. The repository's own fix, a
`Directory.Build.targets` override, was not packed, so every consumer of `NCode.Identity.OpenId.Authentication` or
`NCode.Identity.Server` had the logging code of its own projects changed
([#471](https://github.com/NCodeGroup/identity/issues/471)).

## Decision

1. **The guarded client runs on `Polly.Core`.** Every call goes through `GuardedResilienceHandler` over
   `GuardedResiliencePipelines`: the total timeout, then a jittered exponential retry, then a per-attempt timeout. A
   connection or name-resolution error is never retried, because the guard's refusal is deterministic. `Retry-After` is
   ignored. Only a GET or HEAD without a body is retried; any other request still gets both timeouts, so a POST delivery
   stays bounded by `AttemptTimeout` and is never sent twice. A timeout surfaces as a `TaskCanceledException` with a
   `TimeoutException` inner exception, as `HttpClient.Timeout` produces. Both pipelines read time from the registered
   `TimeProvider`. The limits and options are those of
   [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md).

2. **`NCode.Identity.OpenId.Authentication` references `Polly.Core` directly.** `Directory.Packages.props` pins no
   `Microsoft.Extensions.Http.Resilience`, `Polly.Extensions` or `Polly.RateLimiting`, because no project references
   them.

3. **The build rejects a replacement of the logging generator.** The `RejectLoggingGeneratorReplacement` target of
   `Directory.Build.targets` fails the build of every packable project that carries a
   `Microsoft.Extensions.Telemetry.Abstractions` analyzer or sets `DisableMicrosoftExtensionsLoggingSourceGenerator`. No
   target turns the generator back on after a swap; the build fails instead. A test project may still get the package
   from `Microsoft.Extensions.Diagnostics.Testing`; it declares no `[LoggerMessage]`.

## Options considered

- **A `buildTransitive/<PackageId>.targets` packed into the package.** Rejected: it rewrites the analyzer set of every
  consumer, and depends on the private target names of another package.
- **Documenting the workaround in the package README.** Rejected: every consumer would have to apply it, and the defect
  would stay.
- **Keeping `AddResilienceHandler`.** Rejected: the dependency is what causes the swap.

## Consequences

- Consumers keep their own `[LoggerMessage]` generator.
- The guarded client emits no `Polly` meter or resilience-event logs, as the protocol client emits none.
- The server retries no OPTIONS or TRACE request and no GET with a body; it sends none.
- `NCode.Identity.OpenId.Authentication` and `NCode.Identity.Server` bring none of
  `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http.Diagnostics`,
  `Microsoft.Extensions.Telemetry`, `Microsoft.Extensions.Telemetry.Abstractions`, `Polly.Extensions` or
  `Polly.RateLimiting` to a consumer, which references any of them it uses itself.
- A new shipping dependency that brings `Microsoft.Extensions.Telemetry.Abstractions` fails the build instead of
  reaching the consumers.

## References

- [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md),
  [ADR-1336](1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md)
- Issue [#471](https://github.com/NCodeGroup/identity/issues/471)
- `GuardedResilienceHandler.cs` and `GuardedResiliencePipelines.cs` in `NCode.Identity.OpenId.Authentication`, and
  `Directory.Build.targets`

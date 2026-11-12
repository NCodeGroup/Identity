# 1385. No shipping package replaces its consumers' logging generator or carries Microsoft.Extensions.Http.Resilience

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md), decision 1, for every shipping package

## Context

`Microsoft.Extensions.Http.Resilience` depends on `Microsoft.Extensions.Telemetry.Abstractions`, whose `buildTransitive`
targets set `DisableMicrosoftExtensionsLoggingSourceGenerator=true` in every project that consumes it. That removes the
`Microsoft.Extensions.Logging.Generators` analyzer and adds `Microsoft.Gen.Logging`. The two generators emit a different
logged-state type for `[LoggerMessage]`, so a package that carries the dependency changes the logging code of every
consumer's own projects ([#471](https://github.com/NCodeGroup/identity/issues/471)). No shipping package may do that to
its consumers. A fix inside the repository, such as a `Directory.Build.targets` override, is not packed, so it protects
no consumer.

## Decision

Project-template ADR-0145's decision 1 names `Microsoft.Extensions.Http.Resilience` for bounding outbound calls. For
every shipping package the family bounds them with `Polly.Core` alone, and the build rejects a package that replaces its
consumers' logging generator.

1. **The guarded client runs on `Polly.Core`.** `NCode.Identity.OpenId.Authentication` references it directly. Every
   call of the guarded outbound client goes through `GuardedResilienceHandler` over `GuardedResiliencePipelines`: the
   total timeout, then a jittered exponential retry, then a per-attempt timeout. Which requests retry, what a
   connection error does, `Retry-After` and the exception a timeout throws are those of
   [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md). A timeout surfaces as a
   `TaskCanceledException` with a `TimeoutException` inner exception, as `HttpClient.Timeout` produces, and both
   pipelines read time from the registered `TimeProvider`.

2. **`NCode.Identity.OpenId.Client` also references `Polly.Core` directly.** `Directory.Packages.props` pins no
   `Microsoft.Extensions.Http.Resilience`, `Polly.Extensions` or `Polly.RateLimiting`, because no project references
   them.

3. **The build rejects a replacement of the logging generator.** The `RejectLoggingGeneratorReplacement` target of
   `Directory.Build.targets` fails the build of every packable project that carries a
   `Microsoft.Extensions.Telemetry.Abstractions` analyzer or sets `DisableMicrosoftExtensionsLoggingSourceGenerator`. No
   target turns the generator back on after a swap; the build fails instead. A new shipping dependency that brings the
   package therefore fails the build instead of reaching consumers.

4. **A test project may still get the package** through `Microsoft.Extensions.Diagnostics.Testing`, because it declares
   no `[LoggerMessage]`.

## Options considered

- **A `buildTransitive/<PackageId>.targets` packed into the package.** Rejected: it rewrites the analyzer set of every
  consumer, and depends on the private target names of another package.
- **Documenting a workaround in the package README.** Rejected: every consumer would have to apply it, and the defect
  would stay.
- **Keeping `AddResilienceHandler`.** Rejected: the dependency is what causes the swap.
- **`Polly.Core` referenced directly, with a build check.** Chosen: the pipelines need nothing the extension packages
  add, and the check keeps the defect from returning.

## Consequences

- Consumers keep their own `[LoggerMessage]` generator.
- The guarded client emits no Polly meter or resilience-event logs.
- `NCode.Identity.OpenId.Authentication` and `NCode.Identity.Server` bring none of
  `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http.Diagnostics`,
  `Microsoft.Extensions.Telemetry`, `Microsoft.Extensions.Telemetry.Abstractions`, `Polly.Extensions` or
  `Polly.RateLimiting` to a consumer, which references any of them it uses itself.
- A shipping dependency that would swap the generator is found at build time, not by a consumer.

## References

- [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md),
  [ADR-1336](1336-the-protocol-client-bounds-every-call-with-a-polly-core-pipeline-and-retries-only-safe-requests.md)
- [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
- Issue [#471](https://github.com/NCodeGroup/identity/issues/471)
- `GuardedResilienceHandler.cs` and `GuardedResiliencePipelines.cs` in `NCode.Identity.OpenId.Authentication`,
  `ProviderResiliencePipelines.cs` in `NCode.Identity.OpenId.Client`, and `Directory.Build.targets`

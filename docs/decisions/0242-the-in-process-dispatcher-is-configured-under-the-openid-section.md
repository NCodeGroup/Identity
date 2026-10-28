# 242. The in-process dispatcher is configured under the OpenID section

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

`AddIdentityServer()` composes the in-process dispatcher
([ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md),
[ADR-0232](0232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md)) without configuration, so
`InProcessJobDispatcherOptions` (`QueueCapacity`, `FullMode`, `MaxConcurrentJobs`) is set only in code, and the
reference host offers no key. An operator cannot size the queue or the concurrency of the deliveries without a code
change (issue [#294](https://github.com/NCodeGroup/Identity/issues/294)).

The configuration binder reads an enum from a number as well as from a name, and it accepts a number that no member has:
`"7"` binds `BoundedChannelFullMode` to a value that `BoundedChannelOptions` refuses when the queue is built.

## Decision

**The options of the in-process dispatcher bind strictly from `OpenId:InProcessJobDispatcher` and are validated when
the host starts.**

1. **The subsection lives under the OpenID section.** `AddConfiguration` recognizes `InProcessJobDispatcher` among the
   subsections of the OpenID section, so a misspelled subsection stops the host and a host's own section name moves it.
   It binds `InProcessJobDispatcherOptions` with `BindStrictly` and `ValidateOnStart`
   ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)).
   `InProcessJobDispatcherOptions.SectionName`, the constant `"InProcessJobDispatcher"`, names the subsection, as the
   retention options name theirs
   ([ADR-0221](0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md)). The keys are
   `QueueCapacity`, `FullMode` and `MaxConcurrentJobs`, and an environment variable sets one as
   `OpenId__InProcessJobDispatcher__MaxConcurrentJobs`.
2. **Validation names the key and refuses an undefined `FullMode`.** `InProcessJobDispatcherOptionsValidator` names
   each failing option and its configuration key, relative to the OpenID section. For example, a queue capacity of zero
   fails with:

   ```text
   InProcessJobDispatcherOptions.QueueCapacity ('InProcessJobDispatcher:QueueCapacity') must be at least 1; it is 0.
   ```

   A `FullMode` that names no member of `BoundedChannelFullMode` fails with the names of the members. A name that no
   member has stops the binding, and a number that no member has (`"7"`) fails the validator. A number that a member
   has (`"1"`) is accepted, as the binder accepts it.
3. **The defaults are the library's.** `appsettings*.json` and the Helm chart carry no key for the dispatcher: the
   defaults are a queue of 1024 jobs, 16 concurrent jobs and `Wait`. The host's files carry no tuning key of the other
   options under the OpenID section (`GrantRetention`, `TenantUsage`) either, and the chart models only the settings
   that it validates and passes the others through `extraEnv`.

## Options considered

- **A top-level section.** Rejected: the server composes the dispatcher and only its own jobs run on it. Under the
  OpenID section a misspelled subsection stops the host and a host's section name moves it, as
  [ADR-0221](0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md) reasoned for the
  retention options.
- **A binding in the dispatch package.** Rejected: the server owns the shape of the OpenID section
  ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)).
- **Keys in `appsettings.json` and the chart.** Rejected: the defaults are the library's, and the host's files and the
  chart carry no tuning key of the other options.

## Consequences

- An operator tunes the queue and the concurrency without code.
- A misspelled key or an invalid value stops the host when it starts and names the key.
- A `Configure<InProcessJobDispatcherOptions>` that a host registers after `AddConfiguration` runs after the binding
  and wins.
- This implements issue [#294](https://github.com/NCodeGroup/Identity/issues/294).

## References

- [ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md) — the job-dispatch adapter and the in-process
  dispatcher.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — configuration binds strictly and is
  validated when the host starts.
- [ADR-0221](0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md) — the options that
  `AddConfiguration` binds beneath the OpenID section.
- [ADR-0232](0232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md) — the options of the in-process
  dispatcher and their validation.
- Code: `NCode.Identity.Dispatch.Options.InProcessJobDispatcherOptions`,
  `NCode.Identity.Dispatch.InProcess.InProcessJobDispatcherOptionsValidator`,
  `NCode.Identity.Server.IdentityServerBuilderExtensions`.

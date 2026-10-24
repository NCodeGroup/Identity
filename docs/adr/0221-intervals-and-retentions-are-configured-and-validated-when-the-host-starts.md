# 221. Intervals and retentions are configured and validated when the host starts

- **Status:** Accepted (amended by
  [ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md): `OpenId:EndedSessionTermination`;
  and by [ADR-0242](0242-the-in-process-dispatcher-is-configured-under-the-openid-section.md):
  `OpenId:InProcessJobDispatcher`)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The intervals and the lifetimes that the server and the tenants run timers on accepted zero and negative values:
`OpenIdServerOptions.SettingsPeriodicRefreshInterval`, `SecretsPeriodicRefreshInterval` and
`ReconciliationLeaseDuration`, and `OpenIdTenantOptions.SettingsPeriodicRefreshInterval`,
`SecretsPeriodicRefreshInterval` and `TenantCacheLifetime`. A refresh interval that was not positive stopped a poll, a
lease duration that was not positive kept the reconciliation from holding its lease
([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)), and a cache lifetime that
was not positive failed the first write to the tenant cache (backlog `R86`).

`TenantUsageOptions` was not validated, so a zero flush interval faulted its hosted service after the host started.
Neither `GrantRetentionOptions` nor `TenantUsageOptions` bound to configuration, so an operator could not set the
retention or the interval of either (backlog `R83`).

## Decision

**A period that a timer runs on is validated when the host starts, against the range that every .NET timer accepts, and
the retention options bind from configuration.**

1. **A timer period is between 1 millisecond and 4,294,967,294 milliseconds.** One millisecond is the shortest period a
   `PeriodicTimer` accepts, and 4,294,967,294 milliseconds, about 49.7 days, is the longest that any .NET timer or
   `CancellationTokenSource` delay accepts. `NCode.Time.TimerPeriod` states the range as `MinValue`, `MaxValue`, and
   `IsValid`. The six `OpenIdOptions` values above, `GrantRetentionOptions.PruneInterval`,
   `TenantUsageOptions.FlushInterval`, and `EndedSessionTerminationOptions.Interval`
   ([ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)) must be valid periods. The
   tenant cache lifetime is held to the same range, so one rule covers every one of them.
2. **A retention is not negative.** `GrantRetentionOptions.Retention` and `TenantUsageOptions.Retention` accept zero,
   which removes a row as soon as it has ended, and refuse a negative value.
3. **One validator checks each options type.** `OpenIdIntervalsOptionsValidator`, `GrantRetentionOptionsValidator`,
   `TenantUsageOptionsValidator`, and `EndedSessionTerminationOptionsValidator` implement `IValidateOptions<T>`. Each
   is registered with `ValidateOnStart` beside the services that read the options, and has no constructor
   dependencies, because a validator that reads options deadlocks the host as it starts. A failure names the option
   and its configuration key, relative to the OpenID section, and lists every failing option. For example, a negative
   grant retention fails with:

   ```text
   GrantRetentionOptions.Retention ('GrantRetention:Retention') must not be negative; it is -1.00:00:00.
   ```
4. **`AddConfiguration` binds the retention options.** `OpenId:GrantRetention` binds `GrantRetentionOptions` and
   `OpenId:TenantUsage` binds `TenantUsageOptions`, each with `BindStrictly` and `ValidateOnStart`
   ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)). `OpenId:EndedSessionTermination`
   binds `EndedSessionTerminationOptions` the same way
   ([ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)), and
   `OpenId:InProcessJobDispatcher` binds `InProcessJobDispatcherOptions` the same way
   ([ADR-0242](0242-the-in-process-dispatcher-is-configured-under-the-openid-section.md)). Each options type has a
   `SectionName` constant, and the OpenID section recognizes eight subsections: `Server`, `Tenant`,
   `TenantResolution`, `MutualTls`, `GrantRetention`, `TenantUsage`, `EndedSessionTermination`, and
   `InProcessJobDispatcher`.

## Options considered

- **DataAnnotations `[Range]` on the options.** Rejected: the bounds are runtime facts of the .NET timers, and
  `TimerPeriod` defines them once in code.
- **Fail in the hosted service.** Rejected: the failure then comes after the host started and has accepted traffic,
  and it faults one background service while the rest of the host runs.
- **Top-level configuration sections for the retention options.** Rejected: the options configure the OpenID server, so
  they sit in the OpenID section beside `MutualTls`.

## Consequences

- A misconfigured interval, lifetime, or retention stops the host when it starts, and the error names the option and
  its configuration key.
- An operator sets the grant retention, the prune interval, the usage flush interval, and the usage retention through
  configuration, and a misspelled key stops the host.
- This implements backlog `R83` and `R86`.

## References

- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — configuration binds strictly and is
  validated when the host starts.
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger and the options that
  configure it.
- [ADR-0218](0218-ended-grants-are-removed-after-a-retention-period.md) — ended grants are removed after a retention
  period.
- [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — the reconciliation that
  holds a lease for `ReconciliationLeaseDuration`.
- Code: `NCode.Time.TimerPeriod`, `NCode.Identity.OpenId.OpenIdIntervalsOptionsValidator`,
  `NCode.Identity.OpenId.Authentication.Persistence.GrantRetentionOptionsValidator`,
  `NCode.Identity.OpenId.Authentication.Usage.TenantUsageOptionsValidator`,
  `NCode.Identity.Server.IdentityServerBuilderExtensions`.

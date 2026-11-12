# 1137. Configuration binds strictly, and every option, timer period and retention is validated at startup

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md), [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md)

## Context

[project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md) stops the
host on a configuration key that maps to no option or a value the application cannot honor: options bind strictly, each
section has one owner, and every options type is validated when the host starts.
[project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md) prunes data
that grows with use in bounded batches, on timers and with retentions that an operator configures. This record applies
both to the OpenID section and to the reference host.

The configuration binder ignores a key that maps to no property. A key that binds to nothing is indistinguishable from
an absent key, so a misspelling, a misplaced section and a key left behind by a refactor fail the same way: quietly, at
the moment it matters. A host that configures tenant selection under `OpenId:Tenant`, which binds how a resolved tenant
is materialized and cached rather than which tenant a request selects, starts, serves traffic and looks healthy while
its tenant configuration is discarded. It behaves only because the defaults happen to coincide with what it asked for.
An operator who copies that shape with a non-default value has the setting ignored with no signal at all.

A related class fails late instead of silently. A tenant-resolution configuration that names an unknown strategy, a
path-based route without the tenant parameter or an invalid host pattern is discovered by the first request that
resolves a tenant, after health checks have already reported the instance ready.

The values of timers fail in a third way. The refresh intervals, cache lifetimes and lease durations that timers and
caches run on accept zero and negative values unless something refuses them. A refresh interval that is not positive
stops a poll, a lease duration that is not positive keeps the reconciliation from holding its lease
([ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md)), a cache lifetime that is not
positive fails the first write to the tenant cache, and a zero flush interval faults a hosted service after the host has
started and accepted traffic. The retention and prune options of the data that grows with use need the same protection,
and an operator needs a configuration key for each.

## Decision

**Every options type the family binds from configuration binds strictly and is validated when the host starts, and a
period that a timer runs on is held to the range that every .NET timer accepts.**

### Strict binding and validation at startup

1. **One helper binds strictly.** Every options type the family binds from configuration uses `BindStrictly`
   (`NCode.Identity.Registration.StrictConfigurationBindingExtensions`), which sets the binder's
   `ErrorOnUnknownConfiguration` at every depth, and is registered with `ValidateOnStart`. A misspelled or misplaced key
   stops the host with an error that names the section, the options type and the key.
2. **Every section a package binds is covered.** Strict binding covers the OpenID section and every other section a
   package binds, among them `BootstrapAdmin`, `Frontend` (`FrontendBootstrapOptions`), `ConformanceSeed`,
   `DeveloperInteractiveSeed`, `InteractiveAuthentication`, `DeveloperKeys`, `Cors`, `ForwardedHeaders`,
   `StrictTransportSecurity`, `SecretProtection` and its key-provider subsections, `DataProtectionKeyRing`, `Valkey`,
   `Bff` and `ManagementConsole`.
3. **The OpenID section has one owner.** `AddConfiguration` (`IdentityServerBuilderExtensions`) owns the whole OpenID
   section and rejects any other subsection with `ThrowOnUnknownSubsection`, whose error lists the recognized ones. The
   section recognizes twelve subsections, and each options type has a `SectionName` constant:
   - `Platform` binds `OpenIdPlatformOptions`, whose `Settings` member carries the platform settings subsection as-is
     and is watched for reloads.
   - `Tenant` binds `OpenIdTenantOptions`, which configures how a resolved tenant is materialized and cached.
   - `Team` binds `OpenIdTeamOptions`.
   - `TenantResolution` binds `TenantResolutionOptions`, the one location of tenant selection.
   - `MutualTls` binds `MutualTlsForwardingOptions`.
   - `ClientCertificateRevocation`, `OutboundHttp`, `GrantRetention`, `TenantUsage`, `AuditRecords`,
     `EndedSessionTermination` and `InProcessJobDispatcher` bind the options of the same names.
4. **The subsections bind in `AddConfiguration`, not in the packages that read them.** A host's chosen section name
   therefore applies to all of them. An extension adds no keys under a section the family binds, and a custom tenant
   strategy binds its own options from its own section.
5. **Tenant resolution is validated before the host starts.** `TenantResolutionOptionsValidator` checks the selected
   strategy's options: a literal path for the static and host strategies, the tenant parameter in the path strategy's
   route, a compilable host pattern, and a static issuer that is an absolute `http` or `https` URL ending in the
   tenant path. An unknown strategy code fails when the endpoints are mapped, before the host starts.

### Timer periods and retentions

6. **A timer period is between 1 millisecond and 4,294,967,294 milliseconds.** One millisecond is the shortest period a
   `PeriodicTimer` accepts, and 4,294,967,294 milliseconds, about 49.7 days, is the longest that any .NET timer or
   `CancellationTokenSource` delay accepts. `NCode.Identity.Time.TimerPeriod` states the range once, as `MinValue`,
   `MaxValue` and `IsValid`.
7. **The OpenID intervals are held to that range.** `OpenIdIntervalsOptionsValidator` checks
   `Platform.SettingsPeriodicRefreshInterval`, `Tenant.SettingsPeriodicRefreshInterval`,
   `Tenant.SecretsPeriodicRefreshInterval`, `Tenant.TenantCacheLifetime`, `Team.SettingsPeriodicRefreshInterval`,
   `Team.TeamCacheLifetime` and `Team.ReconciliationLeaseDuration`. The cache lifetimes are held to the same range as the
   timers, so one rule covers every one of them.
8. **Other timer periods are held to it by their own validators.** Among them are `GrantRetentionOptionsValidator`,
   `TenantUsageOptionsValidator`, `EndedSessionTerminationOptionsValidator`, `AuditRecordOptionsValidator`,
   `TenantKeyRolloverOptionsValidator` and `MasterKeyOptionsValidator`.
9. **A retention is not negative.** `GrantRetentionOptions.Retention` and `TenantUsageOptions.Retention` accept zero,
   which removes a row as soon as it ends, and refuse a negative value.
10. **Each validator is an `IValidateOptions<T>` that lists every failure.** It is registered beside the services that
    read the options and takes no constructor dependencies, because a validator that reads options deadlocks the host as
    it starts. A failure names each failing option with its key relative to the OpenID section, for example:

    ```text
    GrantRetentionOptions.Retention ('GrantRetention:Retention') must not be negative; it is -1.00:00:00.
    ```

## Options considered

- **Bind `OpenId:Tenant` as an alias of `OpenId:TenantResolution`.** Rejected: `OpenId:Tenant` already means
  materialization, so the alias would give one key two meanings and leave every other misplaced key silent.
- **DataAnnotations `[Range]` on the options.** Rejected: the bounds are runtime facts of the .NET timers, and
  `TimerPeriod` defines them once in code.
- **Fail inside the hosted service after startup.** Rejected: the failure then comes after the host has started and
  accepted traffic, and it faults one background service while the rest of the host runs.
- **Top-level configuration sections for the retention options.** Rejected: the options configure the OpenID server, so
  they sit in the OpenID section beside `MutualTls`.
- **Strict binding, `ValidateOnStart` validators and `TimerPeriod` as the one definition of a timer's range.** Chosen: a
  misconfiguration stops the host before it serves a request, and the error names the key.

## Consequences

- A misspelled or misplaced key, a misconfigured interval, lifetime or retention, and an invalid tenant-resolution
  configuration stop the host when it starts, and the error names the section, the option and its configuration key.
- An operator sets the grant retention, the prune interval, the usage flush interval and the usage retention through
  configuration, and a misspelled key stops the host.
- A new options type is bound with `BindStrictly`, registered with `ValidateOnStart` and, when it holds a timer period,
  checked with `TimerPeriod.IsValid` by a validator of its own.
- Setting names under the platform settings subsection are a closed vocabulary validated with the rest of the
  configuration ([ADR-1139](1139-setting-names-are-a-closed-vocabulary.md)).

## References

- [project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md) and
  [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md) — the
  general rules this record applies.
- [ADR-1138](1138-protocol-endpoints-are-served-under-the-tenant-route.md) — the tenant route the validator checks.
- [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) — setting names and the remaining configuration inputs.
- [ADR-1160](1160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md) — each tenant strategy validates its
  own options.
- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger and the options that
  configure it.
- [ADR-1218](1218-ended-grants-are-removed-after-a-retention-period.md) — ended grants are removed after a retention
  period.
- [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — the reconciliation that holds
  a lease for `Team.ReconciliationLeaseDuration`.
- Code: `NCode.Identity.Server.IdentityServerBuilderExtensions.AddConfiguration`,
  `NCode.Identity.Registration.StrictConfigurationBindingExtensions`, `NCode.Identity.Time.TimerPeriod`,
  `NCode.Identity.OpenId.OpenIdIntervalsOptionsValidator`,
  `NCode.Identity.OpenId.Tenants.TenantResolutionOptionsValidator`,
  `NCode.Identity.OpenId.Authentication.Persistence.GrantRetentionOptionsValidator`,
  `NCode.Identity.OpenId.Authentication.Usage.TenantUsageOptionsValidator`.

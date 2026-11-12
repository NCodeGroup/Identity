# 1447. Forwarded-header trust and Strict-Transport-Security are general ASP.NET Core helpers each host composes

- **Status:** Accepted (amends
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md) decisions 1 and 6,
  [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) decision 1,
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) decision 6 and
  [ADR-1399](1399-failed-client-authentications-are-limited-per-remote-address-by-default.md))
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A host behind a TLS-terminating ingress sees plain HTTP to an internal address. Without trusted forwarded headers it
reads the wrong scheme and host, and without Strict-Transport-Security a browser does not pin HTTPS for the host's
origin. Four facts shape where the two helpers live:

- The management console host serves behind the same ingress as the OP host and must not reference
  `NCode.Identity.Server`.
- Neither helper names an identity concept, so both are general ASP.NET Core helpers.
- YARP, which a BFF uses for remote APIs, reads its routes from a `ReverseProxy` section by convention and registers
  itself with `AddReverseProxy()`.
- Whether a proxy terminates TLS is the deployment's to say, so a library that adds the helpers for every host would
  decide it for the host.

Issue [#611](https://github.com/NCodeGroup/identity/issues/611) describes the need.

## Decision

**`NCode.Identity.AspNetCore` holds forwarded-header trust and Strict-Transport-Security, and each host composes both.**

1. **`NCode.Identity.AspNetCore` holds both helpers.** It holds `AddTrustedForwardedHeaders(configuration)` and
   `UseTrustedForwardedHeaders()` with `TrustedForwardedHeadersOptions` (section `ForwardedHeaders`: `KnownProxies`,
   `KnownNetworks`, `ForwardLimit`), and `AddStrictTransportSecurity(configuration)` and
   `UseStrictTransportSecurity()` with `StrictTransportSecurityOptions` (section `StrictTransportSecurity`). The options
   live in namespace `NCode.Identity.AspNetCore.Options`. `ForwardedHeaders` is the section that holds the forwarded-header trust
   which ADR-1137, ADR-1139, ADR-1258 and ADR-1399 name, and `TrustedForwardedHeadersOptionsValidator` applies the rules
   of ADR-1139 decision 6 to it.
2. **Both sections bind strictly and validate on start.** `NCode.Identity.AspNetCore` references `NCode.Identity.Registration` for strict
   binding ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
3. **Each host composes both.** A host calls `UseTrustedForwardedHeaders()` and then `UseStrictTransportSecurity()`
   first in its pipeline. No library adds them.
4. **The Helm chart sets the section.** The chart's `forwardedHeaders` values set `ForwardedHeaders__…`.

## Options considered

- **Keep them in `NCode.Identity.Server`.** Rejected: the management console host must not reference it.
- **A separate hosting package.** Rejected: `NCode.Identity.AspNetCore` is the package that
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) names for general ASP.NET Core
  helpers.
- **The `ReverseProxy` section name.** Rejected: YARP's routes live in that section, and strict binding of trust options
  from it would refuse YARP's keys.
- **Compose them inside `AddManagementConsole`.** Rejected: whether a proxy terminates TLS is the deployment's to say.

## Consequences

- The console host trusts its ingress through `ForwardedHeaders:KnownNetworks` and sends Strict-Transport-Security
  outside Development.
- A BFF host references `NCode.Identity.AspNetCore` to do the same.
- `NCode.Identity.AspNetCore` depends on `NCode.Buffers` and `NCode.Identity.Registration`.

## References

- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
  [ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)
- `src/NCode.Identity.AspNetCore/`
- Issue [#611](https://github.com/NCodeGroup/identity/issues/611)

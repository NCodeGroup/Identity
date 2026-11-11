# 1456. The hosts map their health probes through one general helper

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0128](project-template/0128-liveness-never-checks-dependencies-readiness-does.md)

## Context

Two hosts, `NCode.Identity.OpenId.Host` and `NCode.Identity.ManagementConsole.Host`, serve the same liveness and
readiness probes. A copy of the probe options in each host drifts from the other (issue
[#609](https://github.com/NCodeGroup/Identity/issues/609)). Four facts shape where the helper and the readiness tag live:

- The readiness tag must be reachable by a package that registers a check without ASP.NET Core, such as the Valkey
  adapter.
- `NCode.Identity.AspNetCore` names no identity concept
  ([ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md)).
- `NCode.Identity.AspNetCore` references `NCode.Identity.Registration`
  ([ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md)).
- The chart's probes and the AppHost's `WithHttpHealthCheck` call the same two paths.

## Decision

**Every host maps its probes through `MapHealthProbes()` in `NCode.Identity.AspNetCore`, and the readiness tag lives in
`NCode.Identity.Registration`.**

1. **`NCode.Identity.AspNetCore` holds `MapHealthProbes()` and `HealthProbeDefaults`.** Liveness at `/health/live` runs no check,
   and readiness at `/health/ready` runs the checks tagged `ready`. Both probes belong to one returned route group, so a
   host can add `RequireHost` to both.
2. **`HealthCheckTags` lives in `NCode.Identity.Registration`.** `HealthCheckTags.Readiness` is the tag that marks a check a
   replica cannot serve without.
3. **Every host calls `MapHealthProbes()` and builds no `HealthCheckOptions`.**

## Options considered

- **Inline copies in each host.** Rejected: the copies drift.
- **The tag stays in `NCode.Identity.Abstractions` and `NCode.Identity.AspNetCore` references it.** Rejected: `NCode.Identity.AspNetCore`
  names no identity concept ([ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md)).
- **The tag in `NCode.Identity.AspNetCore`.** Rejected: the Valkey adapter would take the ASP.NET Core shared framework to tag
  its check.
- **A tag parameter on `MapHealthProbes`.** Rejected: every caller passes the same constant.
- **A hosting package.** Rejected: it would hold one constant.

## Consequences

- A host makes one call to map both probes.
- A package tags a readiness check with `NCode.Identity.Registration.HealthCheckTags.Readiness`.
- The paths are the ones that the chart's probes and the AppHost's `WithHttpHealthCheck` call.

## References

- [ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md),
  [ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md)
- `src/NCode.Identity.AspNetCore/HealthProbeEndpointRouteBuilderExtensions.cs`,
  `src/NCode.Identity.AspNetCore/HealthProbeDefaults.cs`, `src/NCode.Identity.Registration/HealthCheckTags.cs`
- Issue [#609](https://github.com/NCodeGroup/Identity/issues/609)

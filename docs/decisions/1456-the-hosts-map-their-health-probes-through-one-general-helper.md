# 1456. The hosts map their health probes through one general helper

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0128](project-template/0128-liveness-never-checks-dependencies-readiness-does.md)

## Context

Two hosts, `NCode.Identity.OpenId.Host` and `NCode.Identity.ManagementConsole.Host`, serve the same liveness and
readiness probes. A copy of the probe options in each host would drift from the other. Four facts shape where the helper
and the readiness tag live:

- The readiness tag must be reachable by a package that registers a check without ASP.NET Core, such as the Valkey
  adapter.
- `NCode.Identity.AspNetCore` names no identity concept
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
- `NCode.Identity.AspNetCore` references `NCode.Identity.Registration`
  ([ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md)).
- The chart's probes and the AppHost's `WithHttpHealthCheck` call the same two paths.

## Decision

**Every host maps its probes through `MapHealthProbes()` in `NCode.Identity.AspNetCore`, and the readiness tag lives in
`NCode.Identity.Registration`.** The decision refines
[project-template ADR-0128](project-template/0128-liveness-never-checks-dependencies-readiness-does.md): `MapHealthProbes()`
and `HealthCheckTags.Readiness` implement its two endpoints and its `ready` tag.

1. **`NCode.Identity.AspNetCore` holds `MapHealthProbes()` and `HealthProbeDefaults`.** `MapHealthProbes()` is in
   `HealthProbeEndpointRouteBuilderExtensions`. `HealthProbeDefaults` holds `LivenessPath` (`/health/live`),
   `ReadinessPath` (`/health/ready`) and the endpoint names `health_live_endpoint` and `health_ready_endpoint`.
2. **Liveness runs no check and readiness runs the checks tagged `ready`.** Both probes belong to one returned route
   group, so a host can add `RequireHost` to both.
3. **`HealthCheckTags.Readiness` (`"ready"`) lives in `NCode.Identity.Registration`.** It marks a check a replica cannot
   serve without. A package without ASP.NET Core, such as the Valkey adapter, tags its check with it, and
   `NCode.Identity.AspNetCore` references `NCode.Identity.Registration` to read it.
4. **Each host calls `app.MapHealthProbes()` and builds no `HealthCheckOptions`.** `NCode.Identity.OpenId.Host` tags its
   `openiddb` database check `HealthCheckTags.Readiness`.
5. **The chart's probes and the AppHost's `WithHttpHealthCheck` call these same two paths.**

## Options considered

- **Inline copies in each host.** Rejected: the copies drift.
- **The tag in `NCode.Identity.Abstractions`, which `NCode.Identity.AspNetCore` references.** Rejected:
  `NCode.Identity.AspNetCore` names no identity concept
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
- **The tag in `NCode.Identity.AspNetCore`.** Rejected: the Valkey adapter would take the ASP.NET Core shared framework
  to tag its check.
- **A tag parameter on `MapHealthProbes`.** Rejected: every caller passes the same constant.
- **A hosting package.** Rejected: it would hold one constant.
- **One helper in `NCode.Identity.AspNetCore` and the tag in `NCode.Identity.Registration`.** Chosen: the hosts share one
  mapping, and any package can tag a readiness check.

## Consequences

- A host makes one call to map both probes.
- A package tags a readiness check with `NCode.Identity.Registration.HealthCheckTags.Readiness`.
- The paths are the ones that the chart's probes and the AppHost's `WithHttpHealthCheck` call, so the three cannot
  drift.

## References

- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md),
  [project-template ADR-0128](project-template/0128-liveness-never-checks-dependencies-readiness-does.md)
- `src/NCode.Identity.AspNetCore/HealthProbeEndpointRouteBuilderExtensions.cs`,
  `src/NCode.Identity.AspNetCore/HealthProbeDefaults.cs`, `src/NCode.Identity.Registration/HealthCheckTags.cs`

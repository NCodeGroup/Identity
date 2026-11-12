# 1433. A BFF host refuses to start when an endpoint's BFF metadata is incomplete

- **Status:** Accepted (amends
  [ADR-1381](1381-the-bff-serves-client-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
  decision 8; amended by
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md):
  the check also evaluates each endpoint's IBffEndpointStartupRequirement metadata; decision 4 amended by
  [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md):
  the check also runs when the frontends change)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A YARP route that names `BffTokenRequirement` is a remote API only when its endpoint carries the BFF's API metadata,
which `MapReverseProxy().AsBffRemoteApis()` adds
([ADR-1381](1381-the-bff-serves-client-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
decision 8). A route without that call fails closed: the token accessor serves a token only to an endpoint the BFF
guards, so the transform throws and YARP answers `502`. The failure surfaces on every call to the route and never at
start (issue [#522](https://github.com/NCodeGroup/identity/issues/522)).

Three more facts make a start-time check worth having:

- The BFF's cookie boundary holds only with `AddReverseProxy().AddBffTransforms()`. A YARP route mapped without it
  forwards the BFF's cookies to the upstream.
- A frontend name on an endpoint is a string
  ([ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)), so a misspelled name
  selects no frontend and the endpoint is never reached.
- A host that maps YARP routes names its conventions in code, and a route that names a frontend has to be limited to it
  with the same `AsBffRemoteApis()` convention.

An `IProxyConfigFilter` sees the route configuration and not the conventions that a host applies to the endpoints, so it
cannot tell that `AsBffRemoteApis()` is missing.

## Decision

**`BffEndpointStartupCheck` reads every endpoint of the host once the host has started and refuses to start when an
endpoint's BFF metadata is incomplete. A later change of the endpoints is re-checked and logged, and the request-time
refusals stay the backstop.**

1. **When it runs.** `BffEndpointStartupCheck` is an `IHostedLifecycleService` and checks at `StartedAsync`. DI's
   `EndpointDataSource` holds every endpoint only once the application's pipeline is built, which is after the hosted
   services have started, so an earlier hook sees an incomplete set.
2. **What it reads.** It reads the endpoints of DI's `EndpointDataSource`, the `BffFrontendCatalog` and the registered
   `ITransformProvider` instances, so it needs no route configuration.
3. **The four failures.** The check throws one `InvalidOperationException` that holds every failure, joined by a space.
   For an endpoint that carries YARP route metadata:
   - the host has no `BffTransformProvider`: `The YARP route '<id>' is mapped in a BFF host without AddBffTransforms(),
     which keeps the BFF's cookies from every upstream: call AddReverseProxy().AddBffTransforms().`
   - the route names `BffTokenRequirement` and its endpoint has no `BffApiAttribute`: `The YARP route '<id>' names
     BffTokenRequirement but its endpoint is not a BFF API: call MapReverseProxy().AsBffRemoteApis().`
   - the route names `BffFrontend` and its endpoint is not limited to that frontend: `The YARP route '<id>' names
     BffFrontend '<name>' but its endpoint is not limited to it: call MapReverseProxy().AsBffRemoteApis().`

   For any endpoint, each `BffFrontendAttribute` whose name the catalog lacks: `The endpoint '<display name>' names the
   BFF frontend '<name>', which Bff:Frontends does not configure.`
4. **After startup.** The check subscribes to the endpoint data source's change token. On a change, for example a
   dynamic `IProxyConfigProvider` that loads a new route, it checks the endpoints again and logs each failure at
   `Error` as event 19007 (`EventIds.EndpointCheckFailed`). A started host cannot be stopped by a later change, so the
   re-check only reports it. The subscription is disposed at `StoppingAsync` and on `Dispose`.
5. **The backstop.** The request-time refusals stay: the token accessor answers `502` for a route that names a
   requirement without the BFF's API metadata, and routing answers `404` for an endpoint limited to a frontend that the
   request does not select. The start-time check moves the failure earlier and does not replace them.

## Alternatives considered

- **An `IProxyConfigFilter`.** Rejected: a filter sees the route configuration and cannot see the conventions that the
  host applies to the endpoints, so it cannot tell whether `AsBffRemoteApis()` was called.
- **Marking routes automatically from `AddBffTransforms()`.** Rejected: YARP builds its endpoints only from the
  conventions of the host's `MapReverseProxy()` call, so the transforms have no hook that gives the endpoint BFF
  metadata. The host states the convention where it maps the routes.
- **Keeping only the `502`.** Rejected: the failure then surfaces on every call to the route and not when the host
  starts, and a misconfiguration that reaches production looks like an upstream outage.

## Consequences

- A host that is misconfigured fails at start, and the exception message names every failure at once.
- A dynamic provider's bad route is logged at `Error` and still answers `502`, because the host is already running.
- The check runs once at start and once per endpoint change, so it adds no work to a request.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.6
- [ADR-1381](1381-the-bff-serves-client-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md),
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)
- `src/NCode.Identity.Bff/Hosting/BffEndpointStartupCheck.cs`
- Issue [#522](https://github.com/NCodeGroup/identity/issues/522)

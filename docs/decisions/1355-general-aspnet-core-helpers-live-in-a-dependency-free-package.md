# 1355. General ASP.NET Core helpers live in a dependency-free package

- **Status:** Superseded by [ADR-1498](1498-every-package-names-one-capability-under-the-ncode-identity-prefix.md)
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

`NoStoreEndpointFilter`, `NoStoreEndpointConventionBuilderExtensions.WithNoStore()` and
`NoStoreResponseExtensions.SetNoStore()` ([ADR-1345](./1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md))
held method bodies in `NCode.Identity.OpenId.Authentication.Abstractions`. An `*.Abstractions` package holds contracts
and data only, so they did not belong there.

Their consumers are two packages:

- `NCode.Identity.OpenId.Authentication` uses them in the token endpoint, the authorization response writer, client
  registration, pushed authorization, device authorization and backchannel authentication.
- `NCode.Identity.OpenId.Management` uses them on its secret-revealing routes. It references no implementation package
  ([ADR-1016](./1016-implementation-packages-depend-only-on-abstractions.md)), so it cannot reference the
  authentication package.

The helpers need a home that both can reference.

## Decision

- **A new `NCode.AspNetCore` package holds general ASP.NET Core helpers**, in namespace `NCode.AspNetCore`. It
  references only the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`). It is the ASP.NET Core counterpart of
  `NCode.Json` and `NCode.Time` ([ADR-1188](./1188-general-time-helpers-live-in-a-dependency-free-package.md)), so any
  package in the family can reference it.
- **The three types move unchanged except the namespace.** `NoStoreEndpointFilter` stays `internal`.
- **The tests move to `NCode.AspNetCore.Tests`.**
- **The package holds general ASP.NET Core HTTP helpers that name no identity concept.**

## Options considered

- **Keep them in the Abstractions package.** Rejected. An Abstractions package holds contracts and data only, and these
  types hold logic.
- **Move them to `NCode.Registration.AspNetCore`.** Rejected. That package is endpoint-registration composition, not HTTP
  response semantics.
- **Move them to `NCode.Identity.OpenId.Authentication`.** Rejected.
  [ADR-1016](./1016-implementation-packages-depend-only-on-abstractions.md) forbids the management package from
  referencing it.
- **Keep a copy in each package.** Rejected. Two copies drift, and a drifted copy would leave a secret-revealing
  response cacheable.

## Consequences

- A consumer references `NCode.AspNetCore` and adds `using NCode.AspNetCore;`. The family is pre-release
  ([ADR-1025](./1025-pre-release-posture-and-auth0-parity-plus.md)), so the move is a clean cutover with no forwarding
  types.
- The family publishes one more package. Any later general ASP.NET Core helper goes to `NCode.AspNetCore` too.

## References

- `src/NCode.AspNetCore/`
- `tests/NCode.AspNetCore.Tests/`
- [ADR-1016](./1016-implementation-packages-depend-only-on-abstractions.md)
- [ADR-1188](./1188-general-time-helpers-live-in-a-dependency-free-package.md)
- [ADR-1345](./1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md)
- Issue [#492](https://github.com/NCodeGroup/Identity/issues/492)

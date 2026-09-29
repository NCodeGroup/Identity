# 0009. Endpoint families own their route group and cross-cutting endpoint filters

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** NCode.Identity maintainers

## Context

The composition root (`NCode.Identity`) maps identity endpoints through
`IIdentityEndpointRouteBuilder`, which fans out to a set of registered `IEndpointProvider`s. Each
family of endpoints (OpenID/OAuth, Management, …) contributes its own providers.

The OpenID family needs a cross-cutting behavior that Core cannot know about: any exception thrown
by an OpenID endpoint must be rendered as a standard OpenID error response (e.g. a `400` with an
`error` JSON body) via `IOpenIdExceptionHandler`, with an optional per-endpoint override supplied
through `IOpenIdEndpointExceptionHandlerMetadata`. Previously that handler mechanism was fully
built and registered but had **no invocation site**, so unhandled `OpenIdException`s escaped to the
framework and surfaced as `500`s (the developer exception page in Development).

The forces at play:

- **Layering.** `NCode.Identity` (Core) must not reference OpenID-specific types, so Core cannot
  install an OpenID exception filter or middleware itself.
- **Scoping.** The filter must apply only to OpenID endpoints, not to every identity endpoint.
- **Backward compatibility.** The existing public `AddEndpointProvider<T>()` / flat
  `IEndpointProvider` resolution must keep working for families that do not need a group.

## Decision

Introduce a **group provider** tier alongside the flat provider tier. Core defines
`IEndpointGroupProvider` (with `AddEndpointGroupProvider<T>()`); the root route builder maps every
registered `IEndpointGroupProvider` first, then every ungrouped `IEndpointProvider`.

Each endpoint family that needs its own conventions or cross-cutting filters ships its own
`IEndpointGroupProvider`, creates its own `RouteGroupBuilder` (via `MapGroup`), applies its
conventions/filters there, and maps its own child endpoints into that group. The family's child
endpoints register under a family-scoped marker (e.g. `IOpenIdEndpointProvider`) instead of the
Core `IEndpointProvider` collection, so they are mapped exactly once — by their group.

For OpenID specifically: `OpenIdEndpointGroupProvider` maps an empty-prefix group (preserving the
absolute `/oauth2/*` routes), installs `OpenIdExceptionEndpointFilter`, and maps the five OpenID
endpoints. The filter resolves the handler per request — an endpoint's own
`IOpenIdEndpointExceptionHandlerMetadata` takes precedence, otherwise the DI-registered
`IOpenIdExceptionHandler` — then returns `ReadOnlyEndpointDisposition.HttpResult`.
`DefaultOpenIdExceptionHandler` renders an `OpenIdException` using the error's own status code and
falls back to a `500 server_error` for everything else.

## Options considered

- **Core middleware / Core-installed filter.** Rejected: Core cannot reference OpenID types
  (ADR-0004 spirit — no leaking of family-specific concerns into the composition root), and a
  process-wide middleware would also wrap non-OpenID endpoints.
- **Attach the filter on every OpenID endpoint individually.** Rejected: repetitive, easy to forget
  on a new endpoint, and spreads the same wiring across five files.
- **Replace flat providers entirely with groups.** Rejected: needless churn and a breaking change
  to the public `AddEndpointProvider<T>()` surface for families that do not need a group (e.g.
  Management stays flat).

## Consequences

- Cross-cutting endpoint behavior for a family lives in exactly one place — its group provider.
- Adding an OpenID endpoint is a one-liner (`AddOpenIdEndpointProvider<T>()`); the group and filter
  apply automatically.
- Families opt in incrementally: Management remains a flat `IEndpointProvider` and can adopt a group
  later without Core changes.
- A new public surface (`IEndpointGroupProvider`, `AddEndpointGroupProvider<T>()`) is tracked in
  `PublicAPI.Unshipped.txt` per ADR-0005.
- Gotcha: `AddEndpointFilter` on a `RouteGroupBuilder` uses the two-type-parameter overload
  `AddEndpointFilter<TBuilder, TFilterType>()` (builder type first).

## References

- `src/NCode.Identity.Abstractions/Endpoints/IEndpointGroupProvider.cs`
- `src/NCode.Identity.Abstractions/Endpoints/EndpointProviderRegistration.cs`
- `src/NCode.Identity/Endpoints/DefaultIdentityEndpointRouteBuilder.cs`
- `src/NCode.Identity.OpenId.Authentication/Endpoints/OpenIdEndpointGroupProvider.cs`
- `src/NCode.Identity.OpenId.Authentication/Endpoints/OpenIdExceptionEndpointFilter.cs`
- `src/NCode.Identity.OpenId.Core/Exceptions/DefaultOpenIdExceptionHandler.cs`
- ADR-0004 (no cross-assembly leakage), ADR-0005 (public API surface)

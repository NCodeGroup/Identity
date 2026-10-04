# 43. The hierarchical endpoint-group mechanism is a cross-cutting package layered on the registration builder

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

The family already has a cross-cutting hierarchical registration primitive: `NCode.Registration`'s
`IServiceBuilder<TMarker>`. It is a thin wrapper over `IServiceCollection` plus a compile-time marker type;
`NewBuilder<T>()` creates a sibling builder over the same collection and registers the marker so the composition root can
verify cross-package dependencies (`VerifyIsRegistered<T>`). Its "hierarchy" is a registration-time organizational
device with no runtime materialization — at runtime there is one flat `IServiceCollection`.

Separately, the declarative route-group mechanism of [ADR-0042](0042-management-api-route-driven-tenant-scope.md) —
`IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`, and the route-tree builder that walks the registered
groups — is a **runtime** tree: groups are registered as keyed services and materialized at endpoint-mapping time into
nested `RouteGroupBuilder`s with prefixes, filters, and scopes. These types lived in `NCode.Identity.Abstractions`, a
domain package, even though nothing about them is identity-specific.

Two problems followed from that placement. First, `IEndpointGroupBuilder` exposed a bare `IServiceCollection`, making it
a **second, parallel registration surface** next to `IServiceBuilder` — so a group's `ConfigureServices` could register
simple services but not reach builder-level helpers (notably `AddMessageFactory<T>()`, defined on `IServiceBuilder`),
and endpoint families kept a **separate, disjoint** `IServiceBuilder` registration chain listing the same endpoints a
second time. Second, generic ASP.NET Core routing plumbing was welded to the identity domain, so it could not be reused
and its namespace misrepresented its purpose.

## Decision

**The hierarchical endpoint-group mechanism is its own cross-cutting package, `NCode.Registration.AspNetCore`, that
depends on `NCode.Registration` and the ASP.NET Core shared framework. `IEndpointGroupBuilder` _is_ an `IServiceBuilder`
(it inherits the interface) rather than merely holding an `IServiceCollection`, so the endpoint-group tree composes on
top of the registration builder instead of paralleling it.**

The two mechanisms now have clean, non-overlapping roles:

- `IServiceBuilder<TMarker>` (`NCode.Registration`) is the **registration primitive** — _how_ services are registered,
  with marker-scoped organization and cross-package dependency gating. It is pure DI with no ASP.NET Core dependency.
- `IEndpointGroup` / `IEndpointGroupBuilder` (`NCode.Registration.AspNetCore`) is the **runtime routing tree** — a node
  owns its prefix, shared route filters (`ConfigureRoutes`), and its contents (`ConfigureServices`): the endpoints and
  child groups it declares and the services those endpoints depend on. Because the builder is an `IServiceBuilder`, a
  group registers everything — including `AddMessageFactory<T>()` — in one place, colocated with the endpoint.

Consequently the parallel per-family registration chain is removed: each endpoint family's registration becomes an
`IEndpointGroupBuilder` extension that registers **its own** endpoint provider and services, and the group's
`ConfigureServices` simply invokes each family once. The same endpoint is never listed twice.

The package also owns the generic route-tree builder (`IEndpointTreeRouteBuilder` + its default implementation, formerly
`IIdentityEndpointRouteBuilder` in `NCode.Identity`), the `AddEndpointGroupRouting()` registration, and the
`MapEndpointGroups()` endpoint-route-builder extension (formerly `MapIdentityEndpoints`). The endpoint-execution
disposition types (`ReadOnlyEndpointDisposition`, `OperationDisposition<TError>`, `ReadOnlyOperationDisposition<TError>`)
move with it, as they are generic endpoint/operation results, not identity concepts.

## Options considered

- **Keep the mechanism in `NCode.Identity.Abstractions` and expose `IServiceCollection` on `IEndpointGroupBuilder`
  (status quo after the first colocation pass).** Rejected: it leaves a second registration surface parallel to
  `IServiceBuilder`, cannot reach builder-level helpers, keeps the disjoint per-family chain, and welds generic routing
  plumbing to the identity domain.
- **Collapse the endpoint tree into `IServiceBuilder` and drop `IEndpointGroup`.** Rejected: routing needs a runtime,
  string-keyed group tree (prefixes, filters, nested `RouteGroupBuilder`s) that compile-time markers cannot express — the
  two are genuinely different concepts, not duplicates.
- **Extract a cross-cutting `NCode.Registration.AspNetCore` package and make `IEndpointGroupBuilder : IServiceBuilder`
  (chosen).** The mechanisms compose with one registration vocabulary, the duplicate listing disappears, and the generic
  routing toolkit is reusable with an honest namespace.

## Consequences

- There is a single registration vocabulary. A group's `ConfigureServices` registers its endpoints, child groups, and
  their services (including message factories) through the one `IServiceBuilder` surface, colocated.
- `NCode.Registration` stays pure DI (no ASP.NET Core dependency); the ASP.NET Core routing concern lives in its own
  package that any `NCode.Registration` consumer can adopt.
- The public types moved namespace (`NCode.Identity.Endpoints` → `NCode.Registration.AspNetCore`) and three symbols were
  renamed (`IIdentityEndpointRouteBuilder` → `IEndpointTreeRouteBuilder`, `MapIdentityEndpoints` → `MapEndpointGroups`,
  and the route-builder default implementation). This is a breaking change to the pre-release surface
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- An implementation package that maps endpoints now references `NCode.Registration.AspNetCore` (directly or transitively
  via `NCode.Identity.Abstractions`), which is an abstractions-only, framework-level dependency consistent with
  [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md).

## References

- Code: `NCode.Registration.AspNetCore` (`IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`,
  `EndpointProviderRegistration`, `EndpointRouteBuilderExtensions`, `IEndpointTreeRouteBuilder`,
  `DefaultEndpointTreeRouteBuilder`, the disposition types); `NCode.Registration`'s `IServiceBuilder<TMarker>`;
  `OpenIdEndpointGroup` and the Authentication endpoint-family registrations; the `ManagementRootGroup` hierarchy.
- [ADR-0042](0042-management-api-route-driven-tenant-scope.md) — the declarative named-group mechanism this relocates
  and composes onto the registration builder.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — service vs strategy vs mediator registration shapes.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — implementations depend only on abstractions.

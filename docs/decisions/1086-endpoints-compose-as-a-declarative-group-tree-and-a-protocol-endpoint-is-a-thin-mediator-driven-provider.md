# 1086. Endpoints compose as a declarative group tree, and a protocol endpoint is a thin mediator-driven provider

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

Two questions shape every endpoint of the family: how endpoints are registered and mapped, and how one protocol endpoint
is built.

**Registration and mapping.** An endpoint family (OpenID, Management) needs its own route prefix, its own cross-cutting
filters and its own services. The OpenID family also needs a behavior that no shared package can know about: any
exception thrown by an OpenID endpoint is rendered as a standard OpenID error response, such as a `400` with an `error`
JSON body, through `IOpenIdExceptionHandler`, with an optional per-endpoint override supplied through
`IOpenIdEndpointExceptionHandlerMetadata`. The forces:

- **Layering.** The composition root and the shared routing code must not reference OpenID types, so neither can install
  an OpenID exception filter or middleware itself.
- **Scoping.** The filter applies only to OpenID endpoints, not to every endpoint of the host.
- **One registration vocabulary.** `NCode.Identity.Registration` offers `IServiceBuilder<TMarker>`, a thin wrapper over
  `IServiceCollection` plus a compile-time marker, whose hierarchy is an organizing device at registration time with no
  runtime form. Routing needs a runtime tree of prefixes, filters and nested `RouteGroupBuilder`s. If the group builder
  were a second registration surface that exposes a bare `IServiceCollection`, a group could not reach builder-level
  helpers such as `AddMessageFactory<T>()`, and each family would keep a disjoint registration chain that lists the
  same endpoints a second time.
- **Reuse.** Generic ASP.NET Core routing plumbing has nothing identity-specific in it, so it belongs in a package
  whose name says so.

**The shape of a protocol endpoint.** Every OAuth and OpenID Connect protocol endpoint performs the same orchestration:
reject the wrong verb or content type, authenticate the client, parse the request parameters, validate them, do the
protocol work, and render a result or an error. The family has the building blocks for doing this cohesively:
strongly typed messages (`OpenIdMessage<T>` with `KnownParameter`s and a static `Load`), the mediator (`ICommand`,
`ICommandHandler`, `ICommand<TResponse>`, `ICommandResponseHandler`,
[ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)),
self-registering endpoint families
([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)) and an exception
filter that renders any `IOpenIdError` thrown as an `OpenIdException`. The token, end-session and CIBA endpoints embody
the intended shape. An endpoint written without a recorded shape can instead parse the form by hand, validate inline
and inline the protocol work in the route handler, and every new endpoint would then re-decide its structure.

## Decision

**Endpoints compose as a declarative tree of named groups registered through the one `IServiceBuilder` vocabulary, and a
protocol endpoint is a thin `IEndpointProvider` that guards the verb and content type, authenticates the client, loads a
strongly typed `OpenIdMessage<T>` request, drives validation and every replaceable protocol step through mediator
commands, and renders the result.**

### The endpoint tree

`NCode.Identity.Registration.AspNetCore`, over `NCode.Identity.Registration` and the ASP.NET Core shared framework,
holds the endpoint tree and the endpoint-execution dispositions:

- the tree: `IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`, `IEndpointTreeRouteBuilder`, the
  `AddEndpointGroupRouting()` registration and the `MapEndpointGroups()` extension;
- the dispositions: `ReadOnlyEndpointDisposition`, `OperationDisposition<TError>` and
  `ReadOnlyOperationDisposition<TError>`.

`NCode.Identity.Registration` stays pure DI with no ASP.NET Core dependency.

An `IEndpointGroup` has:

- a `Name`;
- `GetPrefix(IServiceProvider)`, resolved when routes are mapped, so the prefix may come from configuration;
- `ConfigureRoutes(RouteGroupBuilder)` for filters and conventions;
- `ConfigureServices(IEndpointGroupBuilder)`, which registers the group's endpoints (`AddEndpoint<T>()`), its child
  groups (`AddGroup<T>()`) and their services.

`IEndpointGroupBuilder` is an `IServiceBuilder`. A group therefore registers everything, `AddMessageFactory<T>()`
included, in one place beside the endpoint. Each family's registration is an `IEndpointGroupBuilder` extension that its
group invokes once, so the same endpoint is never listed twice.

`AddEndpointGroup<TGroup>()` registers a root group and its subtree: endpoints are keyed by their group's name and
children by their parent's. The tree route builder creates each group with its prefix, runs `ConfigureRoutes`, maps the
group's providers and descends into its children. `AddEndpointProvider<T>()` registers an ungrouped provider, which is
mapped onto the root after the groups.

### Cross-cutting filters live on the family's group

- `OpenIdEndpointGroup`, prefixed by the tenant route
  ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)), installs the
  request-environment filter and `OpenIdExceptionEndpointFilter` beside its metrics, interaction-id and rate-limit
  filters.
- `ManagementRootGroup` installs the management filters.
- `OpenIdExceptionEndpointFilter` renders a thrown error through the endpoint's `IOpenIdEndpointExceptionHandlerMetadata`
  when it has one, and through the registered `IOpenIdExceptionHandler` otherwise. `DefaultOpenIdExceptionHandler`
  renders an `OpenIdException` with its error's status code (`400` when the error has none) and anything else as
  `500 server_error`.
- On a `RouteGroupBuilder`, add a filter with `AddEndpointFilter<TBuilder, TFilter>()`, builder type first.

### The shape of a protocol endpoint

- **A thin endpoint.** `Map` wires `.WithName`, `.WithTags`, `.Produces`, `.DisableAntiforgery()` and
  `.WithOpenIdDiscoverable()`. `HandleRouteAsync` only orchestrates: it guards verb and content type, authenticates the
  client through `IClientAuthenticationService`, loads the request, dispatches through `openIdContext.Mediator` and
  renders. It holds no ad-hoc form parsing, no inline validation and no protocol work, and depends on as little as it
  can, ideally only `IClientAuthenticationService`.
- **A strongly typed request.** The request is `IFooRequest : IOpenIdRequest` plus
  `FooRequest : OpenIdMessage<FooRequest>, IFooRequest`, each parameter a `KnownParameter` on `OpenIdCommonParameters`.
  It is loaded with `FooRequest.Load(environment, form)` and registered with `AddMessageFactory<FooRequest>()`.
- **Validation is a mediator command.** `ValidateFooRequestCommand : ICommand` is handled by
  `DefaultValidateFooRequestHandler` at `DefaultMediatorPriorities.High`, which throws `….AsException()` on any
  violation for the exception filter to render. More validators compose through `TryAddEnumerable`.
- **Each replaceable step is a mediator command with a disposition.** `DoFooCommand : ICommand<FooDisposition>` has an
  `ICommandResponseHandler`. A capability seam that a deployment supplies, such as resolving a subject, issuing a grant
  or delivering a notification, ships a default that reports unsupported (such as `AuthenticateSubjectDisposition`,
  whose default returns an error the endpoint renders), and a capability package replaces it with `TryAddSingleton`.
- **Transports are services.** A side-effecting transport (`IBackChannelLogoutNotifier`, `ICibaNotificationService`) is
  an injectable service called from a handler, so the decision stays in the pipeline and the delivery in a replaceable
  service
  ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)).

The exemplars are the token, end-session, CIBA backchannel
([ADR-1085](1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md)) and device-authorization endpoints. The device-authorization endpoint is
`DeviceAuthorizationRequest : OpenIdMessage<…>` plus `ValidateDeviceAuthorizationRequestCommand` and
`CreateDeviceAuthorizationRequestCommand`.

## Options considered

- **A Core-installed middleware or filter for OpenID errors, or a filter on each endpoint.** Rejected. The shared code
  cannot reference OpenID types, and a process-wide middleware would also wrap non-OpenID endpoints. A per-endpoint
  filter repeats the wiring in every file and is easy to forget on a new endpoint.
- **Collapse the endpoint tree into `IServiceBuilder`.** Rejected. Routing needs a runtime, string-keyed tree of
  prefixes, filters and nested `RouteGroupBuilder`s, which compile-time markers cannot express. The two are different
  concepts, not duplicates.
- **Put the routing in `NCode.Identity.Abstractions` over a bare `IServiceCollection`.** Rejected. It leaves a second
  registration surface parallel to `IServiceBuilder`, cannot reach builder-level helpers, keeps the disjoint per-family
  chain and welds generic routing plumbing to the identity domain.
- **A template-method base endpoint that encodes the steps.** Rejected. The steps vary (some endpoints have no client
  authentication, some render redirects, some render JSON), and a rigid base class fights the minimal-API model and the
  per-endpoint OpenAPI metadata. A convention over the existing primitives, message and mediator, composes better.
- **Use the mediator for validation only and keep the protocol work in the endpoint.** Rejected. It leaves the endpoint
  fat and the persistence and issuance logic untestable without the HTTP pipeline. A processing tail behind an
  `ICommandResponseHandler` makes each step independently testable and replaceable.
- **Parse the request ad hoc for "simple" endpoints.** Rejected. Simple endpoints grow parameters, as the device flow and
  CIBA show, and a `KnownParameter`-backed message gives typed access, consistent parsing and serialization,
  extensibility and OpenAPI metadata.
- **A declarative group tree in its own cross-cutting package, with `IEndpointGroupBuilder : IServiceBuilder`, and a
  recorded thin-provider shape (chosen).** The mechanisms compose with one registration vocabulary, the duplicate listing
  disappears, the routing toolkit is reusable under an honest namespace, and every protocol endpoint has one structure.

## Consequences

- A family's cross-cutting endpoint behavior lives in exactly one place, its group. Adding an OpenID endpoint is
  one `AddEndpoint<T>()` call in the group's `ConfigureServices`, and the group's filters apply automatically.
- There is one registration vocabulary: a group registers its endpoints, child groups and services, message factories
  included, through the one `IServiceBuilder` surface.
- Any consumer of `NCode.Identity.Registration` can adopt the routing package, and an implementation package that maps
  endpoints references it.
- New protocol endpoints have one shape, reviewers have a concrete rule to hold them to, and every protocol step is an
  independently testable and replaceable mediator handler.
- Capability seams are uniform: a default that reports unsupported, and a capability package that replaces it, so the
  core ships protocol-correct behavior and deployment-specific behavior plugs in.
- The shape is enforced by convention and review, not the compiler; the
  [production C# instructions](../../.github/instructions/csharp-production.instructions.md) carry the rule.

## References

- Code: `NCode.Identity.Registration.AspNetCore` (`IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`,
  `EndpointProviderRegistration`, `EndpointRouteBuilderExtensions`, `IEndpointTreeRouteBuilder`,
  `DefaultEndpointTreeRouteBuilder`, the disposition types); `NCode.Identity.Registration`'s `IServiceBuilder<TMarker>`.
- `src/NCode.Identity.OpenId.Authentication/Endpoints/`: `OpenIdEndpointGroup.cs`, `OpenIdExceptionEndpointFilter.cs`,
  `Token/DefaultTokenEndpointProvider.cs`, `EndSession/Handlers/DefaultEndSessionHandler.cs`, `BackchannelAuthentication/`.
- `src/NCode.Identity.OpenId.Core/Exceptions/DefaultOpenIdExceptionHandler.cs`; the `ManagementRootGroup` hierarchy.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): service,
  strategy collection or mediator; the litmus this shape applies.
- [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md): the registration
  shape, including `AddMessageFactory<T>()`.
- [ADR-1085](1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md): the CIBA endpoint built to this shape and the subject-resolution capability seam.
- [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md): the tenant
  route that prefixes the OpenID group.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
  implementations depend only on abstractions.

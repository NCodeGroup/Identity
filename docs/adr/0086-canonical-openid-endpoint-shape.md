# 86. Canonical OpenID endpoint shape: a thin endpoint over a strongly-typed message and a mediator pipeline

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

Every OAuth / OpenID Connect protocol endpoint performs the same orchestration: reject the wrong verb/content-type,
authenticate the client, parse the request parameters, validate them, do the protocol work, and render a result or an
error. The repository already has the building blocks for doing this cohesively — strongly-typed messages
(`OpenIdMessage<T>` with `KnownParameter`s and a static `Load`), the mediator (`ICommand` / `ICommandHandler`,
`ICommand<TResponse>` / `ICommandResponseHandler`, [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)),
self-registering endpoint families ([ADR-0009](0009-endpoint-families-own-their-route-group.md),
[ADR-0069](0069-registration-library-compile-time-marker-nesting.md)), and an exception filter that renders any
`IOpenIdError` thrown as `OpenIdException`.

The **token** endpoint (`DefaultTokenEndpointProvider`) and the **end-session** endpoint
(`DefaultEndSessionHandler` behind `DefaultEndSessionEndpointProvider`) already embody the intended shape: the endpoint
is a thin orchestrator, the request is a loaded message, and validation and each replaceable decision are mediator
commands. But the pattern was never written down, so a new endpoint can just as easily be built the **ad-hoc** way —
hand-rolled `form.TryGetValue(...)` parsing, inline `if`-validation, and all the protocol work inlined in the route
handler. The **device-authorization** endpoint (`DefaultDeviceAuthorizationEndpointHandler`) is exactly that outlier:
it reads the form directly and inlines its logic, diverging from the token/end-session shape for no reason other than
that the convention was implicit. Without a recorded canonical shape, each new endpoint re-litigates the structure and
the family drifts.

## Decision

**A protocol endpoint is a thin `IEndpointProvider` that (1) guards the verb/content-type, (2) authenticates the
client, (3) loads a strongly-typed `OpenIdMessage<T>` request, (4) drives validation and every replaceable protocol
step through mediator commands, and (5) renders the result. No ad-hoc form parsing, no inline validation, and no
protocol work in the route handler.** Concretely, a new endpoint follows this shape:

- **Thin endpoint (`IEndpointProvider`).** `Map` wires the route with `.WithName`/`.WithTags`/`.Produces`/
  `.DisableAntiforgery()`/`.WithOpenIdDiscoverable()`. `HandleRouteAsync` only orchestrates: verb + content-type guard
  → `IClientAuthenticationService.AuthenticateClientAsync` → load → mediator dispatch → render. It holds **no**
  protocol logic and depends on as little as possible (ideally only `IClientAuthenticationService`; everything else is
  reached through `openIdContext.Mediator`).
- **Strongly-typed request message.** Model the request as `IFooRequest : IOpenIdRequest` plus
  `FooRequest : OpenIdMessage<FooRequest>, IFooRequest`, with each parameter a `KnownParameter` on
  `OpenIdCommonParameters` and a `GetKnownParameter` / `SetKnownParameter` property. Materialize it with
  `FooRequest.Load(openIdEnvironment, formData)` and register it with `builder.AddMessageFactory<FooRequest>()`. Never
  hand-roll `form.TryGetValue(...)` in the endpoint.
- **Validation is a mediator command.** `ValidateFooRequestCommand(OpenIdContext, OpenIdClient, IFooRequest) : ICommand`
  with a `DefaultValidateFooRequestHandler : ICommandHandler<…>, ISupportMediatorPriority` (`MediatorPriority =
DefaultMediatorPriorities.High`) that throws `errorFactory.X().WithStatusCode(…).AsException()` on any violation —
  the exception filter renders it. Multiple validators compose via `TryAddEnumerable`.
- **Each replaceable decision/step is a mediator command returning a disposition.**
  `DoFooCommand(…) : ICommand<FooDisposition>` with a `DefaultDoFooHandler : ICommandResponseHandler<DoFooCommand,
FooDisposition>`. When a step is a **capability seam** a deployment supplies (resolving a subject, issuing a grant,
  delivering a notification), ship a **default handler that reports "unsupported/undecided"** (reusing the existing
  disposition where one fits — e.g. `AuthenticateSubjectDisposition`, whose default returns an error the endpoint
  renders) and let a capability package replace it via `TryAddSingleton`. This is how the resource-owner-password grant
  and CIBA ([ADR-0085](0085-ciba-grant.md)) resolve a subject, and how the processing/persistence tail is factored out
  of the endpoint.
- **Transports stay injectable services, invoked from handlers.** A side-effecting transport (back-channel-logout
  delivery, CIBA notification) is an injectable service interface (`IBackChannelLogoutNotifier`,
  `ICibaNotificationService`), not a mediator command — it is called **from** a mediator handler, keeping the
  "decision" in the pipeline and the "delivery" in a replaceable service ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)
  litmus: mediator only for a genuine pipeline seam).

The reference exemplars are the **token** and **end-session** endpoints and the **CIBA** backchannel endpoint
([ADR-0085](0085-ciba-grant.md)). The **device-authorization** endpoint is the known pre-convention outlier and is to
be migrated to this shape (backlog `F18`).

## Options considered

- **Leave the shape implicit (status quo).** Rejected: it already produced one divergent endpoint (device
  authorization); every new endpoint would re-decide the structure and the family would keep drifting. Writing the
  canonical shape down is the cheapest way to keep the endpoint family cohesive.
- **A base class / template-method endpoint that encodes the steps.** Rejected: the steps vary (some endpoints have no
  client auth, some render redirects, some render JSON), and a rigid base class fights the minimal-API model and the
  per-endpoint OpenAPI metadata. A documented convention over the existing primitives (message + mediator) composes
  better than an inheritance scaffold.
- **Put the protocol work in the endpoint and use the mediator only for validation.** Rejected: it leaves the endpoint
  fat and the persistence/issuance logic untestable without the HTTP pipeline. Factoring the processing tail into an
  `ICommandResponseHandler` (as end-session and CIBA do) makes each step independently unit-testable and replaceable.
- **Model request parsing ad hoc (direct form reads) for "simple" endpoints.** Rejected: "simple" endpoints grow
  parameters (the device flow and CIBA both carry several), and a `KnownParameter`-backed message gives typed access,
  consistent parsing/serialization, extensibility, and OpenAPI metadata for free.

## Consequences

- New protocol endpoints have one canonical shape to follow, and reviewers have a concrete rule to hold them to; the
  endpoint stays thin and every protocol step is an independently testable, replaceable mediator handler.
- Capability seams (subject resolution, grant issuance, notification) are uniform: a default "unsupported" handler plus
  a capability package that replaces it, so the core ships protocol-correct and deployment-specific behavior plugs in.
- The device-authorization endpoint is now explicitly a deviation to be reconciled (backlog `F18`), rather than an
  undocumented second pattern a new author might copy.
- The convention is enforced by convention/review, not the compiler; the [production C# instructions](../../.github/instructions/csharp-production.instructions.md)
  carry the rule so it is auto-applied to `src/**/*.cs` work.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — service vs strategy-collection vs mediator; the
  litmus this shape applies (mediator for pipeline seams, services for transports).
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoint families own their route group and discovery
  advertisement.
- [ADR-0069](0069-registration-library-compile-time-marker-nesting.md) — the `DefaultRegistration` +
  `extension(IServiceBuilder<TMarker>)` registration shape, including `AddMessageFactory<T>()`.
- [ADR-0085](0085-ciba-grant.md) — the CIBA endpoint built to this shape, and the subject-resolution capability-seam
  example.
- Exemplars in `src/NCode.Identity.OpenId.Authentication/Endpoints/`: `Token/DefaultTokenEndpointProvider.cs`,
  `EndSession/Handlers/DefaultEndSessionHandler.cs`, `BackchannelAuthentication/`.

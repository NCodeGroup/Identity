# 1001. Mediator (commands & handlers) vs. dependency-injected logic classes

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md)

## Context

[project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md) makes
behavior a service by default, a strategy collection when it is open, and a mediator only at a pipeline seam, through
one litmus question and a ladder that escalates only when a requirement forces it. In this family the two ways most
often confused are:

1. The **NCode.Mediator** pattern — a request-scoped `IMediator` dispatches `*Command` messages to
   `ICommandHandler<T>` / `ICommandResponseHandler<T, TResponse>` implementations.
2. Plain **dependency-injected logic/service classes** — an `IFoo` interface with a `DefaultFoo`
   implementation, injected and called directly.

An audit of the existing code shows the real system is **not a binary** choice but **three distinct
shapes**, distinguished by their DI registration and their calling convention:

| Shape                                | Registration                                           | How it is called                                                    | Representative usages                                                                                                                                                                                                                         |
| ------------------------------------ | ------------------------------------------------------ | ------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Service (capability)**          | `TryAddSingleton<IFoo, DefaultFoo>`                    | Caller invokes it and consumes the return value                     | `ITokenService`/`DefaultTokenService`, `IClaimsService`, `ICryptoService`, `IClientAuthenticationService`, `IJsonWebTokenService`, `ISecretKeyCollectionProvider`                                                                             |
| **2. Strategy collection (plugins)** | `TryAddEnumerable<IFoo, …>`                            | Caller iterates or selects among the implementations itself         | `ITokenGrantHandler` (selected by `DefaultSelectTokenGrantHandlerHandler`), `IEndpointProvider`, `ISecretKeyTypeDescriptor`, `EccCurveSpecification`                                                                  |
| **3. Mediator (pipeline seam)**      | `ICommandHandler<T>` / `ICommandResponseHandler<T, R>` | Caller `SendAsync`es a message and does **not** know who handles it | `Get…ClaimsCommand` (void + mutate, fan-out, priority), `Validate…Command` (fan-out), `SelectTokenGrantHandlerCommand → ITokenGrantHandler` (single, returns a value), `DiscoverMetadataCommand`, `LoadRequestValuesCommand → IRequestValues` |

Two observations from the code make the boundary precise:

- **A service may drive the mediator, never the reverse.** `DefaultTokenService` (a `TryAddSingleton`
  service) builds a `List<Claim>` and a payload dictionary, then `SendAsync`es the void
  `GetAccessTokenSubjectClaimsCommand` / `GetAccessTokenPayloadClaimsCommand` so independently
  registered handlers _mutate those collections_, and only then continues with deterministic
  computation (lifetime, filtering, signing). Handlers, in turn, call services to compute.
- **The mediator is used even for single-handler steps** (`SelectTokenGrantHandlerCommand`,
  `LoadRequestValuesCommand`, `CreateAuthorizationTicketCommand`, `AuthenticatePasswordGrantCommand`).
  These are single-owner _today_, yet they flow through the mediator because they are **seams on the
  request pipeline** the framework wants consumers to be able to override, and which benefit from the
  mediator's pipeline semantics (priority ordering via `ISupportMediatorPriority`, post-processing via
  `ICommandResponsePostProcessor`, and unified exception handling via `OnUnhandledExceptionCommand`).

## Decision

The family applies project-template ADR-0103's litmus question and its coupling-direction discriminator; a seam on the
request pipeline is an `NCode.Mediator` command, and a capability called for an answer is a service.

### Decision ladder

The ladder is project-template ADR-0103's. In this family a service is registered with `TryAddSingleton`, a strategy
collection with `TryAddEnumerable`, and a mediator seam is an `ICommandHandler<T>` or
`ICommandResponseHandler<T, TResponse>`; a mediator seam also serves a collection point that many contributors mutate,
such as claims, discovery metadata and validation.

### Layering rule (already followed by the code)

Endpoints and services **orchestrate and compute**; the mediator **marks the specific seams that must
stay open**; handlers **may call services** to do the work; **services never call handlers except
through the mediator**. `DefaultTokenService` is the canonical example — a service that reaches for
the mediator only at the claims-contribution seam.

## Options considered

The general options are weighed in
[project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md).

## Consequences

- New extension points that only need caller-owned fan-out (like `ISecretKeyTypeDescriptor`) are added as
  strategy collections, avoiding mediator ceremony; they can later be promoted to mediator events if
  observation or ordered pre/post processing becomes a requirement.

### "You chose wrong" smells

The smells are project-template ADR-0103's; in this family a service smell is a `switch` over key, grant or claim types
inside one class.

## References

Verified against the source at the time of writing:

- `NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs` — service that orchestrates and
  drives the mediator at the claims-contribution seam.
- `NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultRegistration.cs` — shows all three
  shapes side by side (`TryAddSingleton` response handler, `TryAddEnumerable` validators/grant
  handlers).
- `NCode.Identity.OpenId.Authentication/Tokens/Handlers/DefaultGetAccessTokenSubjectClaimsHandler.cs`
  — void, collection-mutating command handler with `ISupportMediatorPriority`.
- `NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateSubjectPostProcessor.cs`
  — `ICommandResponsePostProcessor` cross-cutting behavior.
- `NCode.Identity.OpenId.Core/Exceptions/DefaultOpenIdExceptionHandler.cs` — exception handling routed
  through the mediator.
- `NCode.Identity.OpenId.Authentication/Endpoints/Jwks/Converters/IJsonWebKeyConverter.cs`, and the core
  `ISecretKeyTypeDescriptor` ([ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md)) and
  `EccCurveSpecification` / `IEccCurveCatalog` ([ADR-1068](1068-unified-extensible-elliptic-curve-catalog.md)) —
  strategy-collection extension points (shape 2) chosen deliberately over the mediator.

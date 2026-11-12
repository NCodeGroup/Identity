# 1001. Behavior is a service, a strategy collection or a mediator seam, chosen by one ladder

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md)

## Context

[project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md) makes
behavior a service by default, a strategy collection when it is open, and a mediator only at a pipeline seam, through
one litmus question and a ladder that escalates only when a requirement forces it. In this family two ways of structuring
behavior are most often confused:

1. The **NCode.Mediator** pattern: a request-scoped `IMediator` dispatches `*Command` messages to
   `ICommandHandler<T>` and `ICommandResponseHandler<T, TResponse>` implementations.
2. Plain **dependency-injected logic classes**: an `IFoo` interface with a `DefaultFoo` implementation, injected and
   called directly.

The family's code is not a binary choice between them but three distinct shapes, told apart by their DI registration and
their calling convention.

## Decision

**The family applies project-template ADR-0103's litmus question, ladder and coupling-direction discriminator. A
capability called for an answer is a service, an open set is a strategy collection, and a seam on the request pipeline
is an `NCode.Mediator` command dispatched by the request-scoped `IMediator`.**

### The three shapes

| Shape                                | Registration                                           | How it is called                                                    | Representative members                                                                                                                                                                                           |
| ------------------------------------ | ------------------------------------------------------ | ------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Service (capability)**          | `TryAddSingleton<IFoo, DefaultFoo>`                    | The caller invokes it and consumes the return value                 | `ITokenService` / `DefaultTokenService`, `IClaimsService`, `ICryptoService`, `IClientAuthenticationService`, `ISecretKeyCollectionProvider`                                                                      |
| **2. Strategy collection (plugins)** | `TryAddEnumerable<IFoo, …>`                            | The caller iterates or selects among the implementations itself     | `ITokenGrantHandler` (selected by `DefaultSelectTokenGrantHandlerHandler`), `IEndpointProvider`, `ISecretKeyTypeDescriptor`, `EccCurveSpecification`                                                              |
| **3. Mediator (pipeline seam)**      | `ICommandHandler<T>` / `ICommandResponseHandler<T, R>` | The caller `SendAsync`es a message and does not know who handles it | `Get…ClaimsCommand` (void, fan-out, priority-ordered), `Validate…Command` (fan-out), `SelectTokenGrantHandlerCommand`, `DiscoverMetadataCommand`, `LoadRequestValuesCommand` |

Each shape has one registration and one calling convention: a service is registered with `TryAddSingleton<IFoo,
DefaultFoo>` and its caller consumes the return value; a strategy collection is registered with `TryAddEnumerable` and
its caller iterates or selects; a mediator seam is an `ICommandHandler<T>` or `ICommandResponseHandler<T, TResponse>`
that the caller reaches through `SendAsync` without knowing the handler.

### A mediator seam is also a collection point

A mediator seam also serves as a collection point that many contributors mutate. The claims commands are void,
fan-out and priority-ordered, so independently registered handlers each add to the same claim list; discovery metadata
and validation work the same way.

### A single-handler step can still be a mediator command

A single-handler step on the request pipeline is a mediator command when consumers must be able to override it.
`SelectTokenGrantHandlerCommand`, `LoadRequestValuesCommand`, `CreateAuthorizationTicketCommand` and
`AuthenticatePasswordGrantCommand` are such seams. A seam of this kind gets the mediator's pipeline semantics: priority
ordering through `ISupportMediatorPriority`, post-processing through `ICommandResponsePostProcessor` and unified
exception handling through `OnUnhandledExceptionCommand`.

### Layering rule

**Endpoints and services orchestrate and compute; the mediator marks the seams that stay open; handlers may call
services; a service reaches a handler only through the mediator.**

`DefaultTokenService` is the canonical example. It builds the claim list and the payload dictionary, sends
`GetAccessTokenSubjectClaimsCommand` and `GetAccessTokenPayloadClaimsCommand` (and the ID-token equivalents) so that
registered handlers mutate them, and then computes the lifetime, the filtering and the signing itself.

### Caller-owned fan-out is a strategy collection

An extension point that needs only caller-owned fan-out, such as `ISecretKeyTypeDescriptor`, is a strategy collection and
not a mediator event.

## Options considered

The general options are weighed in
[project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md). In this
family the choice reduces to the ladder above:

- **Every behavior is a mediator command.** Rejected by the ladder: a capability called for an answer gains nothing from
  a message dispatch that hides its handler, and a collection that the caller iterates needs no priority ordering or
  post-processing.
- **Every behavior is a directly injected service.** Rejected by the ladder: a seam on the request pipeline that
  consumers must override, or that many contributors mutate, needs the mediator's priority ordering, post-processing
  and exception handling.
- **A service, a strategy collection or a mediator seam by the ladder.** Chosen: each behavior takes the cheapest shape
  its requirement allows.

## Consequences

- A new extension point that needs only caller-owned fan-out, such as `ISecretKeyTypeDescriptor`, is added as a
  strategy collection and carries no mediator ceremony.
- A new seam on the request pipeline that consumers must override is a mediator command, with the priority,
  post-processing and exception handling that comes with it.
- The "you chose wrong" smells are project-template ADR-0103's. The family-specific smell for a service is a `switch`
  over key, grant or claim types inside one class.

## References

- `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs`: the service that orchestrates and drives the
  mediator at the claims-contribution seam.
- `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultRegistration.cs`: the three shapes side by side
  (`TryAddSingleton` response handler, `TryAddEnumerable` validators and grant handlers).
- `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/DefaultGetAccessTokenSubjectClaimsHandler.cs`: a void,
  collection-mutating command handler with `ISupportMediatorPriority`.
- `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticatePostProcessor.cs`: an
  `ICommandResponsePostProcessor`, a cross-cutting behavior.
- `src/NCode.Identity.OpenId.Core/Exceptions/DefaultOpenIdExceptionHandler.cs`: exception handling routed through the
  mediator.
- `ISecretKeyTypeDescriptor` ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)) and
  `EccCurveSpecification` / `IEccCurveCatalog` ([ADR-1068](1068-elliptic-curves-are-one-extensible-catalog-and-an-imported-point-is-checked-against-its-curve.md)): the
  strategy-collection extension points (shape 2) chosen over the mediator.

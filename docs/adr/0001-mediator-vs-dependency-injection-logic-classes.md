# 1. Mediator (commands & handlers) vs. dependency-injected logic classes

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

The library family offers two superficially interchangeable ways to structure behavior, and it is
frequently unclear which to reach for:

1. The **NCode.Mediator** pattern — a request-scoped `IMediator` dispatches `*Command` messages to
   `ICommandHandler<T>` / `ICommandResponseHandler<T, TResponse>` implementations.
2. Plain **dependency-injected logic/service classes** — an `IFoo` interface with a `DefaultFoo`
   implementation, injected and called directly.

Choosing wrong in either direction is costly. Overusing the mediator buys extensibility the feature
never needed while destroying discoverability and traceability (you cannot "Go to definition" from a
`SendAsync` to the code that runs). Underusing it hard-codes a seam that consumers later need to
extend, forcing an invasive refactor.

An audit of the existing code shows the real system is **not a binary** choice but **three distinct
shapes**, distinguished by their DI registration and their calling convention:

| Shape | Registration | How it is called | Representative usages |
|-------|--------------|------------------|-----------------------|
| **1. Service (capability)** | `TryAddSingleton<IFoo, DefaultFoo>` | Caller invokes it and consumes the return value | `ITokenService`/`DefaultTokenService`, `IClaimsService`, `ICryptoService`, `IClientAuthenticationService`, `IJsonWebTokenService`, `ISecretKeyCollectionProvider` |
| **2. Strategy collection (plugins)** | `TryAddEnumerable<IFoo, …>` | Caller iterates or selects among the implementations itself | `ITokenGrantHandler` (selected by `DefaultSelectTokenGrantHandlerHandler`), `IEndpointProvider`, `IJsonWebKeyConverter`, `EccCurveSpecification` |
| **3. Mediator (pipeline seam)** | `ICommandHandler<T>` / `ICommandResponseHandler<T, R>` | Caller `SendAsync`es a message and does **not** know who handles it | `Get…ClaimsCommand` (void + mutate, fan-out, priority), `Validate…Command` (fan-out), `SelectTokenGrantHandlerCommand → ITokenGrantHandler` (single, returns a value), `DiscoverMetadataCommand`, `LoadRequestValuesCommand → IRequestValues` |

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

Use this single litmus test:

> **Is this a deliberate extension / override / observation _seam on the request pipeline_ — or a
> concrete capability you call for an answer?**
>
> **Seam → mediator. Capability → service.**

The sharpest supporting discriminator is **coupling direction**:

- **Service / strategy collection:** the caller _knows and names_ what it wants and depends on the
  abstraction directly (e.g. `TokenService.CreateAccessTokenAsync(...)`). The dependency arrow points
  from caller → capability.
- **Mediator:** the caller _must stay ignorant_ of who participates. The sender depends only on the
  command message; handlers can be added, reordered, wrapped, or overridden without the sender
  changing. This total inversion is the whole point.

### Decision ladder

Prefer the cheapest option and escalate only when a requirement forces it. The mediator's power
(decoupling, ordering, pre/post processing, exception pipeline) must be _earned_, because it is paid
for in discoverability and traceability.

1. **One deterministic implementation and the caller wants the result** → **Service**
   (`TryAddSingleton`). This is the default and covers the large majority of code (crypto, token
   creation, claim utilities, key resolution).
2. **Many independent implementations, but your code owns the orchestration** (you iterate or pick),
   and you do **not** need ordering / pre-post / exception semantics → **Strategy collection**
   (`TryAddEnumerable` of a domain interface). This is the lightweight middle ground.
3. **It is a pipeline seam** where any of the following is true → **Mediator command/handler**:
   - the sender must not know the handlers;
   - consumers should be able to add, reorder, or override participants;
   - it needs priority ordering, pre/post processors, or unified exception handling;
   - it is a "many contributors mutate shared state" collection point (claims, discovery metadata,
     validation).

### Layering rule (already followed by the code)

Endpoints and services **orchestrate and compute**; the mediator **marks the specific seams that must
stay open**; handlers **may call services** to do the work; **services never call handlers except
through the mediator**. `DefaultTokenService` is the canonical example — a service that reaches for
the mediator only at the claims-contribution seam.

## Options considered

- **Everything through the mediator.** Rejected: maximizes extensibility at the cost of
  discoverability, traceability, and runtime overhead for behavior that is fixed and single-owner. It
  makes the common case (call a capability, use the result) unnecessarily indirect.
- **Everything as DI services.** Rejected: forces `if/else`-over-type branching for genuinely
  open-ended concerns (grant types, claim contributors, validators) and provides no clean place for
  consumers to plug in without modifying framework code.
- **A three-tier ladder (chosen).** Matches how the code already behaves, keeps the common case
  simple, gives a lightweight extension mechanism (strategy collections) before the heavyweight one
  (mediator), and yields a single, memorable decision question.

## Consequences

- Contributors have one question to answer ("is it a pipeline seam?") and a ladder that defaults to
  the simplest tool.
- New extension points that only need caller-owned fan-out (like `IJsonWebKeyConverter`) are added as
  strategy collections, avoiding mediator ceremony; they can later be promoted to mediator events if
  observation or ordered pre/post processing becomes a requirement.
- The mediator remains reserved for true request-pipeline seams, keeping the set of commands small
  and meaningful.

### "You chose wrong" smells

- **Mediator smell:** a command returns a value, the sender knows exactly what it wants and could
  depend on the capability directly, no consumer is _expected_ — now or later — to add, reorder, or
  override a handler, and nothing needs priority/pre-post → it is a service wearing a costume.
  Collapse it to a `TryAddSingleton` service. The discriminator is _intent_, not head count: a genuine
  pipeline seam stays a mediator command even while it has a single handler today (see the
  token/authorize/challenge seams above).
- **Service smell:** you find yourself `switch`-ing over key/grant/claim _types_ inside one class, or
  consumers keep asking "how do I add my own X to this step?" → you hard-coded a seam. Promote it to a
  strategy collection, or to the mediator if it needs pipeline semantics.

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
- `NCode.Identity.OpenId.Authentication/Endpoints/Jwks/Converters/IJsonWebKeyConverter.cs` and
  `EccCurveSpecification` / `IEccCurveSpecificationRegistry` — strategy-collection extension points
  (shape 2) chosen deliberately over the mediator.

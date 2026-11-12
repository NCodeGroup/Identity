# 1047. Observation is a fault-isolated event seam that audit specializes and protocol errors reach once

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md)

## Context

The family needs one coherent way to observe what happens: tokens issued, grants revoked, authentication succeeded or
failed, secrets rotated, administrative mutations applied. Consumers audit, log, count, alert on or stream these to an
external bus. The forces, in priority order:

1. **Observation must not alter outcomes, per observer.** An audit sink that writes to a slow or failing store must
   never break token issuance, change the response a client receives, or stop the other sinks from running. Isolation
   is per handler and the default, not something a consumer remembers to configure.
2. **The seam must fit the extensibility ladder.** The template's ladder
   ([project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md))
   reserves the mediator for pipeline seams that shape the result: validation that rejects, claims that are mutated,
   a grant selection that returns a value. Pure observation is a different thing, and the type system should say so,
   so that a reader can tell at a glance whether a seam can change the outcome.
3. **Observation must reach lower layers.** Secrets, a standalone library with no request scope, audits its key
   hierarchy (`RootKeyAuditEvent`, `MasterKeyAuditEvent`, `RemoteKeyAuditEvent`), so the abstraction lives below the
   OpenID layer, where any package can publish.

The mediator carries the transport mechanics well (multi-handler dispatch, priority ordering, polymorphic resolution),
but its fan-out awaits handlers in one loop, so the first throw aborts the remaining handlers, it rethrows unless a
catch-all is registered, and it has no hierarchical dispatch: a handler cannot subscribe to a base event type to
observe a family of events. A token-issued notification modelled as a void mediator command would let a throwing
handler abort the issuance.

A second force concerns protocol errors. An OAuth or OpenID Connect error response is an outcome a security team
audits: failed client authentication, an invalid grant, a rejected redirect URI. Errors reach the response through two
paths, a thrown `OpenIdException` and a returned result whose response is an error, and publishing at each call site
would miss some paths and double-count others.

## Decision

**Pure observation is an event, published as the last, side-effect-only step of an operation; a seam that shapes the
outcome (rejects, mutates, returns) stays a mediator command.** Events travel on a dedicated, deliberately narrow
publish/subscribe seam whose handlers are fault-isolated.

### The seam

- `NCode.Identity.Events.Abstractions` holds `IEvent`, `IEventHandler<in TEvent>`, `ISupportHandlerPriority` with
  `DefaultHandlerPriorities`, and `IEventPublisher` (`PublishAsync`, `HasSubscribers<TEvent>()`). A handler is
  registered with `TryAddEnumerable` or `AddEventHandler<TEvent, THandler>()`.
- `NCode.Identity.Events` (`AddEventServices()`) holds the singleton `DefaultEventPublisher`.
- Each handler runs in its own try/catch. A failure is logged (`EventHandlerFailed`) and never reaches the caller or
  the other handlers.
- Dispatch is hierarchical on the event's runtime type: handlers for the type, its base types and its `IEvent`
  interfaces all receive the event. The closed `IEventHandler<>` service types are cached per runtime type. That
  `MakeGenericType` path is `[RequiresDynamicCode]`, so dispatch is neither AOT- nor trim-compatible.
- Each publish creates a fresh DI scope, resolves the handlers from it, orders them by priority and awaits them
  inline, so the singleton publisher never captures scoped services.
- Background delivery is opt-in. `AddBackgroundEventDelivery()` enables a bounded `IBackgroundEventQueue` (1,024
  events) that `BackgroundEventDispatcher` drains in a fresh scope on the host's stopping token. A handler opts in with
  `ISupportBackgroundDelivery` and runs inline when the queue is off. A full queue drops the event and logs
  `BackgroundEventDropped`.
- An event is an immutable record, and an audit event's envelope is derived by the recorder before it is published,
  so a background handler works from the event alone and never from ambient request state such as the `HttpContext`
  or the request scope.

### Events carry no secrets

Events carry identifiers, hashes and metadata, never tokens, secrets, codes or key material. `TokenIssuedAuditEvent`
carries the token's metadata, not its value.

### Audit specializes the seam

Auditing is the security-grade specialization: `IAuditEvent : IEvent` and `AuditEvent`. An audit sink is an
`IEventHandler<IAuditEvent>`. The shape of the record is [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)'s, and
the persisted audit store is
[ADR-1303](1303-audit-records-are-kept-in-an-indexed-store-that-each-replica-writes-in-batches.md)'s.

A plain event is published raw. An audit event goes through a typed recorder facade (`IAuditEventRecorder` with
`DefaultAuditEventRecorder`, and `IManagementAuditRecorder`) that derives the envelope once. A state-changing success
publishes at its call site.

### Protocol errors are audited once

A protocol error is audited as `openid.error` (`OpenIdErrorEvent`), once per errored request, from
`OpenIdExceptionEndpointFilter`:

- the thrown path publishes eagerly from the `OpenIdException`'s error;
- the returned path is detected from the result's value: an `IOpenIdResult` whose `Response` is an `IOpenIdError`, or an
  `ISupportOpenIdError` whose `Error` is set, such as the redirect-delivered `AuthorizationResult`, so a result that
  carries an error exposes it in one of those two ways;
- an `HttpContext.Items` sentinel enforces once per request;
- a recorder failure is logged and swallowed, so it never changes the response;
- `ISupportOpenIdError` is a static marker for a type that can carry an error, so a successful `OpenIdResult<T>`
  never advertises one.

### Logging, metrics and tracing are subscribers

`DefaultEventLoggingHandler`, `DefaultAuditLoggingHandler`, `DefaultEventMetricsHandler` and
`DefaultEventTracingHandler` subscribe like any other handler. A call site never also logs a milestone it publishes.
Diagnostics stay ordinary `ILogger` calls. The exception behind an errored protocol response is such a diagnostic:
`OpenIdExceptionEndpointFilter` logs it for operators, an unexpected `server_error` at Error and any other at Debug,
beside the `openid.error` audit event; the two are complementary, not a duplicate.

### A governed catalog

Events form a governed catalog in `docs/audit-records.md`. Tests assert with a recording handler.

## Options considered

- **The mediator as the transport.** It is not fault-isolated per handler, rethrows by default and has no hierarchical
  dispatch; using its `ICommand` for notifications blurs the line the ladder draws. Rejected.
- **Ad-hoc observer collections.** Each would re-implement isolation, ordering and scoping. Rejected.
- **A wrapper type for every event.** It adds ceremony without isolating anything. Rejected.
- **A non-generic handler.** It gives up typed subscription and hierarchical dispatch. Rejected.
- **MediatR or an external bus.** A dependency for what a small in-process seam does, and no better isolation by
  default. Rejected.
- **.NET `event` or `IObservable`.** Neither isolates a failing subscriber, orders by priority or resolves handlers
  from DI scopes. Rejected.
- **A dynamic metadata bag on each event.** It is untyped and invites secrets. Rejected.
- **Publishing errors at each call site.** It misses paths and double-counts others; one funnel audits each errored
  request once. Rejected.
- **`OpenIdResult<T>` implementing `ISupportOpenIdError`.** The marker stays static, and the filter detects errors
  from the response. Rejected.
- **A sentinel keyed on the error instance.** One request can carry several instances for one failure, so the sentinel
  is per request. Rejected.
- **Chosen:** a dedicated event seam with per-handler fault isolation, hierarchical dispatch and opt-in background
  delivery, specialized by audit events, with protocol errors published from a single endpoint filter.

## Consequences

- A reader sees from the type whether a seam can change the outcome: events cannot, commands can.
- A failing sink never breaks a request or another sink, and a consumer adds a sink by registering one handler.
- Subscribing to `IAuditEvent` or a base event observes a whole family without listing its members.
- Every errored protocol request yields exactly one `openid.error` audit event, whichever path produced the error.
- Hierarchical dispatch costs a reflection path, so it is not AOT- or trim-compatible.
- Background delivery can drop events when the queue is full; a deployment that cannot lose audit events needs a
  durable path, which [ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md)'s dispatch port is the place for.
- Smell: a handler that must change the outcome, or a call site that logs a milestone it also publishes, is on the
  wrong seam.

## References

- `src/NCode.Identity.Events.Abstractions`, `src/NCode.Identity.Events`, `OpenIdExceptionEndpointFilter`
- `docs/audit-records.md`
- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md),
  [ADR-1303](1303-audit-records-are-kept-in-an-indexed-store-that-each-replica-writes-in-batches.md),
  [ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md)
- [project-template ADR-0103](project-template/0103-behavior-is-a-service-a-strategy-collection-or-a-mediator.md)

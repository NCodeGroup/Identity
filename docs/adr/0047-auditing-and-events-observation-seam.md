# 47. Auditing and events: a dedicated observation seam, distinct from the mediator

- **Status:** Proposed
- **Date:** 2026-10-04
- **Deciders:** NCode Group

## Context

The library family needs a coherent, repo-wide way to **observe** meaningful things that happen —
tokens issued, grants revoked, authentication succeeded or failed, secrets rotated, administrative
mutations applied — so that consumers can audit, log, emit metrics, raise alerts, or stream domain
events to an external bus.

The forces that shape the design, in priority order:

1. **Observation must not alter outcomes — per observer, and safe by default.** An audit sink that
   writes to a slow or failing store must never break token issuance, never change the response a
   client receives, and — critically — a failure in one sink must not prevent the others from running.
   Isolation must be **per handler** and the default, not something a consumer has to remember to
   configure. The failure of an observer is the observer's problem, not the request's.

2. **It must be coherent with the existing extensibility ladder.**
   [ADR-0001](./0001-mediator-vs-dependency-injection-logic-classes.md) defines three shapes —
   **service**, **strategy collection**, and **mediator** — and reserves the mediator for _request-
   pipeline seams that shape the result_ (validation that can reject, claim collections that are
   mutated, grant selection that returns a value). Pure observation is a different thing, and the type
   system should say so: a reader must be able to tell at a glance whether a seam can change the
   outcome. Overloading the mediator's `ICommand` for notifications blurs exactly the line ADR-0001
   draws.

3. **Reaching lower layers is desirable, but secondary.** Auditing ideally spans every layer, including
   JOSE and Secrets, which are standalone libraries with no request scope. This argues for an
   abstraction that can live below the OpenID layer. It is a forward-looking consideration rather than
   a present constraint: today the meaningful events are all in the OpenID layer, and
   `NCode.Mediator.Abstractions` itself carries no web coupling, so this force future-proofs the design
   rather than forcing it.

A single event already exists — `SecurityTokenIssuedEvent` in
[`src/NCode.Identity.OpenId.Authentication.Abstractions/Tokens/Commands/SecurityTokenIssuedEvent.cs`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Tokens/Commands/SecurityTokenIssuedEvent.cs) —
modeled as a void mediator `ICommand` and dispatched from `DefaultTokenService` and
`DefaultAuthorizationCodeService`. It is a _notification_, not a result-shaping command, yet on the
mediator it is **not** fault-isolated: a throwing handler aborts the issuance. That latent hazard is
itself evidence for a safe-by-default observation seam.

The mediator (`NCode.Mediator`) handles the **transport mechanics** well — multi-handler dispatch,
priority ordering, DI-driven enumeration, polymorphic runtime-type resolution, and pre/post and
exception seams — and its fault tolerance is configurable via a catch-all exception handler. But its
fan-out root awaits handlers in a single loop, so the **first throw aborts the remaining handlers**
(isolation is per-dispatch, not per-handler), it **rethrows unless a catch-all is remembered** (unsafe
by default), and it has **no hierarchical dispatch** (a handler cannot subscribe to a base event type
to observe a whole family). These gaps are in the audit-specific semantics of forces (1) and (2), not
in raw capability.

## Decision

Introduce a **fourth, deliberately narrow shape: the event**, as a dedicated observation seam that
sits **below** the mediator and is usable from every layer. Events are published through a thin
publish/subscribe service; handlers are a strategy collection of subscribers invoked with **fault
isolation**. The mediator remains exactly as [ADR-0001](./0001-mediator-vs-dependency-injection-logic-classes.md)
describes it; events do not replace it.

### Litmus test (extends ADR-0001)

> **Does this seam shape the outcome of the request — or merely observe it?**
>
> **Shapes the outcome → mediator command. Pure observation → event.**

A mediator command may reject, mutate shared state that the caller then consumes, or return a value
the caller depends on. An **event** does none of these: it is fire-and-observe, its handlers cannot
influence the result, and a handler throwing is swallowed (logged) rather than propagated.

### The abstraction

A new low-level package **`NCode.Identity.Events.Abstractions`** (with the default implementation in
**`NCode.Identity.Events`**, per [ADR-0016](./0016-implementation-packages-depend-only-on-abstractions.md))
defines:

- **`IEvent`** — a marker interface for any event payload. Payloads are immutable
  `readonly record struct` or `record` types, consistent with the mediator's command style.

- **`IEventHandler<in TEvent> where TEvent : IEvent`** — a subscriber:
  `ValueTask HandleAsync(TEvent @event, CancellationToken cancellationToken)`. Registered with
  `TryAddEnumerable` so any number of subscribers coexist and consumers add their own without touching
  framework code.

- **`ISupportHandlerPriority`** — optional ordered delivery, structurally mirroring
  `ISupportMediatorPriority` / `DefaultMediatorPriorities` (a companion `DefaultHandlerPriorities`
  supplies the standard bands). The noun is deliberately the **participant** (`HandlerPriority`), not the
  subsystem: the mediator can use the subsystem noun because "mediator" is not a data object, but an
  event _is_ a data object, so `EventPriority` would misread as "the event's priority" rather than "this
  handler's order." The priority sorts participants, so it is named for them.

- **`IEventPublisher`** — the dispatcher: `ValueTask PublishAsync<TEvent>(TEvent @event, …)`. It
  resolves the matching handlers, orders them by priority, and invokes each inside a try/catch so one
  failing handler never aborts the publish or the caller. A `bool HasSubscribers<TEvent>()` probe
  (mirroring `ILogger.IsEnabled`) lets a caller skip assembling an expensive payload when nothing is
  listening. `IEventPublisher` is registered as a plain singleton **service** (ADR-0001 shape 1) and is
  injectable anywhere, including from JOSE and Secrets.

### Versatile subscription: hierarchical dispatch

Publishing a concrete event notifies handlers registered for that concrete type **and** for any event
type it derives from or implements. A `TokenIssuedAuditEvent` reaches `IEventHandler<TokenIssuedAuditEvent>`,
`IEventHandler<IAuditEvent>`, and `IEventHandler<IEvent>`. This lets a **single universal audit sink**
subscribe once (to `IAuditEvent`) and capture every audit event, while specific sinks subscribe to
exactly the event they care about. The publisher computes the assignable-handler set per runtime event
type **once** and caches it, so the reflection cost is paid only on the first publish of each type.

`IEventHandler<in TEvent>` is declared contravariant, but the publisher **does not rely on runtime
variance for resolution** — `Microsoft.Extensions.DependencyInjection` does not honor generic variance
when resolving a service, so `GetServices<IEventHandler<TokenIssuedAuditEvent>>()` would not return a
handler registered as `IEventHandler<IAuditEvent>`. The publisher therefore dispatches on the event's
**runtime type** (`@event.GetType()`, not the compile-time `TEvent`): it walks the runtime type's base
types and implemented `IEvent` interfaces, closes `IEventHandler<>` over each, resolves them, merges
and orders the result by priority, and caches that resolved set per runtime type. The contravariant
`in` modifier is kept purely for the ergonomic benefit that an `IEventHandler<IAuditEvent>` can handle
any derived audit event without a cast; it is never the dispatch mechanism.

#### Implementation note: cached type-plan, scope-correct resolution

The publisher caches a **type-plan** per runtime event type — the ordered array of **closed
`IEventHandler<>` service types** to resolve, never resolved instances — and resolves instances from
the ambient scope on every publish. This is the decisive borrowing-and-divergence from the mediator:
the mediator caches its composed chain _per scope_ (its wrapper is scoped), but a singleton publisher
that also serves singleton callers (JOSE/Secrets) and background delivery would turn cached instances
into a captive dependency. Caching only the reflection result keeps "build once per type" while
resolving instances per dispatch keeps scoping correct — the request scope for synchronous publish, a
fresh `IServiceScopeFactory` scope for background drain.

```csharp
// Cached once per runtime event type — service TYPES, not instances.
private sealed record EventDispatchPlan(Type[] HandlerServiceTypes);

private readonly ConcurrentDictionary<Type, EventDispatchPlan> _plans = new();

private EventDispatchPlan GetPlan(Type eventType) =>
    _plans.GetOrAdd(eventType, static t =>
        new EventDispatchPlan(
            EventTypeHierarchy(t) // t + base types + interfaces, filtered to IEvent
                .Select(e => typeof(IEventHandler<>).MakeGenericType(e))
                .ToArray()));

// Per publish: resolve instances from the CURRENT scope, dedupe by reference,
// sort by priority, then invoke each in isolation (never rethrow).
foreach (var handler in Resolve(scopeProvider, plan).OrderByDescending(Priority))
{
    try { await handler.HandleAsync(@event, cancellationToken); }
    catch (Exception ex) { /* log + IEventErrorHandler + metric; continue */ }
}
```

Priority is sorted on the resolved **instances** each publish (because `ISupportHandlerPriority` lives on
the instance), not baked into the plan; the handler count is small, so this is cheap. The exact-type
level resolves generically with no reflection; only the base-type fan-out needs `MakeGenericType`, so
that path is annotated `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]` (mirroring the mediator's
`PolymorphicSendAsync`). For AOT or trimmed hosts a switch **disables hierarchical dispatch** and runs
exact-type only; universal sinks then register per concrete event type instead of once on `IAuditEvent`.

### Delivery semantics

- **Synchronous, in-request, awaited, fault-isolated, priority-ordered** is the default — the caller
  `await`s `PublishAsync`, every handler runs, failures are caught and logged.
- **Fire-and-forget / background** is an opt-in capability provided by the implementation package (a
  bounded `IBackgroundEventQueue` drained by a hosted service) for slow or external sinks, so the
  abstraction stays small and the low-level packages pull in no hosting dependency. A handler opts into
  background delivery; the request path is never blocked on it.
- **Background delivery crosses the request boundary**, so it works from a **snapshot**, not ambient
  state: the event envelope captures correlation/trace id, tenant, actor, and client **at publish time**
  (the `HttpContext` and request scope are gone by the time a background handler runs), and the drain
  uses an **application-lifetime `CancellationToken`**, never the request token (which is already
  cancelled once the response completes).
- **Back-pressure is an explicit policy.** When the bounded background queue is full, best-effort events
  are dropped (and the drop is itself counted/logged); durable audit (below) must not drop — it blocks
  or spills to its persistent store rather than lose a record.
- **Two reliability tiers.** Best-effort observation swallows handler failures and may drop under
  back-pressure. **Compliance-grade audit requires at-least-once delivery** and cannot be lost on
  crash; that durability (a transactional outbox, retry, and publish-after-commit) is a separable
  concern specified in [ADR-0048](./0048-durable-audit-delivery-outbox.md).

### Handler lifetime and scoping

`IEventPublisher` is a singleton, but real sinks depend on **scoped** services — an EF `DbContext`,
tenant-scoped persistence, the current `OpenIdContext`. A singleton must never resolve scoped services
from the root provider (a captive dependency). Therefore:

- **Synchronous publish dispatches within the caller's ambient scope** — handlers are resolved from the
  request's `IServiceProvider`, so scoped dependencies bind to the current request.
- **Background drain creates a fresh scope per event** (or per batch) and resolves handlers from it, so
  background sinks get a clean, correctly-scoped dependency graph outside any request.
- **Handlers default to scoped** registration; a handler with only singleton dependencies may register
  as a singleton, but the publisher treats every delivery as scope-bound so the choice is the handler's.

### Auditing as a specialization of events

Auditing is not a separate mechanism — it is events with a **security-grade contract**:

- **`IAuditEvent : IEvent`** carries the common envelope every audit record needs: a **unique event id**
  (a GUID, distinct from the correlation/trace id, so at-least-once delivery can be de-duplicated), a UTC
  timestamp, a correlation/trace id, the tenant, the actor/subject, the client, the action, the outcome
  (success/failure plus reason), and a **schema version**. An `AuditEvent` abstract `record` supplies
  these once.
- The **schema version** exists because audit events are also a **serialized wire contract** to external
  sinks (SIEM, bus, cold storage); in-process compatibility is tracked by
  [ADR-0005](./0005-public-api-surface-is-tracked-and-evolves-compatibly.md), but external consumers need
  a stable, versioned event name/shape to survive evolution.
- Concrete audit events (e.g. `TokenIssuedAuditEvent`, `TokenRevokedAuditEvent`,
  `AuthenticationFailedAuditEvent`, `SecretRotatedAuditEvent`, `ManagementResourceChangedAuditEvent`)
  derive from `AuditEvent`.
- An **audit sink is just an `IEventHandler<IAuditEvent>`** — there is no separate `IAuditSink`
  interface to learn. One sink persists or ships every audit event via the hierarchical dispatch above.

### Events carry identifiers, never secrets

An event flows to logging, persistence, and external sinks, so it must be **safe to serialize anywhere**.
Events therefore carry **identifiers, hashes, and metadata — never secret material**: no raw access,
refresh, or ID tokens, no client secrets, no key material, no authorization codes. A token event carries
the token's `jti`/id, type, issuer, audience, and lifetime — not the token string. This is a hard
data-minimization rule, and it corrects the pre-existing `SecurityTokenIssuedEvent`, which carries the
live `SecurityToken`: re-homing it onto this seam replaces the token with its identifying metadata.

### Publishing ergonomics: raw publish vs. typed helpers

Publishing sites interact with the seam one of two ways, chosen by a single rule:

> **Publish raw** when the event payload is fully specified by its constructor arguments. **Provide a
> typed helper** when the event carries a shared envelope the call site should not assemble, or when a
> `HasSubscribers` short-circuit avoids real work — which is every audit event.

This mirrors `LoggerMessage` templates: the two reasons those earn their keep — centralizing the
message/`EventId` once instead of at every call site, and the `IsEnabled` short-circuit — both carry
over to events; the allocation argument does not, because event payloads are cheap value records.

- **Plain domain events (no ambient envelope) → publish raw.** The record constructor is the ergonomic,
  intent-revealing signature; a wrapper that only does `new FooEvent(a, b)` + `PublishAsync` adds public
  API surface ([ADR-0005](./0005-public-api-surface-is-tracked-and-evolves-compatibly.md)) and ceremony
  for no value: `await publisher.PublishAsync(new RefreshTokenReusedEvent(grantId, clientId), ct)`.

- **Audit events (shared envelope) → a typed recorder facade.** A single `IAuditEventRecorder`
  (`DefaultAuditEventRecorder`, per [ADR-0006](./0006-single-canonical-impl-is-defaultfoo.md)) exposes
  grouped, intent-revealing methods — `RecordTokenIssuedAsync(openIdContext, openIdClient, securityToken, ct)`,
  `RecordTokenRevokedAsync(...)`, `RecordAuthenticationFailedAsync(...)`. The recorder derives the common
  envelope once from ambient sources (clock, correlation/trace id, `OpenIdContext` actor/tenant/client),
  assembles the strongly-typed audit event, and publishes it. This keeps envelope-derivation in one
  replaceable, testable place rather than scattered across loose `IEventPublisher` extension methods, and
  parallels the existing `IOpenIdErrorFactory` facade over cross-cutting construction.

Traceability is preserved either way. Unlike the mediator — where the sender must stay ignorant of who
handles a command — the **sender of an event legitimately names the contract it raises**; only the
subscriber stays ignorant. A typed helper therefore does not weaken the decoupling, and "Go to
definition" still lands on the concrete event and recorder.

The publisher exposes a `HasSubscribers<TEvent>()` probe (mirroring `ILogger.IsEnabled`) that the
recorder consults before assembling an audit envelope, so no work is done when nothing is listening.

### Logging is a subscriber, not call-site duplication

Logging is itself an observer, so a call site emits an event **once** and never also logs the same fact
beside it — a built-in logging subscriber turns the event into a log line. Writing `logger.LogTokenIssued(…)`
next to `Publish(…TokenIssued…)` with the same data is the duplication this seam exists to remove.

One line separates the two concerns:

- **Milestones / audit / domain outcomes** (token issued, grant revoked, authentication failed) are
  events; a logging subscriber logs them, and the call site does **not** log them too.
- **Diagnostics / mechanics** (trace "entering X", cache miss, retry, parse detail) are not events; they
  stay ordinary `ILogger` calls at the call site and never route through the seam.

Hierarchical dispatch makes the subscriber side two small, replaceable handlers with zero per-call-site
code: `DefaultEventLoggingHandler : IEventHandler<IEvent>` emits a baseline `Debug`/`Trace` line for every
event, and `DefaultAuditLoggingHandler : IEventHandler<IAuditEvent>` emits structured `Information` audit
logging driven off the envelope, carrying the reserved `EventId`. This centralizes message templates and
`EventId`s in one place — the natural home for the existing `Logging/EventIds.cs` reservations — instead
of scattering them across call sites. Known consequences: a central sink logs under its own category and
sees only the event payload (so anything worth logging belongs **on** the event, not in a local log line),
failures are logged by emitting an outcome=failure audit event rather than a call-site `catch`+log, and the
`HasSubscribers` short-circuit stops firing for audit events once a logging sink is always subscribed —
all expected.

### Instrumentation is a subscriber too

Metrics and distributed traces hang off the **same seam** as logging and audit: the event stream is the
single source for all four signals, so instrumentation is never re-plumbed at call sites. A
`DefaultEventMetricsHandler` (an `IEventHandler<IEvent>`) increments an OpenTelemetry `Meter`
(`events.published`, per-action counters, outcome-tagged failure counts), and events carry the
correlation/trace id so a sink can correlate a record to the originating `Activity`/span. Because sinks
are just handlers, a host adds OpenTelemetry — or Prometheus, or a custom `Meter` — by registering one
more `IEventHandler`, with no change to any publishing site. Event, action, tenant, and outcome names
form a **stable tag/dimension vocabulary**, so the taxonomy that names events also names the metrics.

### A curated event catalog, not an ad-hoc vocabulary

The set of events is a **governed contract**, not something each feature invents in isolation. Action
names follow one dotted taxonomy — `token.issued`, `token.revoked`, `auth.failed`,
`secret.rotated`, `management.resource.changed` — and every event type, its action, and its envelope
fields are listed in a single catalog that ships with the library. The catalog is the reference
consumers build sinks, dashboards, and alerts against, so adding or renaming an event is a deliberate,
reviewed change rather than a silent one. This keeps log categories, metric dimensions, and audit
records drawn from the same stable vocabulary, and it makes the seam's surface discoverable instead of
having to be reverse-engineered from publishing sites.

### Testing: a recording sink

Because sinks are just handlers, tests assert on the seam with a **recording `IEventHandler`** that
captures published events for inspection — no mocking of the publisher, no reaching into logging. A
test registers the recorder, exercises the operation, and asserts that the expected event was published
with the expected envelope (action, outcome, subject, correlation id), hermetically and without timing
dependence on background delivery. This is the canonical way to prove a publishing site emits what it
should, and it belongs alongside the handler conventions so new events arrive with a test that pins
their contract.

### Layering rule

Services and endpoints **orchestrate and compute**; the **mediator** marks result-shaping seams; the
**event publisher** marks observation seams; audit/log/metric sinks are **event handlers**. Publishing
an event is always the **last, side-effect-only** step of a completed operation — never a gate before
it. `SecurityTokenIssuedEvent` is re-homed onto this seam (as an `IAuditEvent`) as the canonical first
example.

## Options considered

- **Reuse the mediator as the event transport (publish events as void `ICommand`).** This is the
  status quo for `SecurityTokenIssuedEvent`, and the mediator's transport is proven and capable. The
  mediator handles dispatch, priority, polymorphic resolution, and pre/post seams well, and its
  rethrow can be suppressed with a catch-all `ICommandExceptionHandler`. Rejected on the audit-specific
  semantics, not on capability: its fan-out root awaits handlers in one loop, so the **first throw
  aborts the rest** — per-handler isolation would require **forking the root wrapper**, not configuring
  it; it is **unsafe by default** (rethrows unless a catch-all is remembered); it has **no hierarchical
  dispatch**, so a universal audit sink cannot subscribe once to a base event type; and modeling pure
  observation as `ICommand` blurs the ADR-0001 line between seams that shape the result and seams that
  only watch it. The event seam reuses the mediator's _patterns_ (priority sort, open-generic pipeline,
  polymorphic resolution) without inheriting these defaults.

- **Leave observation as an ad-hoc strategy collection per concern.** Each feature would define its own
  `IEnumerable<IFooObserver>` and loop over it. Rejected: no shared contract, no shared fault isolation,
  no shared priority or correlation envelope, and no universal audit sink — the exact _incoherence_ this
  decision is meant to remove. The dedicated publisher _is_ the strategy-collection shape, standardized.

- **A publish helper for every event (always wrap `PublishAsync`).** Rejected as a blanket rule: events
  whose payload is fully specified by their constructor gain nothing from a wrapper and pay for it in
  public API surface. Typed helpers are reserved for the audit envelope and short-circuit cases above.

- **A single non-generic handler (`IEventHandler` with `HandleAsync(IEvent)`).** One interface, one flat
  subscriber list, trivial dispatch, no variance concern. Rejected: it offers only the firehose — every
  handler sees every event and must type-test and cast to select its own, with no compiler help — and it
  loses targeted subscription, a meaningful `HasSubscribers<TEvent>()` short-circuit, and discoverability
  of a given event's handlers. The generic `IEventHandler<in TEvent>` already subsumes it: subscribing to
  `IEventHandler<IEvent>` is the "handle everything" case, so the generic form is strictly more
  expressive while staying coherent with the mediator's `ICommandHandler<T>`. Offering both would be two
  ways to do one thing.

- **An external event bus / `MediatR`-style notifications dependency.** Rejected: adds a heavyweight
  third-party dependency for a pattern that is a few dozen lines, and conflicts with the family's
  convention of owning its small extensibility primitives.

- **.NET `event` / `IObservable<T>`.** Rejected: neither composes with DI strategy-collection
  registration, async `ValueTask` handlers, priority ordering, or hierarchical dispatch without
  substantial glue, and `IObservable` pushes consumers toward Rx they otherwise do not need.

- **A dynamic metadata bag on the event contract** (`IDictionary<string, object>`, an `IPropertyBag`, or a
  `[JsonExtensionData]` member on `IEvent`). Rejected: it is the opposite of the typed, immutable,
  cataloged, schema-versioned event this ADR commits to. `[JsonExtensionData]` is for a DTO deserialized
  _from_ an external producer — events are authored and serialized _out_, so there is no upstream field to
  capture; `SchemaVersion` is the right forward/back-compat lever. An `object`-valued bag makes the record
  effectively mutable and reference-equal, is a redaction landmine (arbitrary values can smuggle
  secrets/PII past the never-serialize-secrets rule, worst of all for audit events shipped to external
  sinks), and erodes the curated catalog by inviting magic-string keys instead of reviewed typed fields.
  The house `IPropertyBag` is specifically unsuited here: it holds arbitrary in-process `object` values for
  capability passing, exactly what must not reach a serialized audit record. Event extensibility is
  **subtyping plus the catalog**; if genuinely open-ended audit attributes are ever needed, the only
  sanctioned form is a bounded, immutable `IReadOnlyDictionary<string, string>` on the **audit envelope**
  (serialization-safe, redaction-tractable, complementing — never replacing — typed fields), added when a
  real consumer needs it, not speculatively.

- **A dedicated event seam below the mediator (chosen).** Reaches every layer, isolates observer
  failures by construction, reuses the familiar `TryAddEnumerable` + priority conventions, gives
  auditing a first-class home, and keeps the mediator reserved for result-shaping seams.

## Consequences

- There is one memorable question for contributors — _shapes the outcome, or observes it?_ — extending
  the ADR-0001 ladder with a fourth, narrow shape.
- Any layer can publish meaningful events by injecting `IEventPublisher`; JOSE and Secrets gain an
  audit voice they could not have through the mediator.
- Consumers add auditing, logging, metrics, or external streaming by registering an
  `IEventHandler<…>` — no framework edits, no result-path risk.
- A failing or slow sink cannot break or stall an operation; background delivery absorbs slow sinks.
- Sinks get correctly-scoped dependencies: synchronous delivery binds to the request scope, background
  delivery creates its own scope and uses an application-lifetime cancellation token.
- Best-effort observation and compliance-grade audit are distinct tiers; durable, at-least-once audit
  (outbox, retry, publish-after-commit) is specified separately in
  [ADR-0048](./0048-durable-audit-delivery-outbox.md) so this seam stays small.
- Events are safe to serialize anywhere because they carry identifiers and metadata, never secrets; a
  versioned audit schema lets external sinks evolve independently of in-process API tracking.
- Logging, audit, metrics, and traces all derive from one event stream, so a host adds any signal by
  registering an `IEventHandler` — no publishing site changes.
- The repo now has **two dispatchers** (mediator and event publisher), which is coherent only while the
  litmus stays sharp — _shapes the result_ versus _observes it_. The mitigation is to keep the seam
  minimal and reuse the mediator's proven primitives (priority sort, open-generic pipeline, polymorphic
  resolution) rather than regrow a parallel middleware surface; if the line blurs, the two collapse
  into the redundancy ADR-0001 warns against.
- The event set is a governed catalog with one action taxonomy, so sinks, dashboards, and alerts build
  against a stable vocabulary and adding or renaming an event is a reviewed change.
- Publishing sites are tested with a recording sink that captures events, so each new event arrives with
  a test pinning its contract.
- `SecurityTokenIssuedEvent` moves off the mediator onto the event seam and becomes an `IAuditEvent`;
  this is a breaking change, acceptable under the pre-release posture
  ([ADR-0025](./0025-pre-release-posture-and-auth0-parity-plus.md)) and tracked in
  `PublicAPI.Unshipped.txt`.
- **"You chose wrong" smells:**
    - _Event smell:_ a handler needs to reject the operation, return a value the caller consumes, or
      mutate state the caller then reads → it is a result-shaping seam wearing an event costume; promote
      it to a mediator command.
    - _Mediator smell:_ a void command exists only so observers can log/audit/notify, no handler returns
      or mutates anything the caller consumes, and a throwing handler should not fail the request → it is
      an event wearing a command costume; move it to the event seam (as `SecurityTokenIssuedEvent` does).
    - _Duplication smell:_ a call site both publishes an event and logs the same fact beside it → delete
      the call-site log and let the logging subscriber emit it; keep only non-event diagnostics inline.
    - _Leak smell:_ an event payload carries a raw token, secret, authorization code, or key material →
      replace it with an identifier/hash plus metadata before it reaches any sink.

## References

- [ADR-0001](./0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy-collection
  / mediator ladder this decision extends.
- [ADR-0016](./0016-implementation-packages-depend-only-on-abstractions.md) — abstractions vs.
  implementation package split for `NCode.Identity.Events(.Abstractions)`.
- [ADR-0048](./0048-durable-audit-delivery-outbox.md) — durable, at-least-once audit delivery (outbox,
  retry, publish-after-commit) layered on this seam.
- [ADR-0006](./0006-single-canonical-impl-is-defaultfoo.md) — `DefaultEventPublisher` naming.
- [ADR-0025](./0025-pre-release-posture-and-auth0-parity-plus.md) — breaking-change latitude for
  re-homing `SecurityTokenIssuedEvent`.
- [`src/NCode.Identity.OpenId.Authentication.Abstractions/Tokens/Commands/SecurityTokenIssuedEvent.cs`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Tokens/Commands/SecurityTokenIssuedEvent.cs)
  — the lone pre-existing event, mis-modeled as a mediator command, re-homed as the first worked example.
- `ISupportMediatorPriority` / `DefaultMediatorPriorities` (NCode.Mediator) — the prior art
  `ISupportHandlerPriority` / `DefaultHandlerPriorities` mirror.

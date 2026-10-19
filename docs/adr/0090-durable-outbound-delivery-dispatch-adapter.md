# 90. Durable outbound delivery via a generic job-dispatch adapter (BYO orchestration)

- **Status:** Accepted (amended by [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md): the
  backchannel request-object encryption settings are retired and an encrypted backchannel request is rejected, because
  CIBA Core §7.1.1 defines only an asymmetrically signed request)
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

Several flows must perform an **outbound side effect after** the request that triggered them has returned — and must
keep trying until it succeeds:

- **CIBA ping/push delivery** ([ADR-0085](0085-ciba-grant.md)): when the end user approves a backchannel request on
  their authentication device, the OP must `POST` a completion ping — or, for push, the issued tokens — to the client's
  `client_notification_endpoint`. The original implementation did this **inline on the approval request, best-effort**:
  a failed `POST` was logged and dropped, with no retry and (for push) no way for the client to recover, since a
  push-mode client never polls. That is the gap recorded in backlog `D4`.
- **Back-channel logout notification** ([ADR-0074](0074-session-management-and-logout.md)): the same best-effort,
  single-attempt HTTP `POST` shape, with the same "a failed delivery is silently lost" weakness.
- **Durable audit delivery** ([ADR-0048](0048-durable-audit-delivery-outbox.md)): the proposed transactional outbox
  that drains after commit and retries until each sink acknowledges — the same "reliably perform a deferred side effect,
  surviving a crash" problem, stated for audit.

These are one problem — _reliable, retrying, out-of-band execution of a unit of work_ — solved three times, each inline
and lossy. They also differ in **how** a deployment wants the work run: a single-node dev host is happy with an
in-process background worker; a clustered production deployment wants the work on its existing durable orchestrator (a
transactional outbox, a message broker such as RabbitMQ, or a scheduling cluster such as Quartz) so the work survives a
process crash, is not duplicated across nodes, and is observable in the tooling the operator already runs.

The CIBA code (and, later, logout and audit) should express _"deliver this, reliably"_ **once**, against an
abstraction, and a deployment should choose the execution backend **without touching that code**. This is the
textbook shape for the **adapter (port/adapter) pattern**.

## Decision

**Introduce a small, generic background **job-dispatch** subsystem — `NCode.Identity.Dispatch(.Abstractions)` — whose
single port, `IJobDispatcher`, is the adapter seam for *how* deferred work runs, and route CIBA client delivery through
it as the first consumer.** Ship an **in-process, self-scheduling** default adapter with retry and exponential
back-off; let a deployment replace it wholesale with a durable or distributed backend by registering its own
`IJobDispatcher`, with **no change to the producing code or the job handlers**. Also complete the second half of `D4`
by advertising the CIBA request-object **encryption** capability.

### The dispatch subsystem (generic, not CIBA-specific)

- **`IDispatchJob`** — a marker for a **plain, serializable** unit of work (data only, no behaviour), so a durable or
  distributed adapter can persist, transport, and re-materialize it; the in-process adapter holds it by reference.
- **`IDispatchJobHandler<TJob>`** — executes a job. Returning normally marks the attempt successful; **throwing marks
  it failed and schedules a retry** (up to the job's attempt limit). Handlers must be **idempotent**, keyed on the
  stable `DispatchContext.DispatchId`, because dispatch is **at-least-once** and a job may be redelivered.
- **`IJobDispatcher`** — the **adapter port**: `DispatchAsync<TJob>(job, DispatchOptions?, …)`. It decouples the
  producer from the execution strategy. `DispatchOptions` carries advisory **scheduling/retry** hints (`MaxAttempts`,
  `InitialBackoff`, `BackoffFactor`, `MaxBackoff`, an optional initial `Delay`) that each adapter honors as closely as
  its backend allows.
- **The default adapter — `DefaultInProcessJobDispatcher` + `JobDispatchWorker`** — is the `EventsLibrary`
  background-delivery pattern ([ADR-0047](0047-auditing-and-events-observation-seam.md)) generalized: a bounded
  in-memory channel drained by a hosted `BackgroundService` that resolves the job's handler in a fresh DI scope and
  retries failed attempts with exponential back-off. It is registered through the opt-in
  `AddDispatchServices().AddInProcessJobDispatcher()`, mirroring `AddEventServices().AddBackgroundEventDelivery()`.
- **BYO orchestration is "replace the port".** A deployment registers its own `IJobDispatcher` (and omits
  `AddInProcessJobDispatcher`); the `IDispatchJob` / `IDispatchJobHandler<TJob>` contracts are unchanged, so every
  producer and handler is untouched. Adapter sketches: a **transactional-outbox** adapter writes the serialized job in
  the same transaction as the state change and a dispatcher drains it after commit (this is exactly the
  [ADR-0048](0048-durable-audit-delivery-outbox.md) mechanism, now expressed as one `IJobDispatcher`); a **broker**
  adapter (RabbitMQ/SQS/Kafka) publishes the job and a consumer runs the handler; a **scheduler-cluster** adapter
  (Quartz) persists the job in its JDBC job store and fires it on a worker, honoring `Delay` natively.

### CIBA client delivery routed through the port

- **`CibaDeliveryJob : IDispatchJob`** is a self-contained, serializable description of one client notification: the
  target `client_notification_endpoint`, the `client_notification_token` bearer credential, the `auth_req_id`, the
  delivery mode, and — for push — the issued token fields. **`ICibaRequestService` composes this job at approval time
  and hands it to `IJobDispatcher`** instead of `POST`ing inline; the approval returns as soon as the decision is
  persisted, and delivery happens out of band.
- **`CibaDeliveryJobHandler : IDispatchJobHandler<CibaDeliveryJob>`** performs the HTTP `POST` and **throws on a
  non-success status**, so the dispatcher retries — turning CIBA ping/push from best-effort-single-attempt into
  **at-least-once** delivery (within process lifetime for the in-process adapter; across crashes for a durable adapter).
- **`ICibaNotificationService` is narrowed to the one genuinely deployment-specific, non-HTTP seam** — notifying the
  end user's **authentication device** (`NotifyAuthenticationDeviceAsync`), whose default stays an inert no-op. Its
  former `NotifyClientAsync` / `DeliverTokensAsync` HTTP methods are **removed** (clean cutover): client-facing delivery
  is now the dispatch job.

### Request-object encryption advertisement (the second half of `D4`)

New server settings `backchannel_authentication_request_encryption_alg_values_supported` /
`…_enc_values_supported` / `…_zip_values_supported` join the discovery catalog next to the existing
`…_signing_alg_values_supported`, defaulting to the same asymmetric encryption sets as request objects and merging as a
narrowing **Intersect** ceiling. The backchannel request-object loader now enforces them: an **encrypted (JWE)** request
object — identified by the `enc` header — must use an advertised key-management `alg`, content-encryption `enc`, and
(if present) `zip`; a **signed (JWS)** one must use an advertised signing `alg`. This also fixes a latent bug where a
JWE's key-management `alg` was previously checked against the _signing_ set.

> **Correction (conformance triage, 2026-10-15).** The `alg` / `enc` variants are **no longer advertised in discovery**
> (now marked non-discoverable, matching the already-non-discoverable `zip` variant): CIBA Core defines no encryption
> metadata for the backchannel request, so advertising them is a non-standard parameter the OpenID Foundation
> Conformance Suite flags (`CheckForUnexpectedParametersInServerMetadata`). The settings and their **enforcement** are
> unchanged — the encrypted-CIBA-request capability still works; it is simply not advertised via a non-standard
> parameter. See [`docs/conformance/external-suite.md`](../conformance/external-suite.md).

## Options considered

- **Keep delivery inline and best-effort (status quo).** Rejected: it is lossy by construction (a failed push is
  unrecoverable), and it bakes the execution strategy into the flow, so a deployment cannot make it durable without
  editing CIBA code.
- **A CIBA-specific durable delivery service** (e.g. `ICibaDeliveryDispatcher`). Rejected for cohesion: logout and
  audit need the identical "reliably run deferred work with a pluggable backend" capability, so a CIBA-only seam would
  be re-invented two more times. A **generic** dispatch port is the comprehensive, long-term shape — and the user
  explicitly asked for a generic, not CIBA-only, scheduler.
- **A delegate-carrying job (an in-process `Func` on the work item).** Rejected: a captured delegate cannot be
  serialized, persisted, or sent to a broker/scheduler, so it forecloses every durable or distributed adapter. The
  job-as-data + handler-keyed-by-type model (the mediator/Hangfire shape) is what makes the port adapter-agnostic.
- **Mint tokens inside the handler at delivery time instead of at approval.** Rejected for now: it would require
  reconstructing a full request context (tenant, client, grant) on the worker, far from a request. Composing the token
  response at approval and carrying it in the job keeps the job self-contained and serializable. The accepted cost: a
  push job carries issued tokens, so **a durable adapter that persists the job must protect the payload at rest**, and a
  long retry window could deliver a near-expiry token — both documented, both acceptable for the in-process default.
- **Build the full durable outbox now ([ADR-0048](0048-durable-audit-delivery-outbox.md)).** Deferred, but **unified**:
  rather than a parallel outbox mechanism, ADR-0048's transactional outbox becomes **one `IJobDispatcher` adapter**.
  This ADR ships the port plus the in-process default; the durable adapter lands when a concrete need (compliance audit,
  guaranteed push) justifies the persistence and migration cost.

## Consequences

- CIBA ping/push delivery is **retried with back-off** and is **pluggable**: a deployment that needs at-least-once
  across crashes registers a durable `IJobDispatcher` and changes nothing else. Backlog `D4` is closed (delivery
  durability **and** encryption advertisement).
- A new, small, reusable subsystem (`NCode.Identity.Dispatch(.Abstractions)`) exists for *any* deferred, reliable work.
  **Back-channel logout ([ADR-0074](0074-session-management-and-logout.md)) already adopts it** — its default notifier
  builds the signed `logout_token` and dispatches a delivery job instead of `POST`ing inline — and the durable audit
  outbox ([ADR-0048](0048-durable-audit-delivery-outbox.md)) is now framed as one adapter of this port rather than a
  separate mechanism.
- **The in-process default is honest about its limits:** it survives transient handler failures (retry/back-off) but
  **not a process crash** (an in-memory queue is lost on exit) and does **not** coordinate across nodes. Crash-durable,
  cluster-safe at-least-once is the job of a durable adapter — the whole reason the port exists. A full queue sheds the
  newest write (bounded, best-effort back-pressure), logged for the operator.
- Public surface churn (greenfield, [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)): two new packages;
  `ICibaNotificationService` loses its two HTTP methods; and four new CIBA encryption settings. The concrete delivery
  jobs (`CibaDeliveryJob`, `BackChannelLogoutDeliveryJob`) are **internal** implementation detail of the producing
  package — the dispatcher is generic over `IDispatchJob`, so no job type is part of the public surface.

## References

- [ADR-0085](0085-ciba-grant.md) — the CIBA grant whose push/ping delivery this makes durable and whose request-object
  encryption advertisement this completes (closing backlog `D4`).
- [ADR-0048](0048-durable-audit-delivery-outbox.md) — the durable-audit outbox, now unified as a durable `IJobDispatcher`
  adapter rather than a parallel mechanism.
- [ADR-0074](0074-session-management-and-logout.md) — back-channel logout, now a consumer of the dispatch port (its
  default notifier dispatches a delivery job rather than `POST`ing inline).
- [ADR-0047](0047-auditing-and-events-observation-seam.md) — the events background-delivery pattern the in-process
  dispatcher generalizes.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service-vs-strategy extensibility model the
  job/handler split follows.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the greenfield posture that makes the clean cutover
  (removing the notifier's HTTP methods) preferable to a shim.
- [OpenID Connect CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html) §10.

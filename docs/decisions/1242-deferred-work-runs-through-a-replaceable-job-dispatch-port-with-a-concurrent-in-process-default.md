# 1242. Deferred work runs through a replaceable job-dispatch port with a concurrent in-process default

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group
- **Conflicts with:** [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md), tracked in [#25](https://github.com/NCodeGroup/identity/issues/25)

## Context

Several flows perform an outbound side effect after the request that triggered them has returned, and keep trying until
it succeeds:

- the CIBA ping and push delivery to a client's `client_notification_endpoint`
  ([ADR-1085](1085-ciba-grant.md)); a push-mode client never polls, so a lost push is unrecoverable;
- the back-channel logout notification ([ADR-1074](1074-session-management-and-logout.md));
- the key maintenance that runs in the background: the rewrap of data keys under a new master key and the sweep of
  remote keys that no stored secret names.

These are one problem, reliable and retrying out-of-band execution of a unit of work. They also differ in how a
deployment wants the work run: a single-node development host is content with an in-process worker, while a clustered
production deployment wants its existing durable orchestrator (a transactional outbox, a broker, a scheduler cluster),
so that the work survives a crash, is not duplicated across nodes and is observable in the operator's tooling. The
producers should say "deliver this, reliably" once, against an abstraction, and a deployment should choose the
execution backend without touching them.

Three properties of an in-process worker make the behavior of one endpoint the problem of every delivery:

- A worker that runs one job at a time is held by an endpoint that does not answer until the HTTP client's timeout, and
  every job queued behind it waits that long.
- The timeout surfaces as a `TaskCanceledException` whose token is not the worker's. A loop that treats every
  `OperationCanceledException` as its own shutdown ends on it, and `BackgroundServiceExceptionBehavior.StopHost`, the
  host's default, turns an ended worker into a stopped host.
- A job type may have several handlers. If each failing handler scheduled its own retry, one failed attempt would run
  again as many times as handlers failed.

The configuration binder also reads an enum from a number, and accepts a number that no member has: `"7"` binds
`BoundedChannelFullMode` to a value that `BoundedChannelOptions` refuses when the queue is built.

The durable adapter that project-template ADR-0141 asks for, an outbox written in the transaction of the change, is not
built. The in-process default writes no outbox row with the change and loses a queued job on a crash, and a CIBA push
job carries issued tokens. The gap is tracked in issue [#25](https://github.com/NCodeGroup/identity/issues/25).

## Decision

**Deferred work runs through one port, `IJobDispatcher`, that a deployment replaces by registering its own; the default
adapter runs jobs in process, concurrently up to a limit, and its options bind from `OpenId:InProcessJobDispatcher`.**

### The dispatch subsystem

`NCode.Identity.Dispatch(.Abstractions)` is a generic job-dispatch subsystem, not a CIBA one:

- `IDispatchJob` marks a plain, serializable unit of work, data only and no behavior, so a durable or distributed
  adapter can persist, transport and re-materialize it. The in-process adapter holds it by reference.
- `IDispatchJobHandler<TJob>` runs a job. Returning marks the attempt successful, and throwing fails it. Dispatch is at
  least once, so a handler is idempotent on `DispatchContext.DispatchId`.
- `IJobDispatcher.DispatchAsync<TJob>(job, DispatchOptions?, …)` is the adapter port. `DispatchOptions` carries advisory
  hints that each adapter honors as closely as its backend allows: `MaxAttempts` (5), `InitialBackoff` (2 s),
  `BackoffFactor` (2.0), `MaxBackoff` (5 min) and an initial `Delay`.
- A deployment replaces the port by registering its own `IJobDispatcher` (a transactional outbox, a broker such as
  RabbitMQ, a scheduler cluster such as Quartz) and omitting the in-process adapter. Producers and handlers do not
  change. An outbox adapter writes the serialized job with the state change and drains it after commit; a broker
  adapter publishes the job and a consumer runs the handler; a scheduler adapter persists the job and fires it on a
  worker, honoring `Delay` natively.
- A job type is internal to the package that produces it, so no job type is part of the public surface.

### The consumers

- Back-channel logout: `BackChannelLogoutDeliveryJob`. The `logout_token` is signed before dispatch.
- CIBA ping and push: `CibaDeliveryJob`, a self-contained description of one client notification (the endpoint, the
  `client_notification_token`, the `auth_req_id`, the delivery mode and, for push, the issued token fields). The
  approval composes it and hands it to the dispatcher, then returns as soon as the decision is stored. Its handler
  performs the `POST` and throws on a non-success status. `ICibaNotificationService` keeps only
  `NotifyAuthenticationDeviceAsync`, the one deployment-specific, non-HTTP seam.
- `MasterKeyRewrapJob` and `RemoteKeySweepJob`, the key maintenance of the Secrets library.

A push job carries the issued tokens, because composing the token response at approval keeps the job serializable
without rebuilding a request context on a worker. A durable adapter that persists the job protects that payload at
rest.

### The in-process default

`AddDispatchServices().AddInProcessJobDispatcher()` registers `DefaultInProcessJobDispatcher`, `JobDispatchWorker` and
`InProcessJobQueue`, and `AddIdentityServer()` calls it.

1. **Consumers share one bounded channel.** `JobDispatchWorker` starts `MaxConcurrentJobs` consumer loops over the one
   bounded channel of `InProcessJobQueue`, which has several readers (`SingleReader = false`). Each consumer runs one
   job at a time, so a job that waits holds one consumer while the others drain the queue.
2. **A failed attempt is any error while the host is not stopping.** An attempt fails when resolving or running the
   job's handlers throws and the stopping token is not cancelled, a handler's own `OperationCanceledException` from its
   timeout included. Only a cancelled stopping token ends a consumer, quietly, so a timeout never stops the host.
3. **One retry per attempt.** A job's handlers run in sequence, and the failure of one does not skip the others. The
   attempt reschedules the job once, with exponential back-off, however many handlers failed. The job is abandoned and
   logged when its attempts are exhausted.
4. **Handlers resolve in an async scope.** Each attempt creates one `CreateAsyncScope` and resolves every handler of the
   job from it, so a scoped handler binds its dependencies per attempt.

### The options

`InProcessJobDispatcherOptions` has `QueueCapacity` (1024), `MaxConcurrentJobs` (16) and `FullMode`
(`BoundedChannelFullMode`, `Wait`).

- **They bind from the OpenID section.** `AddConfiguration` recognizes `InProcessJobDispatcher` among the subsections of
  the OpenID section, so a misspelled subsection stops the host and a host's own section name moves it. It binds with
  `BindStrictly` and `ValidateOnStart`
  ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
  `InProcessJobDispatcherOptions.SectionName` is the constant `"InProcessJobDispatcher"`, and an environment variable
  sets a key as `OpenId__InProcessJobDispatcher__MaxConcurrentJobs`.
- **Validation names the key.** `InProcessJobDispatcherOptionsValidator` refuses a `QueueCapacity` or a
  `MaxConcurrentJobs` below 1 and a `FullMode` that names no `BoundedChannelFullMode` member, naming each failing option
  and its key relative to the OpenID section. For example, a capacity of zero fails with:

  ```text
  InProcessJobDispatcherOptions.QueueCapacity ('InProcessJobDispatcher:QueueCapacity') must be at least 1; it is 0.
  ```

  A name that no member has stops the binding, a number that no member has (`"7"`) fails the validator, and a number
  that a member has (`"1"`) is accepted, as the binder accepts it. A queue that holds no job and a worker that runs none
  stop the host, instead of dropping or never running the dispatched jobs.
- **The defaults are the library's.** `appsettings*.json` and the Helm chart carry no dispatcher key, as they carry no
  tuning key of the other options under the OpenID section. A `Configure<InProcessJobDispatcherOptions>` that a host
  registers after `AddConfiguration` runs after the binding and wins.

### What stays outside the port

The durable adapter is unbuilt and tracked in issue [#25](https://github.com/NCodeGroup/identity/issues/25): a
transactional outbox that writes the serialized job with the change and drains it after commit, at least once, for
compliance-grade audit and guaranteed push. The management audit records publish inline on their own bounded token
([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)), and the event seam stays
best effort ([ADR-1047](1047-auditing-and-events-observation-seam.md)).

## Options considered

- **Inline best-effort delivery.** Rejected: it is lossy by construction, since a failed push is unrecoverable, and it
  bakes the execution strategy into the flow, so a deployment cannot make it durable without editing the producer.
- **A CIBA-specific delivery service.** Rejected: logout and key maintenance need the same capability with a pluggable
  backend, so a CIBA-only seam would be built again for each.
- **A delegate-carrying job.** Rejected: a captured delegate cannot be serialized, persisted or sent to a broker, which
  forecloses every durable or distributed adapter. A job as data, run by a handler keyed on its type, keeps the port
  adapter-agnostic.
- **Minting the tokens at delivery time.** Rejected: it needs a full request context (tenant, client, grant) on a
  worker, far from the request. The cost of composing at approval is that a push job carries tokens, which a durable
  adapter protects at rest.
- **`Parallel.ForEachAsync` over the channel.** Rejected: an exception from one body cancels every running job.
- **A task per job under a semaphore.** Rejected: it gives the same bound with more bookkeeping.
- **A shorter HTTP timeout only.** Rejected: it shortens the wait but not its cause. A delivery still holds the only
  worker, and a timeout still ends a worker that treats it as shutdown.
- **A write-ahead audit that fails the request.** Rejected: the dispatcher is for out-of-band work, and a failed
  delivery must not fail the request that triggered it.
- **A top-level section, or a binding in the dispatch package.** Rejected: the server composes the dispatcher and owns
  the shape of the OpenID section
  ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
- **A generic port with an in-process default that runs jobs concurrently, configured under the OpenID section.**
  Chosen.

## Consequences

- CIBA ping and push and back-channel logout are retried with back-off and are pluggable: a deployment that needs
  at-least-once across crashes registers a durable `IJobDispatcher` and changes nothing else.
- The in-process default survives transient handler failures but not a process crash, because an in-memory queue is lost
  on exit, and it does not coordinate nodes. This is the conflict with project-template ADR-0141 that issue
  [#25](https://github.com/NCodeGroup/identity/issues/25) closes with the durable adapter, whose stored job carries no
  token it could read again.
- A slow endpoint delays only its own deliveries while fewer than `MaxConcurrentJobs` jobs wait on slow endpoints at
  once. When every consumer waits, the queue waits with them, and with `Wait` a producer waits for room.
- Jobs finish out of dispatch order, while one job's handlers run in sequence. A job that needs another to finish first
  arranges it itself.
- A handler registered as a singleton is thread-safe, because several consumers can run it at once. The built-in
  handlers are scoped.
- A replica sends up to `MaxConcurrentJobs` deliveries at once, so a deployment sizes the limit against the endpoints it
  delivers to and the outbound connections it can hold.
- A handler's timeout is a logged, retried attempt that ends neither the consumer nor the host. A retry runs every
  handler of the job again, which is why each handler is idempotent.
- An operator tunes the queue and the concurrency without code, and a misspelled key or an invalid value stops the host
  when it starts and names the key.

## References

- Code: `NCode.Identity.Dispatch.Abstractions.IJobDispatcher`, `IDispatchJob`, `IDispatchJobHandler<TJob>`,
  `DispatchOptions`, `DispatchContext`; `NCode.Identity.Dispatch.InProcess.DefaultInProcessJobDispatcher`,
  `JobDispatchWorker`, `InProcessJobQueue`, `InProcessJobDispatcherOptionsValidator`;
  `NCode.Identity.Dispatch.Options.InProcessJobDispatcherOptions`;
  `NCode.Identity.Server.IdentityServerBuilderExtensions`.
- Issue [#25](https://github.com/NCodeGroup/identity/issues/25), the durable adapter, and
  [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md).
- [ADR-1085](1085-ciba-grant.md) — the CIBA grant whose delivery the dispatcher carries.
- [ADR-1074](1074-session-management-and-logout.md) — the back-channel logout notification the dispatcher delivers.
- [ADR-1047](1047-auditing-and-events-observation-seam.md) — the events background-delivery pattern the in-process
  dispatcher generalizes.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) —
  configuration binds strictly and is validated when the host starts.
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) — the inline, bounded
  publication of management audit records.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md) — the
  service-versus-strategy model the job and handler split follows.

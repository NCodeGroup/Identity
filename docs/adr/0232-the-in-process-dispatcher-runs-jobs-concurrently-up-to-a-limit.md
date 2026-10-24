# 232. The in-process dispatcher runs jobs concurrently up to a limit

- **Status:** Accepted (amended by
  [ADR-0242](0242-the-in-process-dispatcher-is-configured-under-the-openid-section.md): the options bind from
  `OpenId:InProcessJobDispatcher`, and a `FullMode` that names no member stops the host)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The in-process dispatcher ([ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md)) delivers the back-channel
logout notifications ([ADR-0074](0074-session-management-and-logout.md)) and the CIBA ping and push notifications
([ADR-0085](0085-ciba-grant.md)) to the endpoints that clients register, and it retries a failed delivery with back-off.
Three properties of its worker (backlog `R85`) make the behavior of one endpoint the problem of every delivery:

- A worker that runs one job at a time is held by an endpoint that does not answer until the HTTP client's timeout, and
  every job queued behind the delivery waits that long.
- The timeout surfaces as a `TaskCanceledException` whose token is not the worker's. A loop that treats every
  `OperationCanceledException` as its own shutdown ends on it, and `BackgroundServiceExceptionBehavior.StopHost`, the
  host's default, turns an ended worker into a stopped host.
- A job type may have several handlers. If each failing handler scheduled its own retry, one failed attempt would run
  again as many times as handlers failed.

## Decision

**The in-process dispatcher runs up to `InProcessJobDispatcherOptions.MaxConcurrentJobs` jobs at once, and an attempt
that fails for any reason but the host stopping is retried once.**

1. **Consumers share one bounded channel.** `JobDispatchWorker` starts `MaxConcurrentJobs` consumer loops over the one
   bounded channel of `InProcessJobQueue`, which has several readers (`SingleReader = false`). Each consumer runs one
   job at a time, so a job that waits holds one consumer while the others drain the queue. The default is 16.
2. **The options are validated when the host starts.** `InProcessJobDispatcherOptionsValidator` refuses a
   `MaxConcurrentJobs` and a `QueueCapacity` below 1 and a `FullMode` that names no member of
   `BoundedChannelFullMode`, naming the configuration key of each, and `AddInProcessJobDispatcher` registers it with
   `ValidateOnStart`; `AddConfiguration` binds the options from `OpenId:InProcessJobDispatcher`
   ([ADR-0242](0242-the-in-process-dispatcher-is-configured-under-the-openid-section.md)). A queue that holds no job
   and a worker that runs none stop the host, instead of dropping or never running the dispatched jobs.
3. **A failed attempt is any error while the host is not stopping.** An attempt fails when resolving or running the
   job's handlers throws and the stopping token is not cancelled, an `OperationCanceledException` from a handler's own
   timeout included. Only a cancelled stopping token ends a consumer, quietly, and the jobs in flight end with the host.
4. **One retry per attempt.** A job's handlers run in sequence, and the failure of one does not skip the others. The
   attempt reschedules the job once, with the first failure, however many handlers failed. The job is abandoned, and
   logged, when its attempts are exhausted.
5. **Handlers resolve in an async scope.** Each attempt creates one `CreateAsyncScope` and resolves every handler of the
   job from it, so a scoped handler binds its dependencies per attempt and the scope disposes asynchronously.

## Options considered

- **`Parallel.ForEachAsync` over the channel.** Rejected: an exception from one body cancels every running job.
- **A task per job, bounded by a semaphore.** Rejected: it gives the same bound with more bookkeeping, for the tasks and
  for the semaphore.
- **A shorter HTTP timeout only.** Rejected: it shortens the wait but not its cause. A delivery still holds the only
  worker, and a timeout still ends a worker that treats it as shutdown.
- **A durable adapter** (backlog `D5`). Deferred: it answers a delivery that survives a restart, not a slow endpoint
  that holds the in-process worker. A deployment that needs it registers its own `IJobDispatcher`
  ([ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md)).
- **Options set only in code.** Rejected: an operator tunes the dispatcher through configuration, which
  `AddConfiguration` binds from `OpenId:InProcessJobDispatcher`
  ([ADR-0242](0242-the-in-process-dispatcher-is-configured-under-the-openid-section.md)).

## Consequences

- A slow endpoint delays only its own deliveries while fewer than `MaxConcurrentJobs` jobs wait on slow endpoints at
  once. When every consumer waits, the queue waits with them.
- Jobs finish out of dispatch order, while one job's handlers still run in sequence. A job that needs another to
  finish first arranges it itself.
- A handler registered as a singleton must be thread-safe, because several consumers can run it at once. The built-in
  handlers are scoped.
- A replica sends up to `MaxConcurrentJobs` deliveries at once, so a deployment sizes the limit against the endpoints
  it delivers to and the outbound connections it can hold.
- A handler's timeout is a logged, retried attempt. It ends neither the consumer nor the host.
- A retry runs every handler of the job again, so each handler is idempotent, as
  [ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md) requires.
- This implements backlog `R85`.

## References

- [ADR-0074](0074-session-management-and-logout.md) — the back-channel logout notification the dispatcher delivers.
- [ADR-0085](0085-ciba-grant.md) — the CIBA delivery the dispatcher carries.
- [ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md) — the job-dispatch port and the in-process adapter.
- Code: `NCode.Identity.Dispatch.InProcess.JobDispatchWorker`, `NCode.Identity.Dispatch.InProcess.InProcessJobQueue`,
  `NCode.Identity.Dispatch.Options.InProcessJobDispatcherOptions`,
  `NCode.Identity.Dispatch.InProcess.InProcessJobDispatcherOptionsValidator`.

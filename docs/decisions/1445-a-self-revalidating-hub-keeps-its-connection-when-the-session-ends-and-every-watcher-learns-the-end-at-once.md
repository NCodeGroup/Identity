# 1445. A self-revalidating hub keeps its connection when the session ends, and every watcher learns the end at once

- **Status:** Accepted (amends
  [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md) decisions 2
  and 3)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A Blazor circuit signs out through its authentication state provider, which waits on `IBffConnectionRevalidator`
([ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md)). Four facts
shape how a connection learns that its session ended:

- A circuit whose connection closes drops into reconnection, and reconnection fails once the session cookie ended. The
  circuit never signs out, and the browser shows a failed reconnection instead of a signed-out page.
- Every Blazor event, render acknowledgement and navigation is a hub invocation. A hub filter that fails the
  invocations of an ended connection freezes a circuit that is signing out.
- The invocation, the idle watch, the circuit's provider and a refresh that ends the session each find the end alone.
  The others learn it at their own next `RevalidationInterval`, up to one interval later.
- A token request through a connection whose session ended must fail whichever caller finds the end first.

## Decision

**A connection session latches the end of its session, every watcher learns the end at once, and an invocation that
finds the end closes the connection only for a hub that does not sign its user out itself.**

1. **The connection session latches its end.** `IBffConnectionSession.SessionEnded` is a `CancellationToken` that
   `DefaultBffConnectionSession` cancels once a use of the connection finds the session ended: a validation or a token
   request whose resolver returns no session or whose frontend is gone, or a refresh that ends the session. From then
   on the connection session reports the session ended without reading the store. It cancels without running its
   watchers inside the caller, and `IsCancellationRequested` is set before the call returns.
2. **The revalidator returns as soon as the end is signaled.** `DefaultBffConnectionRevalidator.WaitUntilEndedAsync`
   returns at once when `SessionEnded` is cancelled, and when it becomes cancelled while the revalidator waits. The hub
   filter's idle watch and the circuit's provider learn any finding of the end immediately.
3. **An invocation that finds the end fails, and closes only some connections.** `BffHubFilter` throws a `HubException`
   when an invocation's revalidation finds the session ended. It aborts the connection only when the hub lacks
   `[BffSelfRevalidatingHub]`. A hub with the attribute keeps its connection, so its circuit signs out and stays
   connected.
4. **After the end, a self-revalidating hub's invocations run without the session.** When the connection session had
   already ended before an invocation, a hub without `[BffSelfRevalidatingHub]` aborts and throws as in decision 3. A
   hub with the attribute runs the invocation without revalidating and without making the connection current, because
   its application has been told and signs its user out. Any token request through the connection fails, because the
   latch holds.

## Options considered

- **Keep aborting every hub.** Rejected: a circuit whose connection closes drops into reconnection, which fails, so it
  never signs out.
- **Fail every invocation of an ended self-revalidating connection.** Rejected: render acknowledgements, navigation and
  events are invocations, so a circuit that is signing out freezes.
- **Let the invocation that finds the end run.** Rejected: it would act for a session that ended.
- **A Blazor-only callback.** Rejected: the hub filter, the idle watch, a refresh and the circuit would keep separate
  rules for one end, and a hub that is not Blazor would not learn it.

## Consequences

- A Blazor circuit signs out and stays connected when its session ends.
- Any finding of the end reaches every watcher of the connection at once, and not up to one `RevalidationInterval`
  later.
- The action that the invocation finding the end carried is dropped, and the caller receives a `HubException`.
- The connection session holds a `CancellationTokenSource` without a timer, which holds nothing to release and lives as
  long as its principal.
- A self-revalidating hub's later invocations run with no session, so an application that has not signed its user out
  must not rely on the session from then on.

## References

- [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md),
  [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md)
- `src/NCode.Identity.Bff/Hubs/BffHubFilter.cs`, `src/NCode.Identity.Bff/Sessions/DefaultBffConnectionSession.cs`
- Issue [#613](https://github.com/NCodeGroup/identity/issues/613)

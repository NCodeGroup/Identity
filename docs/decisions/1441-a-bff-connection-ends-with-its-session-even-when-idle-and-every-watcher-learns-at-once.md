# 1441. A BFF connection ends with its session even when idle, and every watcher learns at once

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A hub connection holds its session for as long as it stays open
([ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-opt-in-packages.md)).
Several facts shape how that connection ends with the session:

- A hub that only pushes never receives an invocation. A check that runs on invocations leaves its connection open
  after the session ended, and the connection keeps whatever the hub pushes to it.
- A Blazor circuit signs out through its authentication state provider. A circuit whose connection closes drops into
  reconnection, and reconnection fails once the session cookie ended, so the circuit never signs out and the browser
  shows a failed reconnection instead of a signed-out page.
- Every Blazor event, render acknowledgement and navigation is a hub invocation. A hub filter that fails the
  invocations of an ended connection freezes a circuit that is signing out.
- The invocation, the idle watch, the circuit's provider and a refresh that ends the session each find the end alone.
  Without a shared signal the others learn it at their own next `RevalidationInterval`, up to one interval later.
- A token request through a connection whose session ended must fail whichever caller finds the end first.
- The behaviors that matter here are the ones only a real browser and a real server show: a circuit that signs out
  after back-channel logout, a browser `WebSocket` from another origin, a hub connection that is idle, and a
  WebAssembly component's traffic. A unit test cannot show a circuit, a browser `WebSocket` or a WebAssembly runtime.

## Decision

**One revalidation loop ends every hub connection with its session, idle or not; the connection session latches the
end so every watcher learns it at once; an invocation that finds the end closes the connection only for a hub that
does not sign its user out itself; and a Blazor Web App in the end-to-end suite proves the behavior in real
browsers.**

1. **One revalidator.** `IBffConnectionRevalidator` (`NCode.Identity.Bff.Abstractions`) has one method,
   `WaitUntilEndedAsync(connection, cancellationToken)`, which completes once the connection's session ended.
   `DefaultBffConnectionRevalidator` implements it. It waits `RevalidationInterval`, then validates the session without
   sliding it, so the idle timeout still ends the session of an idle connection, and a connection ends within one
   interval after its session. It returns when the session ended. It logs and retries while the session store is
   unavailable, and it logs and returns on any other failure, so it fails closed. It throws
   `OperationCanceledException` when cancelled.
2. **The connection session latches its end.** `IBffConnectionSession.SessionEnded` is a `CancellationToken` that
   `DefaultBffConnectionSession` cancels once a use of the connection finds the session ended: a validation, a token
   request whose resolver returns no session or whose frontend is gone, or a refresh that ends the session. From then
   on the connection session reports the end without reading the store. It cancels without running its watchers inside
   the caller, and `IsCancellationRequested` is set before the call returns.
3. **The revalidator returns as soon as the end is signaled.** `DefaultBffConnectionRevalidator.WaitUntilEndedAsync`
   returns at once when `SessionEnded` is or becomes cancelled, so the hub filter's watch, the circuit's provider and
   the invocations learn any finding of the end immediately.
4. **The hub filter watches every connection of an ordinary hub.** For a hub without `[BffSelfRevalidatingHub]`,
   `BffHubFilter` starts a watch on the revalidator after `OnConnectedAsync` succeeds. It keeps the watch's
   `CancellationTokenSource` in `HubCallerContext.Items`, cancels it in `OnDisconnectedAsync`, and aborts the
   connection when the revalidator returns. A revalidator that throws is logged, and the connection is aborted.
5. **An invocation revalidates, and closes only some connections.** An invocation revalidates once the interval has
   passed and slides the session. When it finds the session ended, `BffHubFilter` throws a `HubException`, and it
   aborts the connection only when the hub lacks `[BffSelfRevalidatingHub]`.
6. **A hub can keep its own loop.** `[BffSelfRevalidatingHub]` (`NCode.Identity.Bff.Abstractions`) marks a hub whose
   application signs the user out itself when the session ends. `AsBffComponents()` puts it on the Blazor hub and its
   negotiate endpoint, so a circuit signs out and stays connected instead of dropping into a failing reconnection. The
   circuit's authentication state provider waits on the same revalidator and signs the circuit out.
7. **After the end, a self-revalidating hub's invocations run without the session.** When the connection session had
   already ended before an invocation, a hub with the attribute runs the invocation without revalidating and without
   making the connection current, because its application has been told and signs its user out. Every token request
   through the connection fails, because the latch holds.
8. **Support projects.** The Blazor apps are support projects
   ([ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
9. **A Blazor Web App in the suite.** The suite hosts `NCode.Identity.Bff.EndToEnd.BlazorApp` and its
   `NCode.Identity.Bff.EndToEnd.BlazorApp.Client` through `WebApplicationFactory<TEntryPoint>.UseKestrel()` as Keycloak
   client `bff-blazor`, with a `Session:RevalidationInterval` of 2 seconds. Each test is a theory over
   `BrowserNames.All` and asserts a counter of the host beside the browser's outcome. In Chromium, Firefox and WebKit
   the suite proves that a circuit signs out after back-channel logout, that a `WebSocket` from another origin is
   refused, that an idle hub connection closes once its session ended, and that a WebAssembly component's traffic
   carries no token.

## Options considered

- **Revalidate only on invocations.** Rejected: an idle connection outlives its session.
- **A loop per caller.** Rejected: the hub filter, the idle watch, a refresh and the circuit would keep separate copies
  of one rule, and a hub that is not Blazor would not learn the end.
- **Abort the Blazor hub's connection.** Rejected: the circuit drops into reconnection, which fails, so it never signs
  out.
- **Fail every invocation of an ended self-revalidating connection.** Rejected: render acknowledgements, navigation and
  events are invocations, so a circuit that is signing out freezes.
- **Let the invocation that finds the end run.** Rejected: it would act for a session that ended.
- **A Blazor-only callback.** Rejected: a hub that is not Blazor would not learn the end, and the callback would be a
  second rule for one end.
- **Name support projects `.Tests`.** Rejected: the gates would run them as test projects.
- **Host the app with `WebApplication.CreateBuilder` in the test process.** Rejected: the static web assets of the app
  resolve only through its own manifest, which `WebApplicationFactory` locates.

The chosen design is one revalidator over a latched end, with the connection closing only for a hub that does not
sign out itself.

## Consequences

- A hub connection ends with its session within one `RevalidationInterval` after the session ended, whether the hub
  receives invocations or not.
- A hub that only pushes needs no code of its own to stop pushing to an ended session.
- The watch never slides the session, so an idle connection cannot keep a session alive.
- A store outage keeps a connection open, and the watch retries at the next interval.
- A Blazor circuit signs out and stays connected when its session ends.
- Any finding of the end reaches every watcher of the connection at once, and not up to one `RevalidationInterval`
  later.
- The action that the invocation finding the end carried is dropped, and the caller receives a `HubException`.
- The connection session holds a `CancellationTokenSource` without a timer, which holds nothing to release and lives as
  long as its principal.
- A self-revalidating hub's later invocations run with no session, so an application that has not signed its user out
  must not rely on the session from then on.
- The suite's Blazor host means a change to a render mode, to the Blazor packages or to the hub filter runs against
  Chromium, Firefox and WebKit in the end-to-end gate.

## References

- [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md),
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-opt-in-packages.md)
- `src/NCode.Identity.Bff/Hubs/BffHubFilter.cs`, `src/NCode.Identity.Bff/Sessions/DefaultBffConnectionSession.cs`
- Issues [#603](https://github.com/NCodeGroup/identity/issues/603),
  [#604](https://github.com/NCodeGroup/identity/issues/604) and
  [#613](https://github.com/NCodeGroup/identity/issues/613)

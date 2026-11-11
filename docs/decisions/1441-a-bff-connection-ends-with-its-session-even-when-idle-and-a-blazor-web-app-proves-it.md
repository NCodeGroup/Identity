# 1441. A BFF connection ends with its session even when idle, and a Blazor Web App proves it

- **Status:** Accepted (amends
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md)
  decision 4 and
  [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)
  decision 1; decisions 2 and 3 amended by
  [ADR-1445](1445-a-self-revalidating-hub-keeps-its-connection-when-the-session-ends-and-every-watcher-learns-the-end-at-once.md):
  an invocation of a self-revalidating hub that finds the session ended fails without closing the connection, and every
  watcher of a connection learns the end at once)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A hub connection holds its session for as long as it stays open
([ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md)).
Three facts shape how that connection ends with the session:

- A hub that only pushes never receives an invocation. A check that runs on invocations leaves its connection open
  after the session ended, and the connection keeps whatever the hub pushes to it.
- A Blazor circuit revalidates its connection in its own loop and signs the circuit out when the session ended. A loop
  per caller is a loop to keep in step.
- The behaviors that matter here are the ones only a real browser and a real server show: a circuit that signs out
  after back-channel logout, a browser `WebSocket` from another origin, a hub connection that is idle, and a
  WebAssembly component's traffic. A unit test cannot show a circuit, a browser `WebSocket` or a WebAssembly runtime.

## Decision

**One revalidation loop ends every hub connection with its session, idle or not, and a Blazor Web App in the
end-to-end suite proves the behavior in real browsers.**

1. **One revalidator.** `IBffConnectionRevalidator` (`NCode.Identity.Bff.Abstractions`) has one method,
   `WaitUntilEndedAsync(connection, cancellationToken)`, which completes once the connection's session ended.
   `DefaultBffConnectionRevalidator` implements it. It waits `RevalidationInterval`, then validates the session without
   sliding it, so the idle timeout still ends the session of an idle connection. It returns when the session ended. It
   logs and retries when the session store is unavailable, and it logs and returns on any other failure, so it fails
   closed. It throws `OperationCanceledException` when cancelled.
2. **The hub filter watches every connection.** Once a connection's `OnConnectedAsync` succeeds, `BffHubFilter` starts
   a watch on the revalidator. It keeps the watch's `CancellationTokenSource` in `HubCallerContext.Items`, cancels it
   in `OnDisconnectedAsync`, and aborts the connection once the revalidator returns. A revalidator that throws is
   logged, and the connection is aborted. An invocation still revalidates once the interval has passed, slides the
   session and aborts the connection when the session ended.
3. **A hub can keep its own loop.** `[BffSelfRevalidatingHub]` (`NCode.Identity.Bff.Abstractions`) marks a hub whose
   application signs the user out itself when the session ends. The filter reads it from the endpoint metadata of the
   connection's request and does not watch such a connection while it is idle. `AsBffComponents()` puts it on the
   Blazor hub and its negotiate endpoint, because a circuit whose connection closes drops into reconnection instead of
   signing out. The circuit's authentication state provider waits on the same revalidator and signs the circuit out.
4. **Support projects.** A project under `tests/` whose name ends in neither `.Tests` nor `.EndToEnd.Tests` is a
   support project that a suite references. The solution compiles it, `build/test-projects.ps1` lists it in no tier,
   and `tests/Directory.Build.props` makes it no test project and gives it no JUnit logger.
5. **A Blazor Web App in the suite.** The suite hosts `NCode.Identity.Bff.EndToEnd.BlazorApp` and its
   `NCode.Identity.Bff.EndToEnd.BlazorApp.Client` through `WebApplicationFactory<TEntryPoint>.UseKestrel()` as the third
   BFF of the suite, Keycloak client `bff-blazor`, with a `Session:RevalidationInterval` of 2 seconds. Each test is a
   theory over `BrowserNames.All` and asserts a counter of the host beside the browser's outcome. The suite proves
   that a circuit signs out after back-channel logout, that a `WebSocket` from another origin is refused, that an idle
   hub connection closes once its session ended, and that a WebAssembly component's traffic carries no token.

## Options considered

- **Revalidate only on invocations.** Rejected: an idle connection outlives its session.
- **A loop per caller.** Rejected: the hub filter and the Blazor provider would keep two copies of one rule.
- **Abort the Blazor hub's idle connection as well.** Rejected: the circuit drops into reconnection instead of signing
  out.
- **Name support projects `.Tests`.** Rejected: the gates would run them as test projects.
- **Host the app with `WebApplication.CreateBuilder` in the test process.** Rejected: the static web assets of the app
  resolve only through its own manifest, which `WebApplicationFactory` locates.

## Consequences

- A hub connection ends with its session within one `RevalidationInterval` after the session ended, whether the hub
  receives invocations or not.
- A hub that only pushes needs no code of its own to stop pushing to an ended session.
- The watch never slides the session, so an idle connection cannot keep a session alive.
- A store outage keeps a connection open, and the watch retries at the next interval.
- A Blazor circuit signs out instead of closing, and an invocation that finds the session ended still aborts the
  connection of every hub, the Blazor hub included.
- A support project under `tests/` builds with the solution and runs in no tier. Its name must not end in `.Tests`.
- The suite's third BFF host is a Blazor Web App, so a change to a render mode, to the Blazor packages or to the
  hub filter runs against Chromium, Firefox and WebKit in the end-to-end gate.

## References

- [ADR-1430](1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md),
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md)
- Issues [#603](https://github.com/NCodeGroup/identity/issues/603) and
  [#604](https://github.com/NCodeGroup/identity/issues/604)

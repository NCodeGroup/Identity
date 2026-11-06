# 1435. Server-rendered and Blazor hosts use the BFF session through pages, hubs and two opt-in Blazor packages

- **Status:** Accepted (amends
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) and
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) decision 1 and
  [ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md); decision 4
  amended by [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md):
  an idle hub connection ends with its session; decisions 5 and 6 amended by [ADR-1440](1440-the-names-a-bff-and-its-browser-clients-share-live-in-a-dependency-free-package.md):
  the Blazor server package depends only on abstractions and both halves read their shared names from
  NCode.Identity.Bff.Protocol.Abstractions)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

The design leaves one question open for hosts that render on the server
([`docs/backend-for-frontend.md`](../backend-for-frontend.md) §11 question 4, issue
[#524](https://github.com/NCodeGroup/Identity/issues/524)): how do Razor Pages, MVC and Blazor use the BFF session?

Five facts shape the answer:

- A server-rendered page is an ordinary request. It has an `HttpContext`, the BFF's middleware has set the session
  principal on it, and the page posts forms.
- An interactive Blazor Server circuit has no `HttpContext`. Microsoft documents `IHttpContextAccessor` as unavailable
  in interactive components, and the hub's response has started by the time a component runs. The circuit keeps the
  principal that the hub connection carried when it opened.
- A browser `WebSocket` cannot send a custom header, so the anti-forgery header of
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) cannot guard a hub.
- The session record is keyed and encrypted under the cookie handle
  ([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), so only the
  handle opens it. Nothing that lacks the handle can find a session by `sub` or `sid`.
- A Blazor WebAssembly component runs in the browser, where a token is exposed to injected script
  ([RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)).

## Decision

**Razor Pages, MVC and static Blazor rendering use the BFF core. A SignalR hub is a guarded endpoint kind whose
connection holds the session and obtains tokens without an `HttpContext`. Two opt-in packages serve Blazor, and a
WebAssembly client never holds a token.**

1. **Pages need no package.** A page uses `[BffPage]` or `AsBffPage()`, `HttpContext.User`, `IBffTokenAccessor` and
   `AddBffAccessTokenHandler`. The session principal carries the logout form's action and guard as `bff_logout_url` and
   `bff_logout_sid` (`BffClaimTypes`), so a page renders the logout form from `HttpContext.User` in every render mode.
   The two claim types are reserved and dropped from provider claims. A page accepts a form post as
   [ADR-1436](1436-a-bff-page-accepts-a-form-post-only-with-fetch-metadata-and-a-valid-antiforgery-token.md) decides.
2. **Hubs are guarded endpoints.** A hub carries `[BffHub]` or `AsBffHub()`. It accepts any method and refuses a request
   when Fetch Metadata shows a cross-site request or when an `Origin` is neither the frontend's `PublicOrigin` nor a
   trusted origin. A challenge is a `401` problem and a forbid is a `403` problem. A hub request slides the session. An
   endpoint that is both an API and a hub is an API, and one that is both a hub and a page is a hub: API outranks hub,
   and hub outranks page.
3. **A connection holds the session.** The principal of a hub request carries an `IBffConnectionSession` in a
   `BffConnectionIdentity`, whose copies drop it. The connection session holds the session handle in process memory for
   the life of the connection. It revalidates the session, slides it from the connection's activity and obtains tokens
   without an `HttpContext`. A token requirement receives an `IBffTokenContext`, which is a request or a connection, so
   the token accessor's rule holds: only a guarded endpoint's request, or a connection that a guarded hub request
   opened, gets a token. A request to a hub obtains tokens through its connection and never writes cookies to an
   upgraded response. `Session:RevalidationInterval` sets how often a connection revalidates: it defaults to 1 minute
   and is at most half the idle timeout.
4. **A hub filter keeps the connection current.** `BffHubFilter`, registered for every hub, revalidates a connection on
   an invocation once the interval has passed, slides the session from the invocation, aborts the connection when the
   session ended, and makes the connection current for `AddBffAccessTokenHandler` through
   `IBffConnectionSessionAccessor`.
5. **`NCode.Identity.Bff.Blazor` serves the server half.** `AddBffBlazorLibrary()` registers the revalidating server
   `AuthenticationStateProvider`, which revalidates a circuit's connection every `RevalidationInterval` and signs the
   circuit out when the session ended, or when revalidating it fails for any reason but an unavailable store, and a
   `CircuitHandler` that makes the connection current for inbound activity.
   Prerendering persists the `/bff/user` document for WebAssembly. `AsBffComponents()` marks component pages as BFF
   pages and the Blazor hub as a BFF hub that admits anonymous circuits. A Razor Components convention sees no
   application services, so `AsBffComponents()` attaches an `IBffEndpointStartupRequirement`, which is endpoint
   metadata that `BffEndpointStartupCheck`
   ([ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md)) evaluates with the
   application's services once the host has started. A host that maps `AsBffComponents()` without
   `AddBffBlazorLibrary()` refuses to start. Blazor's own framework endpoints stay outside the BFF. The package depends
   on the abstractions and on the client package.
6. **`NCode.Identity.Bff.Blazor.Client` serves the WebAssembly half.** `AddBffBlazorClient()` registers an
   `AuthenticationStateProvider` that reads the persisted document or probes `GET /bff/user`, and probes again every
   `ProbeInterval`, at the idle expiry and after a `401`, as
   [ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md) describes.
   `AddBffAntiforgeryHeader()` adds the static anti-forgery header to same-origin calls. The package never receives a
   token: the document carries only the frontend's allow-listed claims, the logout URL and the guard. It references
   neither `Microsoft.AspNetCore.App` nor `NCode.Identity.Bff.Abstractions`, because it runs in the browser.

## Options considered

- **Guidance only.** Rejected: the pieces exist, but forms, logout and every Blazor render mode would each be left to
  the host.
- **Blazor support in the core package.** Rejected: a host that serves a single-page application would carry Blazor
  services, and the WebAssembly half cannot reference the ASP.NET Core shared framework.
- **One package for both halves.** Rejected: the WebAssembly half runs in the browser and cannot reference the server
  half's dependencies.
- **`IHttpContextAccessor` in circuits.** Rejected: Microsoft documents it as unavailable in interactive components,
  and the hub's response has started.
- **A token store keyed by `sub` or `sid`.** Rejected: the session record is keyed and encrypted under the handle, so
  only the handle opens it.
- **The anti-forgery header on hubs.** Rejected: a browser `WebSocket` cannot send it. Fetch Metadata and the origin
  check guard the upgrade instead.

## Consequences

- A host opts in per package: a page host adds nothing, a Blazor Server host adds `NCode.Identity.Bff.Blazor`, and a
  WebAssembly host adds `NCode.Identity.Bff.Blazor.Client`.
- Anonymous circuits work: the Blazor hub admits a connection without a session.
- A component that runs in WebAssembly sees only the frontend's `UserClaims`, because the persisted document carries no
  other claim and no token.
- A store outage keeps a circuit signed in and fails the hub invocations that are due to revalidate.
- Revalidation fails closed: an error other than an unavailable store signs the circuit out.
- A hub that is not a Blazor hub revalidates only on invocations, so an idle connection holds its session until the
  next invocation.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.6, §5.14 and §11
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1311](1311-the-bff-logs-out-by-a-form-post-that-carries-the-sessions-logout-guard.md),
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md),
  [ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md),
  [ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md),
  [ADR-1436](1436-a-bff-page-accepts-a-form-post-only-with-fetch-metadata-and-a-valid-antiforgery-token.md)
- [RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)
- Issue [#524](https://github.com/NCodeGroup/Identity/issues/524)

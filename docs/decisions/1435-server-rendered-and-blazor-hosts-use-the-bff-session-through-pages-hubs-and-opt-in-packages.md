# 1435. Server-rendered and Blazor hosts use the BFF session through pages, hubs and opt-in packages

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

The design leaves one question open for hosts that render on the server
([`docs/backend-for-frontend.md`](../backend-for-frontend.md) §11 question 4, issue
[#524](https://github.com/NCodeGroup/identity/issues/524)): how do Razor Pages, MVC and Blazor use the BFF session?

Five facts shape the answer:

- A server-rendered page is an ordinary request. It has an `HttpContext`, the BFF's middleware has set the session
  principal on it, and the page posts forms.
- An interactive Blazor Server circuit has no `HttpContext`. Microsoft documents `IHttpContextAccessor` as unavailable
  in interactive components, and the hub's response has started by the time a component runs. The circuit keeps the
  principal that the hub connection carried when it opened.
- A browser `WebSocket` cannot send a custom header, so the anti-forgery header of
  [ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md)
  cannot guard a hub.
- The session record is keyed and encrypted under the cookie handle
  ([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), so only the
  handle opens it. Nothing that lacks the handle can find a session by `sub` or `sid`.
- A Blazor WebAssembly component runs in the browser, where a token is exposed to injected script
  ([RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)).

The Backend for Frontend (BFF) and its Blazor WebAssembly client are also two halves of one contract. They agree on the
names that cross between them: the property names of the `/bff/user` document and the key under which a Blazor server
persists that document for the client; the claim types that carry the logout form's action and guard; the paths of the
endpoints the BFF maps for each frontend; and the defaults of the anti-forgery header that same-origin calls carry. The
WebAssembly client runs in the browser. It cannot reference `NCode.Identity.Bff.Abstractions`, which needs the ASP.NET
Core shared framework, so a name defined there is out of the client's reach. An implementation package keeps its
references to abstractions
([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
so the server half of Blazor cannot reach into the client package for the names either.

## Decision

**Razor Pages, MVC and static Blazor rendering use the BFF core. A SignalR hub is a guarded endpoint kind whose
connection holds the session and obtains tokens without an `HttpContext`. Two opt-in packages serve Blazor, a
WebAssembly client never holds a token, and the names the server and the browser clients share are defined once, in a
package that depends on nothing but the runtime.**

1. **Pages need no package.** A page uses `[BffPage]` or `AsBffPage()`, `HttpContext.User`, `IBffTokenAccessor` and
   `AddBffAccessTokenHandler`. The session principal carries the logout form's action and guard as `bff_logout_url` and
   `bff_logout_sid` (`BffClaimTypes`), so a page renders the logout form from `HttpContext.User` in every render mode.
   The two claim types are reserved and dropped from provider claims.
2. **Hubs are guarded endpoints.** A hub carries `[BffHub]` or `AsBffHub()`. It accepts any method and refuses a request
   when Fetch Metadata shows a cross-site request or when an `Origin` is neither the frontend's `PublicOrigin` nor a
   trusted origin. A challenge is a `401` problem and a forbid is a `403` problem. A hub request slides the session. An
   endpoint that is both an API and a hub is an API, and one that is both a hub and a page is a hub: API outranks hub,
   and hub outranks page. The anti-forgery header does not guard a hub, because a browser `WebSocket` cannot send it.
3. **A connection holds the session.** The principal of a hub request carries an `IBffConnectionSession` in a
   `BffConnectionIdentity`, whose copies drop it. The connection session holds the session handle in process memory for
   the life of the connection. It revalidates the session, slides it from the connection's activity and obtains tokens
   without an `HttpContext`. A token requirement receives an `IBffTokenContext`, which is a request or a connection, so
   only a guarded endpoint's request, or a connection that a guarded hub request opened, gets a token. A request to a
   hub obtains tokens through its connection and never writes a cookie to an upgraded response.
   `Session:RevalidationInterval` sets how often a connection revalidates: it defaults to 1 minute and is at most half
   the idle timeout.
4. **A hub filter keeps the connection current.** `BffHubFilter`, registered for every hub, revalidates a connection on
   an invocation once the interval has passed, slides the session from the invocation and makes the connection current
   for `AddBffAccessTokenHandler` through `IBffConnectionSessionAccessor`. How a connection ends with its session is
   [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-every-watcher-learns-at-once.md)'s and
   [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-every-watcher-learns-at-once.md)'s.
5. **`NCode.Identity.Bff.Blazor` serves the server half.** `AddBffBlazorLibrary()` registers the revalidating server
   `AuthenticationStateProvider`, which revalidates a circuit's connection every `RevalidationInterval` and signs the
   circuit out when the session ended, or when revalidating it fails for any reason but an unavailable store, and a
   `CircuitHandler` that makes the connection current for inbound activity. Prerendering persists the `/bff/user`
   document for WebAssembly. `AsBffComponents()` marks component pages as BFF pages and the Blazor hub as a
   self-revalidating BFF hub that admits anonymous circuits. A Razor Components convention sees no application
   services, so `AsBffComponents()` attaches an `IBffEndpointStartupRequirement`, which is endpoint metadata that
   `BffEndpointStartupCheck`
   ([ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md))
   evaluates with the application's services once the host has started. A host that maps `AsBffComponents()` without
   `AddBffBlazorLibrary()` refuses to start. Blazor's own framework endpoints stay outside the BFF.
6. **`NCode.Identity.Bff.Blazor.Client` serves the WebAssembly half.** `AddBffBlazorClient()` registers an
   `AuthenticationStateProvider` that reads the persisted document or probes `GET /bff/user`, and probes again every
   `ProbeInterval`, at the idle expiry and after a `401`, as
   [ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md) describes.
   `AddBffAntiforgeryHeader()` adds the anti-forgery header to same-origin calls. The package never receives a token:
   the document carries only the frontend's `UserClaims`, the logout URL and the guard.
7. **The shared names live in a dependency-free package.** `NCode.Identity.Bff.Protocol.Abstractions` holds its types in
   the namespace `NCode.Identity.Bff` and has no project reference and no framework reference, as
   [ADR-1276](1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md) decides for the
   OAuth vocabulary. It holds `BffDefaults` (the cookie names, the header defaults and `RemoteApiHttpClientName`),
   `BffClaimTypes`, `BffEndpointPaths` (each endpoint's path relative to the frontend's path prefix) and
   `BffUserDocumentNames` (the `/bff/user` properties and the key a Blazor server persists the document under). The
   client keeps no copy of any of them. A name only the server reads stays in `NCode.Identity.Bff.Abstractions`.
8. **Who references what.** `NCode.Identity.Bff.Abstractions` and `NCode.Identity.Bff.Blazor.Client` reference the
   shared package. `NCode.Identity.Bff.Blazor` references only `NCode.Identity.Bff.Abstractions` and
   `NCode.Identity.Registration`, so a host that serves both render modes references both Blazor packages. The client
   references no shared framework and, of the family, only the shared package, `NCode.Identity.Json`,
   `NCode.Identity.Registration` and `NCode.Identity.Time`, because it runs in the browser.
9. **Tests enforce the boundaries.** `BffPackageDependencyTests` holds the shared package to the runtime and the server
   packages to their abstractions, `BffBlazorPackageDependencyTests` holds the server half of Blazor to the abstraction
   packages, and `BffBlazorClientPackageDependencyTests`, an allow-list, holds the client to the shared package and to
   the browser-safe Blazor assemblies.

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
- **The names in `NCode.Identity.Bff.Abstractions`, with a copy in the client.** Rejected: a wire contract that exists
  in two copies drifts, and a name that differs by one character fails only in a browser.
- **The client references `NCode.Identity.Bff.Abstractions`.** Rejected: the ASP.NET Core shared framework does not run
  in the browser.
- **The server half of Blazor references the client package.** Rejected: an implementation package that references a
  sibling implementation package gives up the rule that keeps each package replaceable
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
- **Pages, hubs, two opt-in Blazor packages and a dependency-free shared package.** Chosen: a host opts in per package,
  the one definition of each shared name is reachable from the server and from the browser, and the boundary is
  checkable by reading an assembly's references.

## Consequences

- A host opts in per package: a page host adds nothing, a Blazor Server host adds `NCode.Identity.Bff.Blazor`, and a
  WebAssembly host adds `NCode.Identity.Bff.Blazor.Client`. A host that serves interactive server and WebAssembly
  components references both, and neither package needs the other.
- Anonymous circuits work: the Blazor hub admits a connection without a session.
- A component that runs in WebAssembly sees only the frontend's `UserClaims`, because the persisted document carries no
  other claim and no token.
- A store outage keeps a circuit signed in and fails the hub invocations that are due to revalidate.
- Revalidation fails closed: an error other than an unavailable store signs the circuit out.
- Each shared name has one copy, and it is part of the wire contract, so its value stays byte-identical across the
  server and the client.
- The client compiles the package's constants into its own assembly, so a built client lists no `NCode.Identity.`
  reference beyond the shared package, if it lists one at all. Its dependency test is an allow-list and asserts no
  presence.
- A stray reference in one of these packages fails its dependency test rather than a browser.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.6, §5.14 and §11
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-a-guarded-logout-form-post-and-by-back-channel-logout-through-indexes.md),
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md),
  [ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md),
  [ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md),
  [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-every-watcher-learns-at-once.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1276](1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md)
- [RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)
- Issues [#524](https://github.com/NCodeGroup/identity/issues/524) and
  [#605](https://github.com/NCodeGroup/identity/issues/605)
